using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Sicurezza;
using Xunit;

namespace Paghe.Tests;

public sealed class SessioniRefreshTests : IDisposable
{
    private readonly SqliteConnection _connessione;
    private readonly OrologioRegolabile _orologio = new(
        new DateTimeOffset(2026, 7, 18, 8, 0, 0, TimeSpan.Zero));

    public SessioniRefreshTests()
    {
        _connessione = new SqliteConnection("DataSource=:memory:");
        _connessione.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connessione).Options);

    [Fact]
    public async Task Emissione_SalvaSoloHash_EAccessoValido()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db).RegistraAsync("Alfa", "sessione@alfa.it", "password1");
        var sessioni = new SessioniRefreshService(db, _orologio);

        var emessa = await sessioni.CreaAsync(utente, "Mobile");
        var riga = await db.SessioniRefresh.AsNoTracking().SingleAsync(x => x.FamigliaId == emessa.FamigliaId);

        Assert.DoesNotContain(emessa.RefreshToken, riga.HashSegreto);
        Assert.Equal(43, riga.HashSegreto.Length); // SHA-256 in base64url senza padding
        Assert.True(await sessioni.ValidaAccessoAsync(
            utente.TenantId, utente.Id, emessa.FamigliaId, utente.Ruolo));
    }

    [Fact]
    public async Task Rotazione_E_Monouso_ConCollisioneBreveSenzaRevoca()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db).RegistraAsync("Beta", "ruota@beta.it", "password1");
        var sessioni = new SessioniRefreshService(db, _orologio);
        var iniziale = await sessioni.CreaAsync(utente, "Web");

        var ruotata = await sessioni.RuotaAsync(iniziale.RefreshToken, "Web");
        Assert.Equal(EsitoRotazioneRefresh.Ok, ruotata.Esito);
        Assert.NotNull(ruotata.Sessione);
        Assert.NotEqual(iniziale.RefreshToken, ruotata.Sessione!.RefreshToken);

        var collisione = await sessioni.RuotaAsync(iniziale.RefreshToken, "Web");
        Assert.Equal(EsitoRotazioneRefresh.Collisione, collisione.Esito);
        Assert.True(await sessioni.ValidaAccessoAsync(
            utente.TenantId, utente.Id, iniziale.FamigliaId, utente.Ruolo));

        var prosegue = await sessioni.RuotaAsync(ruotata.Sessione.RefreshToken, "Web");
        Assert.Equal(EsitoRotazioneRefresh.Ok, prosegue.Esito);
    }

    [Fact]
    public async Task ReplayTardivo_RevocaInteraFamiglia()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db).RegistraAsync("Gamma", "replay@gamma.it", "password1");
        var sessioni = new SessioniRefreshService(db, _orologio);
        var iniziale = await sessioni.CreaAsync(utente, "Mobile");
        Assert.Equal(EsitoRotazioneRefresh.Ok,
            (await sessioni.RuotaAsync(iniziale.RefreshToken, "Mobile")).Esito);

        _orologio.Avanti(TimeSpan.FromSeconds(11));
        var replay = await sessioni.RuotaAsync(iniziale.RefreshToken, "Mobile");

        Assert.Equal(EsitoRotazioneRefresh.Replay, replay.Esito);
        Assert.False(await sessioni.ValidaAccessoAsync(
            utente.TenantId, utente.Id, iniziale.FamigliaId, utente.Ruolo));
        Assert.All(await db.SessioniRefresh.AsNoTracking()
                .Where(x => x.FamigliaId == iniziale.FamigliaId).ToListAsync(),
            x => Assert.NotNull(x.RevocataUtc));
    }

    [Fact]
    public async Task Revoca_RichiedeTenantCorretto_EBloccaAccessToken()
    {
        await using var db = NuovoContesto();
        var auth = new AuthService(db);
        var a = await auth.RegistraAsync("A", "a@tenant.it", "password1");
        var b = await auth.RegistraAsync("B", "b@tenant.it", "password1");
        var sessioni = new SessioniRefreshService(db, _orologio);
        var emessa = await sessioni.CreaAsync(a, "Mobile");

        Assert.Equal(0, await sessioni.RevocaFamigliaAsync(b.TenantId, emessa.FamigliaId, "TentativoAltroTenant"));
        Assert.True(await sessioni.ValidaAccessoAsync(a.TenantId, a.Id, emessa.FamigliaId, a.Ruolo));

        Assert.True(await sessioni.RevocaFamigliaAsync(a.TenantId, emessa.FamigliaId, "Logout") > 0);
        Assert.False(await sessioni.ValidaAccessoAsync(a.TenantId, a.Id, emessa.FamigliaId, a.Ruolo));
    }

    [Fact]
    public async Task InattivitaScaduta_RevocaLaFamiglia()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db).RegistraAsync("Delta", "scade@delta.it", "password1");
        var sessioni = new SessioniRefreshService(db, _orologio);
        var emessa = await sessioni.CreaAsync(utente, "Web");

        _orologio.Avanti(TimeSpan.FromDays(8));
        Assert.Equal(EsitoRotazioneRefresh.Scaduto,
            (await sessioni.RuotaAsync(emessa.RefreshToken, "Web")).Esito);
        Assert.False(await sessioni.ValidaAccessoAsync(
            utente.TenantId, utente.Id, emessa.FamigliaId, utente.Ruolo));
    }

    public void Dispose() => _connessione.Dispose();

    private sealed class OrologioRegolabile(DateTimeOffset iniziale) : TimeProvider
    {
        private DateTimeOffset _ora = iniziale;
        public override DateTimeOffset GetUtcNow() => _ora;
        public void Avanti(TimeSpan durata) => _ora += durata;
    }
}
