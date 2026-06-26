using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashEVerifica_PasswordCorretta_True()
    {
        string hash = PasswordHasher.Hash("SuperSegreta1");
        Assert.NotEqual("SuperSegreta1", hash);            // non in chiaro
        Assert.True(PasswordHasher.Verifica("SuperSegreta1", hash));
    }

    [Fact]
    public void Verifica_PasswordSbagliata_False()
    {
        string hash = PasswordHasher.Hash("SuperSegreta1");
        Assert.False(PasswordHasher.Verifica("sbagliata", hash));
    }

    [Fact]
    public void Hash_StessaPassword_SaltDiversi_ProduceHashDiversi()
    {
        Assert.NotEqual(PasswordHasher.Hash("uguale1234"), PasswordHasher.Hash("uguale1234"));
    }
}

public sealed class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Registra_CreaTenantEUtenteAdmin()
    {
        await using var db = NuovoContesto();
        var utente = await new AuthService(db).RegistraAsync("Alfa Srl", "Mario@Alfa.it", "password1");

        Assert.NotEqual(Guid.Empty, utente.TenantId);
        Assert.Equal("admin", utente.Ruolo);
        Assert.Equal("mario@alfa.it", utente.Email); // normalizzata
        Assert.Equal(1, await db.Tenants.CountAsync());
    }

    [Fact]
    public async Task Registra_EmailDuplicata_LanciaEccezione()
    {
        await using var db = NuovoContesto();
        var auth = new AuthService(db);
        await auth.RegistraAsync("Alfa", "dup@alfa.it", "password1");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            auth.RegistraAsync("Beta", "dup@alfa.it", "password2"));
    }

    [Fact]
    public async Task Registra_PasswordTroppoCorta_LanciaEccezione()
    {
        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AuthService(db).RegistraAsync("Alfa", "x@alfa.it", "corta"));
    }

    [Fact]
    public async Task Autentica_CredenzialiValide_RestituisceUtente()
    {
        await using var db = NuovoContesto();
        var auth = new AuthService(db);
        await auth.RegistraAsync("Alfa", "login@alfa.it", "password1");

        var utente = await auth.AutenticaAsync("LOGIN@alfa.it", "password1");
        Assert.NotNull(utente);
    }

    [Fact]
    public async Task Autentica_PasswordSbagliata_Null()
    {
        await using var db = NuovoContesto();
        var auth = new AuthService(db);
        await auth.RegistraAsync("Alfa", "login2@alfa.it", "password1");

        Assert.Null(await auth.AutenticaAsync("login2@alfa.it", "sbagliata"));
    }

    public void Dispose() => _connection.Dispose();
}
