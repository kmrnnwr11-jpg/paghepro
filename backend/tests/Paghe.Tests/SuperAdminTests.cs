using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del super-admin di piattaforma. Sicurezza (Fase 1): la registrazione NON concede più il
/// ruolo superadmin — nemmeno all'email un tempo configurata (era registrabile pubblicamente).
/// L'unico canale è il bootstrap con token segreto, a uso singolo. Gli studi sospesi non accedono
/// (il super-admin sì); elenco/sospensione studi.
/// </summary>
public sealed class SuperAdminTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SuperAdminTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private static IConfiguration Config(string? superEmail) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SuperAdmin:Email"] = superEmail })
            .Build();

    private static IConfiguration ConfigBootstrap(string? token) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SuperAdmin:BootstrapToken"] = token })
            .Build();

    private const string TokenValido = "token-super-lungo-di-piattaforma-0123456789";

    [Fact]
    public async Task Registra_EmailUnTempoSuperAdmin_OraNonConcedeSuperadmin()
    {
        // Sicurezza F1: anche registrandosi con l'email in SuperAdmin:Email il ruolo resta admin.
        await using var db = NuovoContesto();
        var utente = await new AuthService(db, Config("boss@studio.it"))
            .RegistraAsync("Studio Boss", "BOSS@studio.it", "password1");
        Assert.Equal("admin", utente.Ruolo);
    }

    [Fact]
    public async Task Google_EmailUnTempoSuperAdmin_OraNonConcedeSuperadmin()
    {
        await using var db = NuovoContesto();
        var u = await new AuthService(db, Config("boss@z.it")).AccediConGoogleAsync("BOSS@z.it", "Capo");
        Assert.Equal("admin", u.Ruolo);
    }

    [Fact]
    public async Task Registra_EmailNormale_AssegnaRuoloAdmin()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db, Config("boss@studio.it"))
            .RegistraAsync("Studio Pippo", "pippo@x.it", "password1");
        Assert.Equal("admin", utente.Ruolo);
    }

    [Fact]
    public async Task Bootstrap_TokenValido_PromuoveUnaVoltaSola()
    {
        Guid utenteId;
        await using (var db = NuovoContesto())
            utenteId = (await new AuthService(db).RegistraAsync("Studio", "a@a.it", "password1")).Id;

        // Token corretto: promozione riuscita.
        await using (var db = NuovoContesto())
            Assert.True(await new AuthService(db, ConfigBootstrap(TokenValido))
                .BootstrapSuperAdminAsync(utenteId, TokenValido));

        await using (var db = NuovoContesto())
            Assert.Equal(AuthService.RuoloSuperAdmin,
                (await db.Utenti.FindAsync(utenteId))!.Ruolo);

        // Uso singolo: un secondo utente non può più bootstrap-arsi (esiste già un super-admin).
        Guid altro;
        await using (var db = NuovoContesto())
            altro = (await new AuthService(db).RegistraAsync("Studio2", "b@b.it", "password1")).Id;
        await using (var db = NuovoContesto())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new AuthService(db, ConfigBootstrap(TokenValido)).BootstrapSuperAdminAsync(altro, TokenValido));
    }

    [Fact]
    public async Task Bootstrap_TokenErrato_NonPromuove()
    {
        Guid utenteId;
        await using (var db = NuovoContesto())
            utenteId = (await new AuthService(db).RegistraAsync("Studio", "a@a.it", "password1")).Id;

        await using var db2 = NuovoContesto();
        Assert.False(await new AuthService(db2, ConfigBootstrap(TokenValido))
            .BootstrapSuperAdminAsync(utenteId, "token-sbagliato"));
        Assert.Equal("admin", (await db2.Utenti.FindAsync(utenteId))!.Ruolo);
    }

    [Fact]
    public async Task Bootstrap_TokenNonConfigurato_Errore()
    {
        Guid utenteId;
        await using (var db = NuovoContesto())
            utenteId = (await new AuthService(db).RegistraAsync("Studio", "a@a.it", "password1")).Id;

        await using var db2 = NuovoContesto();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AuthService(db2, ConfigBootstrap(null)).BootstrapSuperAdminAsync(utenteId, "qualsiasi"));
    }

    [Fact]
    public async Task Login_StudioSospeso_Bloccato_PoiRiattivato_Ok()
    {
        Guid tenantId;
        await using (var db = NuovoContesto())
            tenantId = (await new AuthService(db).RegistraAsync("Studio Y", "y@y.it", "password1")).TenantId;

        await using (var db = NuovoContesto())
            Assert.True(await new PiattaformaAdminService(db).ImpostaSospensioneAsync(tenantId, true));

        await using (var db = NuovoContesto())
            Assert.Equal(EsitoAccesso.AccountSospeso,
                (await new AuthService(db).AccediAsync("y@y.it", "password1")).Esito);

        await using (var db = NuovoContesto())
            await new PiattaformaAdminService(db).ImpostaSospensioneAsync(tenantId, false);

        await using (var db = NuovoContesto())
            Assert.Equal(EsitoAccesso.Ok, (await new AuthService(db).AccediAsync("y@y.it", "password1")).Esito);
    }

    [Fact]
    public async Task SuperAdmin_NonBloccato_AncheConTenantSospeso()
    {
        Guid tenantId, utenteId;
        await using (var db = NuovoContesto())
        {
            var u = await new AuthService(db).RegistraAsync("Studio Boss", "boss@z.it", "password1");
            tenantId = u.TenantId; utenteId = u.Id;
        }

        // Promozione a super-admin tramite il canale sicuro (bootstrap), non più via email.
        await using (var db = NuovoContesto())
            await new AuthService(db, ConfigBootstrap(TokenValido)).BootstrapSuperAdminAsync(utenteId, TokenValido);

        await using (var db = NuovoContesto())
            await new PiattaformaAdminService(db).ImpostaSospensioneAsync(tenantId, true);

        await using (var db = NuovoContesto())
            Assert.Equal(EsitoAccesso.Ok, (await new AuthService(db).AccediAsync("boss@z.it", "password1")).Esito);
    }

    [Fact]
    public async Task ElencaStudi_ConteggiUtenti()
    {
        await using (var db = NuovoContesto())
        {
            await new AuthService(db).RegistraAsync("Studio A", "a@a.it", "password1");
            await new AuthService(db).RegistraAsync("Studio B", "b@b.it", "password1");
        }

        await using (var db = NuovoContesto())
        {
            var studi = await new PiattaformaAdminService(db).ElencaStudiAsync();
            Assert.Equal(2, studi.Count);
            Assert.All(studi, s => Assert.Equal(1, s.NumeroUtenti));
            Assert.All(studi, s => Assert.False(s.Sospeso));
        }
    }

    [Fact]
    public async Task Google_NuovaEmail_CreaUtenteSenzaPassword()
    {
        await using var db = NuovoContesto();
        var u = await new AuthService(db, Config("boss@z.it"))
            .AccediConGoogleAsync("mario@gmail.com", "Mario Rossi");

        Assert.Equal("mario@gmail.com", u.Email);
        Assert.Equal("admin", u.Ruolo);
        Assert.Equal("", u.PasswordHash); // accesso solo via Google
        Assert.Equal(1, await db.Utenti.CountAsync());
    }

    [Fact]
    public async Task Google_EmailEsistente_RestituisceLoStessoUtente()
    {
        Guid id1;
        await using (var db = NuovoContesto())
            id1 = (await new AuthService(db).AccediConGoogleAsync("mario@gmail.com", "Mario")).Id;

        await using (var db = NuovoContesto())
        {
            var u2 = await new AuthService(db).AccediConGoogleAsync("mario@gmail.com", "Mario");
            Assert.Equal(id1, u2.Id); // non duplica
            Assert.Equal(1, await db.Utenti.CountAsync());
        }
    }

    public void Dispose() => _connection.Dispose();
}
