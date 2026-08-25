using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using UglyToad.PdfPig;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della Certificazione Unica completa: decodifica anagrafica dal codice fiscale,
/// costruzione del modello dai cedolini archiviati e compilazione del PDF sul modello
/// ufficiale AdE (verificata per estrazione testo).
/// </summary>
public sealed class CertificazioneUnicaModelloTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CertificazioneUnicaModelloTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    // ---- Decodifica anagrafica dal codice fiscale ----

    [Fact]
    public void CodiceFiscale_decodifica_uomo()
    {
        var a = AnagraficaCodiceFiscale.Decodifica("RSSMRA85T10F257K", 2025);
        Assert.NotNull(a);
        Assert.Equal("M", a.Sesso);
        Assert.Equal(new DateOnly(1985, 12, 10), a.DataNascita);
        Assert.Equal("F257", a.CodiceCatastaleNascita);
    }

    [Fact]
    public void CodiceFiscale_decodifica_donna_e_secolo()
    {
        // Giorno 41 = 1 + 40 (femmina); anno "05" con riferimento 2025 = 2005.
        var a = AnagraficaCodiceFiscale.Decodifica("VRDLSU05A41F205Z", 2025);
        Assert.NotNull(a);
        Assert.Equal("F", a.Sesso);
        Assert.Equal(new DateOnly(2005, 1, 1), a.DataNascita);
        Assert.Equal("F205", a.CodiceCatastaleNascita);
    }

    [Fact]
    public void CodiceFiscale_decodifica_omocodia()
    {
        // Cifre sostituite da lettere (L=0, M=1, ...): giorno "M0" = 10, anno "8S" = 86.
        var a = AnagraficaCodiceFiscale.Decodifica("RSSMRA8SDM0F25TK", 2025);
        Assert.NotNull(a);
        Assert.Equal("M", a.Sesso);
        Assert.Equal(new DateOnly(1986, 4, 10), a.DataNascita);
        Assert.Equal("F257", a.CodiceCatastaleNascita);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TROPPOCORTO")]
    [InlineData("RSSMRA85Z10F257K")] // mese inesistente
    public void CodiceFiscale_non_standard_restituisce_null(string? cf) =>
        Assert.Null(AnagraficaCodiceFiscale.Decodifica(cf, 2025));

    [Fact]
    public void ComuniCatastali_trova_il_comune_dal_codice()
    {
        var milano = ComuniCatastali.Trova("F205");
        Assert.NotNull(milano);
        Assert.Equal("MILANO", milano.Nome, ignoreCase: true);
        Assert.Equal("MI", milano.Provincia);
        Assert.Null(ComuniCatastali.Trova("Z999"));
        Assert.Null(ComuniCatastali.Trova(null));
    }

    // ---- Costruzione del modello dai cedolini archiviati ----

    private async Task<(Guid AziendaId, Guid RapportoId)> SeedAziendaConCedoliniAsync(Guid tenant)
    {
        Guid aziendaId;
        await using (var db = NuovoContesto())
        {
            var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
            {
                RagioneSociale = "Alfa Srl", PartitaIva = "01234561007",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
                CodiceAteco = "620100", MatricolaInps = "4971234567",
                Email = "paghe@alfa.it", Telefono = "02 123456",
            });
            aziendaId = az.Id;
        }

        Guid rapportoId;
        await using (var db = NuovoContesto())
        {
            var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
                tenant, aziendaId,
                new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
                new RapportoLavoroEntity
                {
                    CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                    NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                    DataAssunzione = new DateOnly(2020, 1, 1),
                });
            rapportoId = rapporto.Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 1 });
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 2 });

            // Questa fixture verifica l'aggregazione CU, non il workflow. Simula quindi cedolini
            // realmente anteriori al workflow senza forgiare una chiusura priva di snapshot/PDF.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE Cedolini
                SET Stato = {nameof(StatoCedolino.PreWorkflow)}, Versione = 1,
                    ParametriApplicatiProtetti = '', HashParametriSha256 = ''
                WHERE TenantId = {tenant} AND RapportoLavoroId = {rapportoId} AND Anno = 2026
                """);
        }
        return (aziendaId, rapportoId);
    }

    [Fact]
    public async Task CostruisceModelloCompletoDaiCedoliniArchiviati()
    {
        var tenant = Guid.NewGuid();
        var (_, rapportoId) = await SeedAziendaConCedoliniAsync(tenant);

        await using var db = NuovoContesto();
        var cu = await new CertificazioneUnicaService(db).CostruisciModelloAsync(tenant, rapportoId, 2026);

        Assert.NotNull(cu);
        Assert.Empty(cu.Valida());
        Assert.Equal(2026, cu.AnnoRedditi);

        // Sostituto: senza CF usa la P.IVA.
        Assert.Equal("01234561007", cu.Sostituto.CodiceFiscale);
        Assert.Equal("Alfa Srl", cu.Sostituto.Denominazione);

        // Percipiente: sesso/nascita dal CF, domicilio fiscale dal comune delle addizionali.
        Assert.Equal("M", cu.Percipiente.Sesso);
        Assert.Equal(new DateOnly(1980, 1, 1), cu.Percipiente.DataNascita);
        Assert.Equal("MILANO", cu.Percipiente.ComuneNascita, ignoreCase: true);
        Assert.Equal("MILANO", cu.Percipiente.DomicilioComune, ignoreCase: true);
        Assert.Equal("F205", cu.Percipiente.DomicilioCodiceComune);

        // Fiscale: contratto a tempo indeterminato → punto 1, punto 2 vuoto.
        Assert.True(cu.DatiFiscali.RedditoTempoIndeterminato > 0m);
        Assert.Equal(0m, cu.DatiFiscali.RedditoTempoDeterminato);
        Assert.Equal(365, cu.DatiFiscali.GiorniDetrazione);
        Assert.Equal(new DateOnly(2020, 1, 1), cu.DatiFiscali.DataInizioRapporto);
        Assert.True(cu.DatiFiscali.RitenuteIrpef > 0m);
        Assert.True(cu.DatiFiscali.ImpostaLorda > 0m);
        Assert.Equal(
            Math.Max(0m, cu.DatiFiscali.ImpostaLorda - cu.DatiFiscali.TotaleDetrazioni),
            cu.DatiFiscali.ImpostaNetta);

        // Previdenza: matricola, imponibile e mesi denunciati.
        Assert.Equal("4971234567", cu.Previdenza.MatricolaAzienda);
        Assert.True(cu.Previdenza.ImponibilePrevidenziale > 0m);
        Assert.True(cu.Previdenza.ContributiLavoratore > 0m);
        Assert.Equal(new[] { 1, 2 }, cu.Previdenza.MesiRetribuiti);

        Assert.Contains(cu.Annotazioni, n => n.Contains("BOZZA DA VALIDARE"));
    }

    [Fact]
    public async Task AnnoSenzaCedolini_Lancia_E_RapportoInesistente_RestituisceNull()
    {
        var tenant = Guid.NewGuid();
        var (_, rapportoId) = await SeedAziendaConCedoliniAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new CertificazioneUnicaService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CostruisciModelloAsync(tenant, rapportoId, 2024));
        Assert.Null(await svc.CostruisciModelloAsync(tenant, Guid.NewGuid(), 2026));
        // Isolamento tenant: un altro tenant non vede il rapporto.
        Assert.Null(await svc.CostruisciModelloAsync(Guid.NewGuid(), rapportoId, 2026));
    }

    // ---- Compilazione del PDF sul modello ufficiale ----

    private static CertificazioneUnica CuEsempio() => new()
    {
        AnnoRedditi = 2025,
        Sostituto = new SostitutoCu
        {
            CodiceFiscale = "01234561007", Denominazione = "Rossi Impianti Srl",
            Comune = "Modena", Provincia = "MO", Cap = "41121", Indirizzo = "Via Emilia Est 120",
            CodiceAttivita = "432101",
        },
        Percipiente = new PercipienteCu
        {
            CodiceFiscale = "RSSMRA85T10F257K", Cognome = "Rossi", Nome = "Mario",
            Sesso = "M", DataNascita = new DateOnly(1985, 12, 10),
            ComuneNascita = "Modena", ProvinciaNascita = "MO",
            DomicilioComune = "Modena", DomicilioProvincia = "MO", DomicilioCodiceComune = "F257",
        },
        DatiFiscali = new DatiFiscaliCu
        {
            RedditoTempoIndeterminato = 28450.32m, GiorniDetrazione = 365,
            DataInizioRapporto = new DateOnly(2019, 3, 1), InForzaAl31Dicembre = true,
            RitenuteIrpef = 6104.55m, AddizionaleRegionale = 483.66m, AddizionaleComunale = 142.25m,
            ImpostaLorda = 7529.10m, DetrazioniLavoroDipendente = 1424.55m,
            TotaleDetrazioni = 1424.55m, ImpostaNetta = 6104.55m,
        },
        Previdenza = new PrevidenzaCu
        {
            MatricolaAzienda = "7010203040",
            ImponibilePrevidenziale = 31230.00m, ContributiLavoratore = 2870.05m,
            MesiRetribuiti = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        },
        Annotazioni = ["Quota TFR maturata nell'anno: euro 2.158,44."],
    };

    [Fact]
    public void CompilaPdf_ScriveIDatiSulModelloUfficiale()
    {
        byte[] pdf = CuPdfUfficiale.Compila(CuEsempio());

        using var doc = PdfDocument.Open(pdf);
        Assert.Equal(11, doc.NumberOfPages);

        // Pagina anagrafica: modello intatto + dati di sostituto e percipiente.
        string p1 = doc.GetPage(1).Text;
        Assert.Contains("DATI ANAGRAFICI", p1);
        Assert.Contains("01234561007", p1);
        Assert.Contains("ROSSI IMPIANTI SRL", p1);
        Assert.Contains("RSSMRA85T10F257K", p1);
        Assert.Contains("2025", p1); // riquadro "RELATIVA ALL'ANNO"

        // Parte B dati fiscali: reddito, ritenute, imposta lorda/netta.
        string p2 = doc.GetPage(2).Text;
        Assert.Contains("28.450,32", p2);
        Assert.Contains("6.104,55", p2);
        Assert.Contains("7.529,10", p2);
        Assert.Contains("1.424,55", p2);

        // Previdenza INPS: matricola e imponibile.
        string p5 = doc.GetPage(5).Text;
        Assert.Contains("7010203040", p5);
        Assert.Contains("31.230,00", p5);

        // Annotazioni.
        Assert.Contains("QUOTA TFR MATURATA", doc.GetPage(7).Text);
    }

    [Fact]
    public void CompilaPdf_RifiutaModelloNonValido()
    {
        var senzaCf = CuEsempio() with
        {
            Percipiente = new PercipienteCu { CodiceFiscale = "", Cognome = "Rossi" },
        };
        var ex = Assert.Throws<InvalidOperationException>(() => CuPdfUfficiale.Compila(senzaCf));
        Assert.Contains("Codice fiscale", ex.Message);
    }

    [Theory]
    [InlineData(2022)]
    [InlineData(2026)]
    public void CompilaPdf_RifiutaUnAnnoNonCompatibileConIlModelloCu2026(int annoRedditi)
    {
        var cu = CuEsempio() with { AnnoRedditi = annoRedditi };

        var ex = Assert.Throws<ArgumentException>(() => CuPdfUfficiale.Compila(cu));

        Assert.Contains("CU 2026", ex.Message);
        Assert.Contains("redditi 2025", ex.Message);
        Assert.Contains(annoRedditi.ToString(), ex.Message);
    }

    public void Dispose() => _connection.Dispose();
}
