using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Documenti;
using Paghe.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test dell'import UNILAV da PDF: parsing del testo, round-trip PDF→testo (PdfPig) e orchestrazione
/// che registra datore di lavoro + lavoratore + rapporto.
/// </summary>
public sealed class UnilavPdfTests : IDisposable
{
    private const string TestoUnilav = """
        COMUNICAZIONE OBBLIGATORIA - UNILAV
        Datore di lavoro
        Denominazione: Alfa Srl
        Codice fiscale: 01234567897
        Lavoratore
        Codice fiscale: RSSMRA80A01F205X
        Cognome: Rossi
        Nome: Mario
        Sesso: M
        Data di nascita: 01/01/1980
        Comune di nascita: Roma
        Cittadinanza: Italiana
        Comune di domicilio: Milano
        Provincia di domicilio: MI
        C.A.P.: 20100
        Rapporto di lavoro
        Data inizio: 15/03/2024
        Tipologia contrattuale: Tempo indeterminato
        C.C.N.L.: COMMERCIO_TERZIARIO
        Qualifica professionale: Impiegato amministrativo
        Livello di inquadramento: 3
        Tipo orario: Full time
        Ore settimanali: 40
        Retribuzione: 1500,00
        """;

    private readonly SqliteConnection _connection;

    public UnilavPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    // --- Parser del testo ---

    [Fact]
    public void Parser_RiconosceDatoreLavoratoreRapporto()
    {
        var r = ImportazioneUnilavPdf.Importa(TestoUnilav);

        Assert.Equal("01234567897", r.Datore.CodiceFiscale);
        Assert.Equal("Alfa Srl", r.Datore.Denominazione);

        Assert.Equal("RSSMRA80A01F205X", r.Lavoratore.CodiceFiscale);
        Assert.Equal("Rossi", r.Lavoratore.Cognome);
        Assert.Equal("Mario", r.Lavoratore.Nome);
        Assert.Equal(new DateOnly(1980, 1, 1), r.Lavoratore.DataNascita);
        Assert.Equal("Milano", r.Lavoratore.ComuneDomicilio);

        Assert.Equal(new DateOnly(2024, 3, 15), r.Rapporto.DataInizio);
        Assert.Equal("COMMERCIO_TERZIARIO", r.Rapporto.Ccnl);
        Assert.Equal("3", r.Rapporto.Livello);
    }

    // --- Round-trip: PDF generato → testo estratto con PdfPig ---

    [Fact]
    public void RoundTrip_PdfGeneratoVieneRiletto()
    {
        byte[] pdf = GeneraPdf(TestoUnilav);

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);

