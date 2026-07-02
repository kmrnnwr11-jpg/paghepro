using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Coerenza incrociata sui documenti dell'anno: i cedolini archiviati, la Certificazione Unica,
/// il Modello 770 e gli F24 mensili devono raccontare gli <b>stessi numeri</b>. Scenario: un anno
/// di cedolini per due dipendenti (uno full-time tutto l'anno, uno part-time 50% assunto a luglio).
/// </summary>
public sealed class CoerenzaAnnualeTests : IDisposable
{
    private const int Anno = 2026;
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();
    private Guid _aziendaId;
    private Guid _rapportoFullTimeId;   // 12 cedolini (gen-dic)
    private Guid _rapportoPartTimeId;   // 6 cedolini (lug-dic), part-time 50%

    public CoerenzaAnnualeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
        }
        PreparaAnnoDiCedolini().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task PreparaAnnoDiCedolini()
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890", CodiceFiscale = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        _aziendaId = azienda.Id;

        var anagrafiche = new AnagraficheLavoroService(db);
        var fullTime = await anagrafiche.CreaDipendenteERapportoAsync(
            _tenant, _aziendaId,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });
        _rapportoFullTimeId = fullTime.Id;

        var partTime = await anagrafiche.CreaDipendenteERapportoAsync(
            _tenant, _aziendaId,
            new DipendenteEntity { CodiceFiscale = "VRDLGI85B02F205Y", Nome = "Luigi", Cognome = "Verdi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "4", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                PartTime = true, PercentualePartTime = 50m,
                DataAssunzione = new DateOnly(Anno, 7, 1),
            });
        _rapportoPartTimeId = partTime.Id;

        var cedolini = new CedoliniService(db, new ParametriNormativiService(db));
        for (int mese = 1; mese <= 12; mese++)
            await cedolini.CalcolaEArchiviaAsync(_tenant, _rapportoFullTimeId,
                new Cartellino { Anno = Anno, Mese = mese });
        for (int mese = 7; mese <= 12; mese++)
            await cedolini.CalcolaEArchiviaAsync(_tenant, _rapportoPartTimeId,
                new Cartellino { Anno = Anno, Mese = mese });
    }

    private async Task<List<CedolinoEntity>> CedoliniDellAnno()
    {
        await using var db = NuovoContesto();
        return await db.Cedolini
            .Where(c => c.TenantId == _tenant && c.AziendaId == _aziendaId && c.Anno == Anno)
            .ToListAsync();
    }

    [Fact]
    public async Task ImportiCedolino_SempreAlCentesimo_ENettoQuadra()
    {
        var cedolini = await CedoliniDellAnno();
        Assert.Equal(18, cedolini.Count); // 12 full-time + 6 part-time

        foreach (var c in cedolini)
        {
            // Ogni importo persistito è arrotondato al centesimo (mai sub-centesimi nascosti).
            foreach (var (nome, v) in new (string, decimal)[]
            {
                ("TotaleCompetenze", c.TotaleCompetenze), ("TotaleTrattenute", c.TotaleTrattenute),
                ("ImponibileInps", c.ImponibileInps), ("ContributiDipendente", c.ContributiDipendente),
                ("ContributiDatore", c.ContributiDatore), ("ImponibileIrpef", c.ImponibileIrpef),
                ("IrpefNetta", c.IrpefNetta), ("AddizionaleRegionale", c.AddizionaleRegionale),
                ("AddizionaleComunale", c.AddizionaleComunale), ("NettoInBusta", c.NettoInBusta),
            })
                Assert.True(decimal.Round(v, 2) == v, $"{nome}={v} non è al centesimo ({c.Mese}/{c.Anno})");

            // Il netto è esattamente competenze - trattenute.
            Assert.Equal(c.TotaleCompetenze - c.TotaleTrattenute, c.NettoInBusta);
        }
    }

    [Fact]
    public async Task CertificazioneUnica_StessiTotaliDeiCedoliniArchiviati()
    {
        var cedolini = await CedoliniDellAnno();
        await using var db = NuovoContesto();
        var cu = new CertificazioneUnicaService(db);

        foreach (var (rapportoId, attesi) in new[]
        {
            (_rapportoFullTimeId, cedolini.Where(c => c.RapportoLavoroId == _rapportoFullTimeId).ToList()),
            (_rapportoPartTimeId, cedolini.Where(c => c.RapportoLavoroId == _rapportoPartTimeId).ToList()),
        })
        {
            var bozza = await cu.CostruisciAsync(_tenant, rapportoId, Anno);
            Assert.NotNull(bozza);
            Assert.Equal(attesi.Count, bozza!.NumeroCedolini);
            Assert.Equal(attesi.Sum(c => c.ImponibileIrpef), bozza.TotaleImponibileIrpef);
            Assert.Equal(attesi.Sum(c => c.IrpefNetta), bozza.TotaleRitenuteIrpef);
            Assert.Equal(attesi.Sum(c => c.AddizionaleRegionale), bozza.TotaleAddizionaleRegionale);
            Assert.Equal(attesi.Sum(c => c.AddizionaleComunale), bozza.TotaleAddizionaleComunale);
            Assert.Equal(attesi.Sum(c => c.ImponibileInps), bozza.TotaleImponibileInps);
            Assert.Equal(attesi.Sum(c => c.NettoInBusta), bozza.TotaleNetto);
        }

        // Il part-time assunto a luglio certifica SOLO i 6 mesi lavorati.
        var cuPartTime = await cu.CostruisciAsync(_tenant, _rapportoPartTimeId, Anno);
        Assert.Equal(6, cuPartTime!.NumeroCedolini);
    }

    [Fact]
    public async Task Modello770_QuadraConLaSommaDelleCu()
    {
        await using var db = NuovoContesto();
        var bozze = await new CertificazioneUnicaService(db).CostruisciPerAziendaAsync(_tenant, _aziendaId, Anno);
        var m770 = await new Modello770Service(db).CostruisciAsync(_tenant, _aziendaId, Anno);

        Assert.Equal(2, m770.NumeroPercipienti);
        Assert.Equal(18, m770.NumeroCedolini);
        Assert.Equal(bozze.Sum(b => b.TotaleRitenuteIrpef), m770.TotaleRitenuteIrpef);
        Assert.Equal(bozze.Sum(b => b.TotaleAddizionaleRegionale), m770.TotaleAddizionaleRegionale);
        Assert.Equal(bozze.Sum(b => b.TotaleAddizionaleComunale), m770.TotaleAddizionaleComunale);

        // Anche per percipiente: la riga 770 di ciascun dipendente = la sua CU.
        foreach (var b in bozze)
        {
            var riga = Assert.Single(m770.Percipienti, p => p.CodiceFiscale == b.CodiceFiscale);
            Assert.Equal(b.TotaleRitenuteIrpef, riga.RitenuteIrpef);
            Assert.Equal(b.TotaleAddizionaleRegionale, riga.AddizionaleRegionale);
            Assert.Equal(b.TotaleAddizionaleComunale, riga.AddizionaleComunale);
        }
    }

    [Fact]
    public async Task F24Mensili_QuadranoConCedolini_ELaSommaAnnuaCol770()
    {
        var cedolini = await CedoliniDellAnno();
        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));

        decimal irpefVersata = 0m, addRegVersata = 0m, addComVersata = 0m, inpsVersati = 0m;
        for (int mese = 1; mese <= 12; mese++)
        {
            var delMese = cedolini.Where(c => c.Mese == mese).ToList();
            var f24 = await svc.GeneraF24DelMeseAsync(_tenant, _aziendaId, Anno, mese, "1234567890");

            // Erario 1001 = somma IRPEF dei cedolini del mese (già al centesimo).
            var voce1001 = Assert.Single(f24.Erario, v => v.CodiceTributo == "1001");
            Assert.Equal(delMese.Sum(c => c.IrpefNetta), voce1001.ImportoDebito);

            // Addizionali del mese (unica regione/comune nello scenario).
            Assert.Equal(delMese.Sum(c => c.AddizionaleRegionale), f24.Regioni.Sum(v => v.ImportoDebito));
            Assert.Equal(delMese.Sum(c => c.AddizionaleComunale), f24.TributiLocali.Sum(v => v.ImportoDebito));

            // INPS = contributi dipendente + datore del mese.
            Assert.Equal(delMese.Sum(c => c.ContributiDipendente + c.ContributiDatore),
                f24.Inps.Sum(v => v.ImportoDebito));

            irpefVersata += voce1001.ImportoDebito;
            addRegVersata += f24.Regioni.Sum(v => v.ImportoDebito);
            addComVersata += f24.TributiLocali.Sum(v => v.ImportoDebito);
            inpsVersati += f24.Inps.Sum(v => v.ImportoDebito);
        }

        // La somma dei 12 F24 racconta gli stessi numeri del 770 (ritenute operate nell'anno).
        var m770 = await new Modello770Service(db).CostruisciAsync(_tenant, _aziendaId, Anno);
        Assert.Equal(m770.TotaleRitenuteIrpef, irpefVersata);
        Assert.Equal(m770.TotaleAddizionaleRegionale, addRegVersata);
        Assert.Equal(m770.TotaleAddizionaleComunale, addComVersata);

        // E i contributi versati coincidono con quelli maturati sui cedolini dell'anno.
        Assert.Equal(cedolini.Sum(c => c.ContributiDipendente + c.ContributiDatore), inpsVersati);
    }

    public void Dispose() => _connection.Dispose();
}
