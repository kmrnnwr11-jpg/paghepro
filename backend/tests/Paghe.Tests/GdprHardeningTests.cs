using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Gdpr;
using Paghe.Persistence.Sicurezza;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Irrigidimenti GDPR/sicurezza della Fase 1: (1) la chiave di cifratura è obbligatoria in
/// produzione (fail-fast sulla passphrase di sviluppo) e (2) reiniziare l'enrollment 2FA non
/// disattiva un secondo fattore già attivo.
/// </summary>
public sealed class GdprHardeningTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GdprHardeningTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    // --- Fix #1: chiave di cifratura obbligatoria in produzione ---

    [Fact]
    public void Cifratura_InProduzione_ConPassphraseSviluppo_Lancia()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ProtettoreDati(ProtettoreDati.PassphraseSviluppo, produzione: true));
    }

    [Fact]
    public void Cifratura_InProduzione_ConChiaveReale_Funziona()
    {
        var p = new ProtettoreDati("chiave-di-produzione-robusta-da-secret", produzione: true);
        var cifrato = p.Proteggi("IT60X0542811101000000123456");
        Assert.StartsWith("enc:v1:", cifrato);
        Assert.Equal("IT60X0542811101000000123456", p.Svela(cifrato));
    }

    [Fact]
    public void Cifratura_InSviluppo_ConPassphraseSviluppo_Consentita()
    {
        var p = new ProtettoreDati(ProtettoreDati.PassphraseSviluppo, produzione: false);
        Assert.Equal("ciao", p.Svela(p.Proteggi("ciao")));
    }

    // --- Fix #3: re-enrollment 2FA non disattiva un 2FA già attivo ---

    [Fact]
    public async Task Inizia2FA_QuandoGiaAttivo_RifiutaELasciaIl2FaAttivo()
    {
        Guid utenteId;
        string segreto;

        await using (var db = NuovoContesto())
        {
            var auth = new AuthService(db);
            var u = await auth.RegistraAsync("Alfa Srl", "hard@alfa.it", "password1");
            utenteId = u.Id;
            segreto = (await auth.IniziaDueFattoriAsync(utenteId)).Segreto;
            Assert.True(await auth.AttivaDueFattoriAsync(utenteId, Totp.GeneraCodice(segreto)));
        }

        await using (var db = NuovoContesto())
        {
            // Reiniziare l'enrollment mentre il 2FA è attivo deve essere rifiutato (no "strip").
            var auth = new AuthService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => auth.IniziaDueFattoriAsync(utenteId));
        }

        await using (var db = NuovoContesto())
        {
            // Il 2FA esistente resta attivo e verificabile con il segreto originale.
            var auth = new AuthService(db);
            Assert.Equal(EsitoAccesso.ServeCodice2fa,
                (await auth.AccediAsync("hard@alfa.it", "password1")).Esito);
            Assert.Equal(EsitoAccesso.Ok,
                (await auth.AccediAsync("hard@alfa.it", "password1", Totp.GeneraCodice(segreto))).Esito);
        }
    }

    public void Dispose() => _connection.Dispose();
}
