using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Una busta paga consegnata non deve cambiare. Il PDF si rigenera leggendo l'archivio — totali e
/// voci del giorno dell'elaborazione — invece di ricalcolare con i parametri di oggi: se domani
/// cambia un minimo tabellare, una regola del CCNL o un'aliquota, la ristampa resta quella
/// consegnata al dipendente.
/// </summary>
public sealed class FotografiaCedolinoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();

    public FotografiaCedolinoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    private async Task<Guid> ArchiviaUnCedolinoAsync()
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567897",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            _tenant, azienda.Id,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });

        var svc = new CedoliniService(db, new ParametriNormativiService(db),
            new OrologioFisso(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)));
        var cedolino = await svc.CalcolaEArchiviaAsync(
            _tenant, rapporto.Id,
            new Cartellino { Anno = 2026, Mese = 5, OreStraordinario = 6m });
        return cedolino.Id;
    }

    [Fact]
    public async Task IlPdfCoincideConIValoriArchiviati()
    {
        var id = await ArchiviaUnCedolinoAsync();

        await using var db = NuovoContesto();
        var archiviato = await db.Cedolini.SingleAsync(c => c.Id == id);
        var perPdf = await new CedoliniService(db, new ParametriNormativiService(db))
            .LeggiPerPdfAsync(_tenant, id);

        Assert.NotNull(perPdf);
        Assert.Equal(archiviato.NettoInBusta, perPdf.Cedolino.NettoInBusta);
        Assert.Equal(archiviato.TotaleCompetenze, perPdf.Cedolino.TotaleCompetenze);
        Assert.Equal(archiviato.ContributiDipendente, perPdf.Cedolino.ContributiDipendente);
        Assert.Equal(archiviato.QuotaTfr, perPdf.Cedolino.QuotaTfr);
        Assert.Equal(archiviato.CostoAzienda, perPdf.Cedolino.CostoAzienda);
        Assert.Equal(archiviato.CostoMedioAnnualizzato, perPdf.Cedolino.CostoMedioAnnualizzato);
    }

    [Fact]
    public async Task CambiareIlMinimoTabellareNonCambiaUnaBustaGiaFatta()
    {
        var id = await ArchiviaUnCedolinoAsync();

        decimal nettoPrima;
        await using (var db = NuovoContesto())
            nettoPrima = (await db.Cedolini.SingleAsync(c => c.Id == id)).NettoInBusta;

        // Rinnovo contrattuale: il minimo del livello 3 sale di 200 euro.
        await using (var db = NuovoContesto())
        {
            var livello = await db.CcnlLivelli.SingleAsync(
                l => l.CcnlCodice == "COMMERCIO_TERZIARIO" && l.Anno == 2026 && l.Livello == "3");
            livello.MinimoTabellare += 200m;
            await db.SaveChangesAsync();
        }

        await using var contesto = NuovoContesto();
        var perPdf = await new CedoliniService(contesto, new ParametriNormativiService(contesto))
            .LeggiPerPdfAsync(_tenant, id);

        Assert.NotNull(perPdf);
        Assert.Equal(nettoPrima, perPdf.Cedolino.NettoInBusta);
    }

    [Fact]
    public async Task LeVociEscononoNellOrdineInCuiSonoStateCalcolate()
    {
        var id = await ArchiviaUnCedolinoAsync();

        await using var db = NuovoContesto();
        var perPdf = await new CedoliniService(db, new ParametriNormativiService(db))
            .LeggiPerPdfAsync(_tenant, id);

        var codici = perPdf!.Cedolino.Righe.Select(r => r.Codice).ToList();
        // La paga base apre la busta e lo straordinario viene dopo: è l'ordine del motore,
        // non quello in cui il database restituisce le righe.
        Assert.Equal("PAGA_BASE", codici[0]);
        Assert.True(codici.IndexOf("STRAORD") > codici.IndexOf("PAGA_BASE"));
        Assert.Contains("INPS_DIP", codici);
    }
}
