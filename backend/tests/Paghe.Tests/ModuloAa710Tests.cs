using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Documenti;
using Paghe.Persistence;
using UglyToad.PdfPig;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test dell'archivio Persone e della compilazione del modello ufficiale AA7/10:
/// il PDF prodotto è il modello AdE con i dati scritti sopra, verificati per estrazione testo.
/// </summary>
public sealed class ModuloAa710Tests : IDisposable
{
    private const string CfRappresentante = "RSSMRA80C15L117V";
    private readonly SqliteConnection _connection;

    public ModuloAa710Tests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto()
    {
        var options = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new PagheDbContext(options);
    }

    private static PersonaInput Persona(string cf = CfRappresentante) => new(
        "Rossi", "Mario", cf,
        DataNascita: new DateOnly(1980, 3, 15),
        ComuneNascita: "Terni", ProvinciaNascita: "TR");

    // ---- Archivio persone ----

    [Fact]
    public async Task Crea_e_rilegge_persona()
    {
        var tenant = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            var p = await new PersoneService(db).CreaAsync(tenant, Persona());
            Assert.Equal("RSSMRA80C15L117V", p.CodiceFiscale);
        }
        await using (var db = NuovoContesto())
        {
            var elenco = await new PersoneService(db).ElencoAsync(tenant);
            var p = Assert.Single(elenco);
            Assert.Equal("Rossi", p.Cognome);
            Assert.Equal(new DateOnly(1980, 3, 15), p.DataNascita);
        }
    }

    [Fact]
    public async Task Rifiuta_codice_fiscale_duplicato_nello_stesso_tenant()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new PersoneService(db);
        await svc.CreaAsync(tenant, Persona());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreaAsync(tenant, Persona()));
    }

    [Fact]
    public async Task Tenant_diversi_non_si_vedono()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new PersoneService(db);
        var p = await svc.CreaAsync(tenantA, Persona());

        Assert.Empty(await svc.ElencoAsync(tenantB));
        Assert.Null(await svc.OttieniAsync(tenantB, p.Id));
        Assert.False(await svc.EliminaAsync(tenantB, p.Id));
        // Stesso CF in un altro tenant: ammesso.
        await svc.CreaAsync(tenantB, Persona());
    }

    // ---- Modello AA7/10 ----

    private static ModuloAa710 ModuloVariazione() => new()
    {
        TipoDichiarazione = 3,
        CodiceFiscale = "12345670017",
        PartitaIva = "12345670017",
        Data = new DateOnly(2026, 7, 11),
        Denominazione = "Esempio Srl",
        NaturaGiuridica = "2",
        SedeLegale = new SedeAa710("Via Garibaldi 42", "05100", "Terni", "TR", ScrittureContabili: true),
        CodiceAttivita = "62.01.00",
        DescrizioneAttivita = "Produzione di software",
        Rappresentante = new RappresentanteAa710(
            "Rossi", "Mario", "1", CfRappresentante,
            "Terni", "TR", new DateOnly(1980, 3, 15)),
    };

    [Fact]
    public void Valida_segnala_i_campi_mancanti()
    {
        var errori = (ModuloVariazione() with { PartitaIva = null, NaturaGiuridica = "99" }).Valida();
        Assert.Contains(errori, e => e.Contains("Partita IVA"));
        Assert.Contains(errori, e => e.Contains("natura giuridica"));
    }

    [Fact]
    public void Compila_variazione_scrive_i_dati_sul_modello_ufficiale()
    {
        byte[] pdf = Aa710PdfUfficiale.Compila(ModuloVariazione());

        using var doc = PdfDocument.Open(pdf);
        Assert.Equal(5, doc.NumberOfPages);
        string pagina2 = doc.GetPage(2).Text;

        // Il modello ufficiale è rimasto intatto...
        Assert.Contains("QUADRO B", pagina2);
        Assert.Contains("RAPPRESENTANTE", pagina2);
        // ...e sopra ci sono i dati (maiuscoli, un carattere per cella nelle caselle a pettine).
        Assert.Contains("ESEMPIO SRL", pagina2);
        Assert.Contains("VIA GARIBALDI 42", pagina2);
        Assert.Contains("TERNI", pagina2);
        Assert.Contains("ROSSI", pagina2);
        Assert.Contains("MARIO", pagina2);
        Assert.Contains("12345670017", Compatta(pagina2));   // P.IVA nelle celle
        Assert.Contains(CfRappresentante, Compatta(pagina2));
        Assert.Contains("11072026", Compatta(pagina2));      // data variazione
        Assert.Contains("620100", Compatta(pagina2));        // ATECO senza punti

        // Codice fiscale ripetuto nella testata delle pagine successive.
        Assert.Contains("12345670017", Compatta(doc.GetPage(3).Text));
        Assert.Contains("12345670017", Compatta(doc.GetPage(5).Text));
    }

    [Fact]
    public void Compila_inizio_attivita_con_estremi_registrazione()
    {
        var modulo = ModuloVariazione() with
        {
            TipoDichiarazione = 1,
            CodiceFiscale = null,
            PartitaIva = null,
            EstremiRegistrazione = new EstremiRegistrazioneAa710(
                new DateOnly(2026, 6, 15), "TR3", "12345", "001", "1A"),
            VolumeAffariPresunto = 50_000m,
        };

        byte[] pdf = Aa710PdfUfficiale.Compila(modulo);
        using var doc = PdfDocument.Open(pdf);
        string testo = Compatta(doc.GetPage(2).Text);
        Assert.Contains("15062026", testo); // data registrazione atto
        Assert.Contains("TR3", testo);      // ufficio
        Assert.Contains("50000", testo);    // volume d'affari presunto
    }

    [Fact]
    public void Compila_rifiuta_modulo_non_valido()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Aa710PdfUfficiale.Compila(ModuloVariazione() with { PartitaIva = null }));
        Assert.Contains("Partita IVA", ex.Message);
    }

    /// <summary>Rimuove gli spazi: nelle caselle a pettine ogni carattere è scritto da solo.</summary>
    private static string Compatta(string testo) => testo.Replace(" ", "");

    public void Dispose() => _connection.Dispose();
}
