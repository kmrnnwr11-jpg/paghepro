using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Fatturazione;
using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

public sealed class TrasmissioneSdiTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TrasmissioneSdiTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private TrasmissioneFattureService Servizio(PagheDbContext db, RisultatoTrasmissioneSdi risultato, EsitoSdi? esito = null) =>
        new(db, new FirmaDelegataAlProvider(), new FakeGateway(risultato, esito));

    [Fact]
    public async Task Trasmetti_PersisteStatoEIdentificativoSdi()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId);

        await using var db = NuovoContesto();
        var t = await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-123"))
            .TrasmettiAsync(tenant, fatturaId);

        Assert.Equal(nameof(StatoTrasmissioneSdi.Inviata), t.Stato);
        Assert.Equal("SDI-123", t.IdentificativoSdi);
        Assert.False(t.Firmato);
        Assert.EndsWith(".xml", t.NomeFileXml);
        Assert.NotNull(t.InviataIl);
    }

    [Fact]
    public async Task Trasmetti_FatturaNonConforme_Blocca()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        // Cessionario senza Partita IVA né Codice Fiscale -> controllo SdI 00417.
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId, clienteConIdentificativo: false);

        await using var db = NuovoContesto();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "X")).TrasmettiAsync(tenant, fatturaId));
        Assert.Contains("00417", ex.Message);
    }

    [Fact]
    public async Task Trasmetti_DueVolte_Blocca()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId);

        await using (var db = NuovoContesto())
            await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-1")).TrasmettiAsync(tenant, fatturaId);

        await using (var db = NuovoContesto())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-2")).TrasmettiAsync(tenant, fatturaId));
    }

    [Fact]
    public async Task RegistraRicevuta_RC_PortaAConsegnata()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId);

        Guid trasmissioneId;
        await using (var db = NuovoContesto())
            trasmissioneId = (await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-1"))
                .TrasmettiAsync(tenant, fatturaId)).Id;

        await using (var db = NuovoContesto())
        {
            var t = await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata))
                .RegistraRicevutaAsync(tenant, trasmissioneId, new RicevutaSdiInput("RC", Descrizione: "Consegnata"));
            Assert.Equal(nameof(StatoTrasmissioneSdi.ConsegnataSdi), t.Stato);
            Assert.Single(t.Ricevute);
        }
    }

    [Fact]
    public async Task RegistraRicevuta_NS_PortaAScartata()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId);

        Guid trasmissioneId;
        await using (var db = NuovoContesto())
            trasmissioneId = (await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-1"))
                .TrasmettiAsync(tenant, fatturaId)).Id;

        await using (var db = NuovoContesto())
        {
            var t = await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata))
                .RegistraRicevutaAsync(tenant, trasmissioneId, new RicevutaSdiInput("NS", "00400"));
            Assert.Equal(nameof(StatoTrasmissioneSdi.ScartataSdi), t.Stato);
        }
    }

    [Fact]
    public async Task AggiornaEsito_LeggeStatoDalGateway()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId);

        Guid trasmissioneId;
        await using (var db = NuovoContesto())
            trasmissioneId = (await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-9"))
                .TrasmettiAsync(tenant, fatturaId)).Id;

        await using (var db = NuovoContesto())
        {
            var t = await Servizio(
                    db,
                    new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata),
                    new EsitoSdi(StatoTrasmissioneSdi.ConsegnataSdi, "SDI-9"))
                .AggiornaEsitoAsync(tenant, trasmissioneId);
            Assert.Equal(nameof(StatoTrasmissioneSdi.ConsegnataSdi), t.Stato);
        }
    }

    [Fact]
    public async Task Trasmissioni_SonoIsolatePerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenantA);
        var fatturaId = await CreaFatturaAsync(tenantA, aziendaId);

        Guid trasmissioneId;
        await using (var db = NuovoContesto())
            trasmissioneId = (await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-1"))
                .TrasmettiAsync(tenantA, fatturaId)).Id;

        await using (var db = NuovoContesto())
        {
            var svc = Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata));
            Assert.NotNull(await svc.GetAsync(tenantA, trasmissioneId));
            Assert.Null(await svc.GetAsync(tenantB, trasmissioneId));
        }
    }

    [Fact]
    public async Task Elenco_e_GetPerFattura_OrdinanoSenzaErrori()
    {
        // Regressione: l'ordinamento per timestamp deve funzionare sul provider SQLite
        // (DateTimeOffset non è ordinabile in ORDER BY su SQLite, DateTime sì).
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        var fatturaId = await CreaFatturaAsync(tenant, aziendaId);

        await using (var db = NuovoContesto())
            await Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata, "SDI-1"))
                .TrasmettiAsync(tenant, fatturaId);

        await using (var db = NuovoContesto())
        {
            var svc = Servizio(db, new RisultatoTrasmissioneSdi(StatoTrasmissioneSdi.Inviata));
            var elenco = await svc.ElencoAsync(tenant);
            Assert.Single(elenco);
            Assert.NotNull(await svc.GetPerFatturaAsync(tenant, fatturaId));
        }
    }

    private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl",
            PartitaIva = "01234567897",
            Indirizzo = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
        });
        return azienda.Id;
    }

    private async Task<Guid> CreaFatturaAsync(Guid tenant, Guid aziendaId, bool clienteConIdentificativo = true)
    {
        await using var db = NuovoContesto();
        var cliente = new DatiAnagrafici
        {
            Denominazione = "Beta Spa",
            PartitaIva = clienteConIdentificativo ? "09876543217" : null,
            Indirizzo = "Via Po 2",
            Cap = "10100",
            Comune = "Torino",
            Provincia = "TO",
        };
        var f = await new FattureService(db).SalvaAsync(tenant, aziendaId, new FatturaInput(
            Data: new DateOnly(2026, 7, 10),
            Cessionario: cliente,
            Righe: new[] { new RigaFattura { Descrizione = "Servizi", PrezzoUnitario = 100m, AliquotaIva = 22m } }));
        return f.Id;
    }

    private sealed class FakeGateway : IFatturaSdiGateway
    {
        private readonly RisultatoTrasmissioneSdi _risultato;
        private readonly EsitoSdi? _esito;

        public FakeGateway(RisultatoTrasmissioneSdi risultato, EsitoSdi? esito)
        {
            _risultato = risultato;
            _esito = esito;
        }

        public string Nome => "Fake";
        public bool RichiedeFirmaLocale => false;
        public Task<RisultatoTrasmissioneSdi> TrasmettiAsync(FatturaDaTrasmettere fattura, CancellationToken ct = default) =>
            Task.FromResult(_risultato);
        public Task<EsitoSdi?> InterrogaEsitoAsync(string identificativoSdi, CancellationToken ct = default) =>
            Task.FromResult(_esito);
    }

    public void Dispose() => _connection.Dispose();
}
