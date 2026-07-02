using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del super-admin di piattaforma: l'email configurata riceve il ruolo superadmin alla
/// registrazione; gli studi sospesi non possono accedere (ma il super-admin sì); elenco/sospensione studi.
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

    [Fact]
    public async Task Registra_EmailSuperAdmin_AssegnaRuoloSuperadmin()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db, Config("boss@studio.it"))
            .RegistraAsync("Studio Boss", "BOSS@studio.it", "password1"); // email con maiuscole: normalizzata
        Assert.Equal(AuthService.RuoloSuperAdmin, utente.Ruolo);
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
        var cfg = Config("boss@z.it");
        Guid tenantId;
        await using (var db = NuovoContesto())
            tenantId = (await new AuthService(db, cfg).RegistraAsync("Studio Boss", "boss@z.it", "password1")).TenantId;

        await using (var db = NuovoContesto())
            await new PiattaformaAdminService(db).ImpostaSospensioneAsync(tenantId, true);

        await using (var db = NuovoContesto())
            Assert.Equal(EsitoAccesso.Ok, (await new AuthService(db, cfg).AccediAsync("boss@z.it", "password1")).Esito);
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
    public async Task Google_EmailSuperAdmin_RuoloSuperadmin()
    {
        await using var db = NuovoContesto();
        var u = await new AuthService(db, Config("boss@z.it"))
            .AccediConGoogleAsync("BOSS@z.it", "Capo");
        Assert.Equal(AuthService.RuoloSuperAdmin, u.Ruolo);
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
