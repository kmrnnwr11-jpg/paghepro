using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Contabilita;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica dell'isolamento multi-tenant sui dati principali: un tenant "intruso" che conosce
/// gli ID di un altro tenant NON deve poter leggere né modificare cedolini, fatture, bilanci,
/// pratiche F24 e Certificazioni Uniche altrui. Stesso database, tenant diversi.
/// </summary>
public sealed class IsolamentoTenantTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _vittima = Guid.NewGuid();
    private readonly Guid _intruso = Guid.NewGuid();

    public IsolamentoTenantTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            Settore = "COMMERCIO_SERVIZI", CodiceAteco = "47.91.10",
            MatricolaInps = "1234567890", CodiceSedeInps = "7100", CscInps = "10101",
            PatInail = "12345678/90", CcnlDefault = "COMMERCIO_TERZIARIO", LivelloDefault = "3",
        });
        return az.Id;
    }

    private async Task<Guid> CreaRapportoAsync(Guid tenant, Guid aziendaId)
    {
        await using var db = NuovoContesto();
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, aziendaId,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });
        return rapporto.Id;
    }

    private async Task<(Guid rapportoId, Guid cedolinoId)> CreaCedolinoVittimaAsync()
    {
        var aziendaId = await CreaAziendaAsync(_vittima);
        var rapportoId = await CreaRapportoAsync(_vittima, aziendaId);
        await using var db = NuovoContesto();
        var c = await new CedoliniService(db, new ParametriNormativiService(db))
            .CalcolaEArchiviaAsync(_vittima, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
        return (rapportoId, c.Id);
    }

    [Fact]
    public async Task Cedolino_AltroTenant_NonLeggibile()
    {
        var (rapportoId, cedolinoId) = await CreaCedolinoVittimaAsync();

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        Assert.Null(await svc.GetAsync(_intruso, cedolinoId));
        Assert.Empty(await svc.ElencoPerRapportoAsync(_intruso, rapportoId));
    }

    [Fact]
    public async Task Fattura_AltroTenant_NonLeggibile()
    {
        var aziendaId = await CreaAziendaAsync(_vittima);
        Guid fatturaId;
        await using (var db = NuovoContesto())
        {
            var f = await new FattureService(db).SalvaAsync(_vittima, aziendaId, new FatturaInput(
                Data: new DateOnly(2026, 6, 26),
                Cessionario: new DatiAnagrafici
                {
                    Denominazione = "Beta Spa", PartitaIva = "09876543210",
                    Indirizzo = "Via Po 2", Cap = "10100", Comune = "Torino", Provincia = "TO",
                },
                Righe: new[]
                {
                    new RigaFattura { Descrizione = "Consulenza", PrezzoUnitario = 100m, AliquotaIva = 22m },
                }));
            fatturaId = f.Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new FattureService(db);
            Assert.Null(await svc.GetAsync(_intruso, fatturaId));
            Assert.Empty(await svc.ElencoAsync(_intruso, aziendaId));
        }
    }

    [Fact]
    public async Task Bilancio_AltroTenant_NonLeggibileNeModificabile()
    {
        var aziendaId = await CreaAziendaAsync(_vittima);
        Guid bilancioId;
        await using (var db = NuovoContesto())
        {
            var b = await new BilancioService(db).CreaDaEsempioAsync(_vittima, aziendaId, 2026);
            bilancioId = b.Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new BilancioService(db);
            Assert.Null(await svc.GetAsync(_intruso, bilancioId));
            Assert.Empty(await svc.ElencoAsync(_intruso, aziendaId));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.CambiaStatoAsync(_intruso, bilancioId, StatoBilancio.Approvato));
        }
    }

    [Fact]
    public async Task PraticaF24_AltroTenant_NonLeggibileNeModificabile()
    {
        var (_, _) = await CreaCedolinoVittimaAsync();
        Guid aziendaId;
        Guid praticaId;
        await using (var db = NuovoContesto())
        {
            aziendaId = (await db.Aziende.SingleAsync(a => a.TenantId == _vittima)).Id;
            var svc = new PraticheF24Service(db, new CedoliniService(db, new ParametriNormativiService(db)));
            var pratica = await svc.GeneraEArchiviaDelMeseAsync(_vittima, aziendaId, 2026, 6, "1234567890");
            praticaId = pratica.Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new PraticheF24Service(db, new CedoliniService(db, new ParametriNormativiService(db)));
            Assert.Null(await svc.GetAsync(_intruso, praticaId));
            Assert.Empty(await svc.ElencoAsync(_intruso));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.CambiaStatoAsync(_intruso, praticaId, StatoPraticaTelematica.InviataManualmente));
        }
    }

    [Fact]
    public async Task CertificazioneUnica_AltroTenant_NonCostruibile()
    {
        var (rapportoId, _) = await CreaCedolinoVittimaAsync();
        Guid aziendaId;
        await using (var db = NuovoContesto())
            aziendaId = (await db.Aziende.SingleAsync(a => a.TenantId == _vittima)).Id;

        await using (var db2 = NuovoContesto())
        {
            var svc = new CertificazioneUnicaService(db2);
            Assert.Null(await svc.CostruisciAsync(_intruso, rapportoId, 2026));
            Assert.Empty(await svc.CostruisciPerAziendaAsync(_intruso, aziendaId, 2026));
        }
    }

    public void Dispose() => _connection.Dispose();
}