        Assert.Contains("RSSMRA80A01F205X", testo);
        Assert.Contains("Alfa Srl", testo);
        Assert.Contains("Rossi", testo);
    }

    // --- Orchestrazione: registra datore + lavoratore + rapporto ---

    [Fact]
    public async Task Servizio_RegistraDatoreLavoratoreRapporto_IsolatoPerTenant()
    {
        var tenant = Guid.NewGuid();

        EsitoImportUnilav esito;
        await using (var db = NuovoContesto())
            esito = await new ImportUnilavService(db).ImportaDaTestoAsync(tenant, TestoUnilav);

        Assert.True(esito.AziendaCreata);
        Assert.Equal("Alfa Srl", esito.Azienda.RagioneSociale);
        Assert.Equal("01234567897", esito.Azienda.PartitaIva); // 11 cifre -> P.IVA
        Assert.False(esito.LavoratoreEsistente);
        Assert.Equal("Rossi", esito.Rapporto.Dipendente!.Cognome);
        Assert.Equal("RSSMRA80A01F205X", esito.Rapporto.Dipendente!.CodiceFiscale);
        Assert.Equal(new DateOnly(2024, 3, 15), esito.Rapporto.DataAssunzione);

        // Persistito e isolato per tenant.
        var altroTenant = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            Assert.Equal(1, await db.Aziende.CountAsync(a => a.TenantId == tenant));
            Assert.Equal(1, await db.Dipendenti.CountAsync(d => d.TenantId == tenant));
            Assert.Equal(1, await db.Rapporti.CountAsync(rp => rp.TenantId == tenant));
            Assert.Equal(0, await db.Dipendenti.CountAsync(d => d.TenantId == altroTenant));
        }
    }

    [Fact]
    public async Task Servizio_LavoratoreGiaPresente_AggiungeRapportoSenzaDuplicare()
    {
        var tenant = Guid.NewGuid();

        await using (var db = NuovoContesto())
            await new ImportUnilavService(db).ImportaDaTestoAsync(tenant, TestoUnilav);

        EsitoImportUnilav secondo;
        await using (var db = NuovoContesto())
            secondo = await new ImportUnilavService(db).ImportaDaTestoAsync(tenant, TestoUnilav);

        Assert.False(secondo.AziendaCreata);      // stesso datore
        Assert.True(secondo.LavoratoreEsistente);  // stesso lavoratore

        await using (var db = NuovoContesto())
        {
            Assert.Equal(1, await db.Aziende.CountAsync(a => a.TenantId == tenant));
            Assert.Equal(1, await db.Dipendenti.CountAsync(d => d.TenantId == tenant));
            Assert.Equal(2, await db.Rapporti.CountAsync(rp => rp.TenantId == tenant)); // due rapporti
        }
    }

    [Fact]
    public async Task Servizio_ConAziendaScelta_AttaccaAQuellaSenzaCrearne()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaSceltaId;
        await using (var db = NuovoContesto())
        {
            var az = await new AziendeService(db).CreaAsync(
                tenant, new Paghe.Persistence.Entities.AziendaEntity { RagioneSociale = "Studio Cliente Srl" });
            aziendaSceltaId = az.Id;
        }

        EsitoImportUnilav esito;
        await using (var db = NuovoContesto())
            esito = await new ImportUnilavService(db)
                .ImportaDaTestoAsync(tenant, TestoUnilav, aziendaIdScelto: aziendaSceltaId);

        Assert.False(esito.AziendaCreata);
        Assert.Equal(aziendaSceltaId, esito.Azienda.Id);

        await using (var db = NuovoContesto())
        {
            // Nessuna azienda nuova creata dal datore del PDF: resta solo quella scelta.
            Assert.Equal(1, await db.Aziende.CountAsync(a => a.TenantId == tenant));
            Assert.Equal(1, await db.Rapporti.CountAsync(r => r.TenantId == tenant && r.AziendaId == aziendaSceltaId));
        }
    }

    [Fact]
    public async Task Servizio_CcnlAgricolo_MappatoAInternoEcedolinoCalcolabile()
    {
        const string testo = """
            Datore di Lavoro
            Codice Fiscale: 01174590339
            Ragione Sociale: Az. Agricola Esempio
            Lavoratore
            Codice Fiscale: TLKKRN04P28Z249M
            Cognome: TALUKDAR Nome: KAMRAN
            Dati Rapporto
            Data Inizio: 11/04/2026
            Qualifica: 8.3.1.1.0 - BRACCIANTI AGRICOLI
            CCNL: 0190 - AGRICOLTURA - Operai florovivaisti
            Livello inquadramento: ND - Non dichiarato
            """;
        var tenant = Guid.NewGuid();
        await using (var db = NuovoContesto())
            await new ParametriNormativiService(db).ImportaSeedSeVuotoAsync();

        EsitoImportUnilav esito;
        await using (var db = NuovoContesto())
            esito = await new ImportUnilavService(db).ImportaDaTestoAsync(tenant, testo);

        // CCNL del portale "0190 - AGRICOLTURA" mappato al CCNL interno; livello "ND" -> default "3".
        Assert.Equal("AGRICOLTURA_OPERAI", esito.Rapporto.CodiceCcnl);
        Assert.Equal("3", esito.Rapporto.Livello);

        // Il CCNL agricolo è caricato a DB con i suoi livelli: il cedolino è calcolabile.
        await using (var db = NuovoContesto())
        {
            var ccnl = await new ParametriNormativiService(db).CaricaCcnlAsync("AGRICOLTURA_OPERAI", 2026);
            Assert.Contains(ccnl.Livelli, l => l.Codice == "3");
        }
    }

    private static byte[] GeneraPdf(string testo) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Column(col =>
                {
                    foreach (var riga in testo.Replace("\r\n", "\n").Split('\n'))
                        col.Item().Text(string.IsNullOrWhiteSpace(riga) ? " " : riga).FontSize(10);
                });
            });
        }).GeneratePdf();

    public void Dispose() => _connection.Dispose();
}
