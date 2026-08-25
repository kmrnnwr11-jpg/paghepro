using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Gdpr;
using Paghe.Persistence.Sicurezza;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test del core TOTP (RFC 6238) e del flusso di login a due fattori in <see cref="AuthService"/>.</summary>
public sealed class DueFattoriTests : IDisposable
{
    // Segreto del vettore ufficiale RFC 6238 (ASCII "12345678903234567897" in Base32).
    private const string SegretoRfc = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private readonly SqliteConnection _connection;

    public DueFattoriTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    // --- Core TOTP ---

    [Fact]
    public void GeneraCodice_VettoreUfficialeRfc6238()
    {
        // RFC 6238 Appendix B: a T=59s (SHA1) il TOTP a 8 cifre è 94287082 → a 6 cifre 287082.
        var codice = Totp.GeneraCodice(SegretoRfc, DateTimeOffset.FromUnixTimeSeconds(59));
        Assert.Equal("287082", codice);
    }

    [Fact]
    public void Verifica_CodiceCorretto_True_Sbagliato_False()
    {
        var istante = DateTimeOffset.FromUnixTimeSeconds(59);
        var codice = Totp.GeneraCodice(SegretoRfc, istante);
        Assert.True(Totp.Verifica(SegretoRfc, codice, finestra: 1, istante: istante));
        Assert.False(Totp.Verifica(SegretoRfc, "000000", finestra: 1, istante: istante));
    }

    [Fact]
    public void Verifica_TolleraScartoOrologio_NellaFinestra()
    {
        var codice = Totp.GeneraCodice(SegretoRfc, DateTimeOffset.FromUnixTimeSeconds(59)); // contatore 1
        // 30s dopo (contatore 2): con finestra ±1 il codice del passo precedente è ancora valido.
        Assert.True(Totp.Verifica(SegretoRfc, codice, finestra: 1,
            istante: DateTimeOffset.FromUnixTimeSeconds(75)));
    }

    [Fact]
    public void GeneraSegreto_ProduceBase32Utilizzabile()
    {
        var segreto = Totp.GeneraSegreto();
        Assert.False(string.IsNullOrWhiteSpace(segreto));
        Assert.Equal(6, Totp.GeneraCodice(segreto).Length); // round-trip senza eccezioni
    }

    // --- Flusso 2FA su AuthService ---

    [Fact]
    public async Task Login_DopoAttivazione2FA_RichiedeEVerificaIlCodice()
    {
        Guid utenteId;
        string segreto;

        await using (var db = NuovoContesto())
        {
            var auth = new AuthService(db);
            var utente = await auth.RegistraAsync("Alfa Srl", "due@alfa.it", "password1");
            utenteId = utente.Id;

            // Prima di attivare: login con sola password funziona.
            Assert.Equal(EsitoAccesso.Ok, (await auth.AccediAsync("due@alfa.it", "password1")).Esito);

            var conf = await auth.IniziaDueFattoriAsync(utenteId);
            segreto = conf.Segreto;
            Assert.StartsWith("otpauth://totp/", conf.UriOtpauth);

            // Codice sbagliato non attiva; codice giusto sì.
            Assert.False(await auth.AttivaDueFattoriAsync(utenteId, "000000"));
            Assert.True(await auth.AttivaDueFattoriAsync(utenteId, Totp.GeneraCodice(segreto)));
        }

        await using (var db = NuovoContesto())
        {
            var auth = new AuthService(db);

            // Ora la sola password non basta.
            var serveSecondoFattore = await auth.AccediAsync("due@alfa.it", "password1");
            Assert.Equal(EsitoAccesso.ServeCodice2fa, serveSecondoFattore.Esito);
            Assert.Null(serveSecondoFattore.Utente);
            Assert.NotNull(serveSecondoFattore.IdentitaAudit);

            var secondoFattoreErrato = await auth.AccediAsync("due@alfa.it", "password1", "000000");
            Assert.Equal(EsitoAccesso.Codice2faErrato, secondoFattoreErrato.Esito);
            Assert.Null(secondoFattoreErrato.Utente);
            Assert.NotNull(secondoFattoreErrato.IdentitaAudit);

            var ok = await auth.AccediAsync("due@alfa.it", "password1", Totp.GeneraCodice(segreto));
            Assert.Equal(EsitoAccesso.Ok, ok.Esito);
            Assert.NotNull(ok.Utente);

            // Password sbagliata resta credenziali errate anche col codice giusto.
            var passwordErrata = await auth.AccediAsync(
                "due@alfa.it", "sbagliata", Totp.GeneraCodice(segreto));
            Assert.Equal(EsitoAccesso.CredenzialiErrate, passwordErrata.Esito);
            Assert.Null(passwordErrata.Utente);
            Assert.NotNull(passwordErrata.IdentitaAudit);
        }
    }

    [Fact]
    public async Task SegretoTotp_CifratoNelDatabase()
    {
        string segreto;
        await using (var db = NuovoContesto())
        {
            var auth = new AuthService(db);
            var utente = await auth.RegistraAsync("Beta Srl", "cifra@beta.it", "password1");
            segreto = (await auth.IniziaDueFattoriAsync(utente.Id)).Segreto;
        }

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT SegretoTotp FROM Utenti WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", "cifra@beta.it");
        var grezzo = (string?)await cmd.ExecuteScalarAsync();
        Assert.NotNull(grezzo);
        Assert.StartsWith(ProtettoreDati.Corrente.PrefissoChiaveCorrente, grezzo); // cifrato a riposo
        Assert.DoesNotContain(segreto, grezzo);   // il segreto in chiaro non compare
    }

    public void Dispose() => _connection.Dispose();
}
