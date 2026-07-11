using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della logica di creazione azienda, gestione dei dati contributivi/normativi,
/// verifica dei duplicati (P.IVA / CF) e isolamento multi-tenant.
/// </summary>
public sealed class AziendaCreazioneTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public AziendaCreazioneTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Crea_AziendaCompleta_SalvaTuttiICampiEDatiContributivi()
    {
        await using var db = NuovoContesto();
        var svc = new AziendeService(db);

        var azienda = await svc.CreaAsync(_tenantA, new AziendaEntity
        {
            RagioneSociale = "  Azienda Alfa Srl  ",
            PartitaIva = "12345678901",
            CodiceFiscale = "12345678901",
            Indirizzo = "Via Roma 100",
            Cap = "00100",
            Comune = "Roma",
            Provincia = "rm",
            CodiceCatastale = "H501",
            CodiceDestinatarioSdi = "0000000",
            Pec = "alfa@pec.it",
            Settore = "COMMERCIO_SERVIZI",
            CodiceAteco = "47.91.10",
            MatricolaInps = "1234567890",
            CodiceSedeInps = "4500",
            CscInps = "10101",
            PatInail = "12345678/90",
            CcnlDefault = "COMMERCIO",
            LivelloDefault = "3",
            Iban = "IT60X0542811101000000123456",
            LegaleRappresentanteNome = "Mario",
            LegaleRappresentanteCognome = "Rossi",
            LegaleRappresentanteCodiceFiscale = "RSSMRA80A01F205X",
            HaDipendenti = true
        });

        Assert.NotEqual(Guid.Empty, azienda.Id);
        Assert.Equal("Azienda Alfa Srl", azienda.RagioneSociale);
        Assert.Equal("12345678901", azienda.PartitaIva);
        Assert.Equal("12345678901", azienda.CodiceFiscale);
        Assert.Equal("RM", azienda.Provincia);
        Assert.Equal("H501", azienda.CodiceCatastale);
        Assert.Equal("0000000", azienda.CodiceDestinatarioSdi);
        Assert.Equal("alfa@pec.it", azienda.Pec);
        Assert.Equal("COMMERCIO_SERVIZI", azienda.Settore);
        Assert.Equal("47.91.10", azienda.CodiceAteco);
        Assert.Equal("1234567890", azienda.MatricolaInps);
        Assert.Equal("10101", azienda.CscInps);
        Assert.Equal("12345678/90", azienda.PatInail);
        Assert.Equal("COMMERCIO", azienda.CcnlDefault);
        Assert.Equal("IT60X0542811101000000123456", azienda.Iban);
        Assert.Equal("Mario", azienda.LegaleRappresentanteNome);
        Assert.Equal("Rossi", azienda.LegaleRappresentanteCognome);
        Assert.Equal("RSSMRA80A01F205X", azienda.LegaleRappresentanteCodiceFiscale);

        var sintesi = await svc.GetSintesiAsync(_tenantA, azienda.Id);
        Assert.NotNull(sintesi);
        Assert.True(sintesi.DatiContributiviCompleti);
        Assert.Empty(sintesi.DatiMancanti);
    }

    [Fact]
    public async Task Crea_DuplicatoPartitaIvaStessoTenant_LanciaEccezione()
    {
        await using var db1 = NuovoContesto();
        var svc1 = new AziendeService(db1);

        await svc1.CreaAsync(_tenantA, new AziendaEntity
        {
            RagioneSociale = "Azienda Uno",
            PartitaIva = "11111111111",
            Indirizzo = "Via Test 1",
            Cap = "20100", Comune = "Milano", Provincia = "MI"
        });

        await using var db2 = NuovoContesto();
        var svc2 = new AziendeService(db2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc2.CreaAsync(_tenantA, new AziendaEntity
            {
                RagioneSociale = "Azienda Due Duplicata",
                PartitaIva = "11111111111",
                Indirizzo = "Via Test 2",
                Cap = "20100", Comune = "Milano", Provincia = "MI"
            }));

        Assert.Contains("11111111111", ex.Message);
    }

    [Fact]
    public async Task Crea_StessaPartitaIvaTenantDiversi_ConsentitaPerIsolamentoMultiTenant()
    {
        await using var db1 = NuovoContesto();
        var svc1 = new AziendeService(db1);

        var a1 = await svc1.CreaAsync(_tenantA, new AziendaEntity
        {
            RagioneSociale = "Azienda Tenant A",
            PartitaIva = "22222222222",
            Indirizzo = "Via A 1", Cap = "10100", Comune = "Torino", Provincia = "TO"
        });

        await using var db2 = NuovoContesto();
        var svc2 = new AziendeService(db2);

        var a2 = await svc2.CreaAsync(_tenantB, new AziendaEntity
        {
            RagioneSociale = "Azienda Tenant B",
            PartitaIva = "22222222222",
            Indirizzo = "Via B 2", Cap = "10100", Comune = "Torino", Provincia = "TO"
        });

        Assert.NotEqual(a1.TenantId, a2.TenantId);
        Assert.Equal("22222222222", a1.PartitaIva);
        Assert.Equal("22222222222", a2.PartitaIva);
    }

    [Fact]
    public async Task Sintesi_SegnalaDatiMancantiSeSettoreAtecoMatricolaInpsOCcnlAssenti()
    {
        await using var db = NuovoContesto();
        var svc = new AziendeService(db);

        var azienda = await svc.CreaAsync(_tenantA, new AziendaEntity
        {
            RagioneSociale = "Azienda Incompleta Srl",
            PartitaIva = "33333333333",
            Indirizzo = "Via Dante 1", Cap = "50100", Comune = "Firenze", Provincia = "FI"
            // MatricolaInps, CcnlDefault non valorizzati
        });

        var sintesi = await svc.GetSintesiAsync(_tenantA, azienda.Id);
        Assert.NotNull(sintesi);
        Assert.False(sintesi.DatiContributiviCompleti);
        Assert.Contains("settore", sintesi.DatiMancanti);
        Assert.Contains("codiceAteco", sintesi.DatiMancanti);
        Assert.Contains("matricolaInps", sintesi.DatiMancanti);
        Assert.Contains("ccnlDefault", sintesi.DatiMancanti);
    }

    public void Dispose() => _connection.Dispose();
}
