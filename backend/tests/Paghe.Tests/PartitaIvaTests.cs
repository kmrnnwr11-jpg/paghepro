using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Core.Fatturazione;
using Paghe.Core.PartiteIva;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

public class PianoAperturaPartitaIvaTests
{
    [Fact]
    public void Professionista_UsaAa9ENonRichiedeCameraCommercio()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(new PraticaAperturaPartitaIva
        {
            FormaGiuridica = FormaGiuridicaApertura.ProfessionistaPersonaFisica,
            RegimeContabile = RegimeContabilePartitaIva.Forfettario,
            DataInizioAttivita = new DateOnly(2026, 7, 1),
            Denominazione = "Mario Rossi",
            CodiceFiscale = "RSSMRA80A01F205X",
            CodiceAteco = "70.22.09",
            DescrizioneAttivita = "Consulenza aziendale",
            IndirizzoSede = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
        });

        Assert.Equal("AA9/12", piano.ModelloAgenziaEntrate);
        Assert.False(piano.RichiedeCameraCommercio);
        Assert.Contains(piano.Adempimenti, a => a.Codice == "AE-INIZIO-IVA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "PREVIDENZA");
    }

    [Fact]
    public void ImpresaCommerciale_RichiedeComUnicaCameraInpsESuap()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(new PraticaAperturaPartitaIva
        {
            FormaGiuridica = FormaGiuridicaApertura.ImpresaIndividuale,
            RegimeContabile = RegimeContabilePartitaIva.Semplificato,
            DataInizioAttivita = new DateOnly(2026, 7, 1),
            Denominazione = "Rossi Market",
            CodiceFiscale = "RSSMRA80A01F205X",
            CodiceAteco = "47.11.40",
            DescrizioneAttivita = "Minimarket",
            IndirizzoSede = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
            AttivitaCommercio = true,
        });

        Assert.True(piano.RichiedeComunicazioneUnica);
        Assert.True(piano.RichiedeCameraCommercio);
        Assert.Contains(piano.Adempimenti, a => a.Codice == "COMUNICA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "RI-REA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "INPS-COM");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "SUAP-SCIA");
    }
}

public class LiquidazioneIvaTests
{
    [Fact]
    public void Calcola_IvaVenditeMenoAcquistiDetraibili()
    {
        var fattura = new Fattura
        {
            Numero = "1/2026",
            Data = new DateOnly(2026, 7, 10),
            Cedente = Soggetto("Alfa Srl", "01234567890"),
            Cessionario = Soggetto("Beta Spa", "09876543210"),
            Righe = { new RigaFattura { Descrizione = "Servizi", PrezzoUnitario = 100m, AliquotaIva = 22m } },
        };
        var spesa = new Spesa
        {
            Data = new DateOnly(2026, 7, 11),
            Descrizione = "Software",
            ContoCosto = "6400",
            Imponibile = 50m,
            AliquotaIva = 22m,
        };

        var liq = CalcoloLiquidazioneIva.Calcola(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            PeriodicitaIva.Mensile, new[] { fattura }, new[] { spesa });

        Assert.Equal(22m, liq.IvaVendite);
        Assert.Equal(11m, liq.IvaAcquistiDetraibile);
        Assert.Equal(11m, liq.IvaDaVersare);
        Assert.Equal(0m, liq.CreditoDaRiportare);
    }

    private static DatiAnagrafici Soggetto(string denominazione, string partitaIva) => new()
    {
        Denominazione = denominazione,
        PartitaIva = partitaIva,
        Indirizzo = "Via Roma 1",
        Cap = "20100",
        Comune = "Milano",
        Provincia = "MI",
    };
}

public sealed class PraticheAperturaPartitaIvaServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PraticheAperturaPartitaIvaServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Crea_E_IsolaPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Guid praticaId;
        await using (var db = NuovoContesto())
        {
            var pratica = await new PraticheAperturaPartitaIvaService(db).CreaAsync(tenantA, Input());
            praticaId = pratica.Id;
            Assert.NotEmpty(pratica.Adempimenti);
        }

        await using (var db = NuovoContesto())
        {
            var svc = new PraticheAperturaPartitaIvaService(db);
            Assert.NotNull(await svc.GetAsync(tenantA, praticaId));
            Assert.Null(await svc.GetAsync(tenantB, praticaId));
        }
    }

    private static PraticaAperturaPartitaIvaInput Input() => new(
        FormaGiuridica: FormaGiuridicaApertura.ImpresaIndividuale,
        RegimeContabile: RegimeContabilePartitaIva.Semplificato,
        DataInizioAttivita: new DateOnly(2026, 7, 1),
        Denominazione: "Rossi Market",
        CodiceFiscale: "RSSMRA80A01F205X",
        CodiceAteco: "47.11.40",
        DescrizioneAttivita: "Minimarket",
        IndirizzoSede: "Via Roma 1",
        Cap: "20100",
        Comune: "Milano",
        Provincia: "MI",
        AttivitaCommercio: true);

    public void Dispose() => _connection.Dispose();
}
