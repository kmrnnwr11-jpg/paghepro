using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test GDPR: cifratura applicativa versionata. Verifica round-trip, randomizzazione, compatibilità
/// col testo storico, fallimento chiuso e persistenza cifrata via converter EF.
/// </summary>
public sealed class GdprProtezioneDatiTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GdprProtezioneDatiTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    // --- Unit: ProtettoreDati ---

    [Fact]
    public void Proteggi_PoiSvela_RestituisceOriginale()
    {
        var p = new ProtettoreDati("chiave-di-test", produzione: false);
        const string iban = "IT60X0542811101000000123456";

        var cifrato = p.Proteggi(iban);

        Assert.NotNull(cifrato);
        Assert.StartsWith(p.PrefissoChiaveCorrente, cifrato);
        Assert.NotEqual(iban, cifrato);
        Assert.Equal(iban, p.Svela(cifrato));
    }

    [Fact]
    public void Proteggi_DueVolte_ProduceCifratiDiversiMaStessoChiaro()
    {
        var p = new ProtettoreDati("chiave-di-test", produzione: false);
        const string iban = "IT60X0542811101000000123456";

        var a = p.Proteggi(iban);
        var b = p.Proteggi(iban);

        Assert.NotEqual(a, b);          // nonce casuale → cifrati diversi
        Assert.Equal(iban, p.Svela(a)); // ma entrambi decifrano allo stesso valore
        Assert.Equal(iban, p.Svela(b));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Proteggi_NullOEmpty_Invariato(string? valore)
    {
        var p = new ProtettoreDati("chiave-di-test", produzione: false);
        Assert.Equal(valore, p.Proteggi(valore));
        Assert.Equal(valore, p.Svela(valore));
    }

    [Fact]
    public void Svela_DatoStoricoInChiaro_RestituitoInvariato()
    {
        var p = new ProtettoreDati("chiave-di-test", produzione: false);
        const string ibanInChiaro = "IT60X0542811101000000123456"; // senza prefisso enc:v1:
        Assert.Equal(ibanInChiaro, p.Svela(ibanInChiaro));
    }

    [Fact]
    public void Svela_ConChiaveErrata_Lancia()
    {
        var cifrato = new ProtettoreDati("chiave-giusta", produzione: false)
            .Proteggi("IT60X0542811101000000123456");
        var altro = new ProtettoreDati("chiave-sbagliata", produzione: false);
        Assert.Throws<InvalidOperationException>(() => altro.Svela(cifrato));
    }

    [Theory]
    [InlineData("enc:v2:chiave:non-base64")]
    [InlineData("enc:v2::YWJj")]
    [InlineData("enc:v9:qualcosa")]
    public void Svela_CifratoCorrottoOSconosciuto_FallisceChiuso(string valore)
    {
        var p = new ProtettoreDati("chiave-di-test", produzione: false);
        Assert.Throws<InvalidOperationException>(() => p.Svela(valore));
    }

    [Fact]
    public void Rotazione_ChiavePrecedente_DecifraERicifraConLaCorrente()
    {
        const string chiaveVecchia = "chiave-vecchia-da-ruotare";
        const string chiaveNuova = "chiave-nuova-corrente";
        const string valore = "IT60X0542811101000000123456";
        var vecchio = new ProtettoreDati(chiaveVecchia, produzione: false);
        var portachiavi = new ProtettoreDati(chiaveNuova, [chiaveVecchia], produzione: false);

        var cifratoVecchio = vecchio.Proteggi(valore)!;
        var ricifrato = portachiavi.Ricifra(cifratoVecchio)!;

        Assert.Equal(valore, portachiavi.Svela(cifratoVecchio));
        Assert.StartsWith(portachiavi.PrefissoChiaveCorrente, ricifrato);
        Assert.Equal(valore, portachiavi.Svela(ricifrato));
        Assert.False(portachiavi.RichiedeRicifratura(ricifrato));
    }

    [Fact]
    public void RotazioneContestuale_MantieneAad_EContestoDiversoFallisce()
    {
        const string vecchia = "chiave-contestuale-vecchia";
        const string nuova = "chiave-contestuale-nuova";
        const string contesto = "tenant-a|tabella|record-a|campo";
        var protettoreVecchio = new ProtettoreDati(vecchia, produzione: false);
        var portachiavi = new ProtettoreDati(nuova, [vecchia], produzione: false);
        var cifratoVecchio = protettoreVecchio.ProteggiConContesto("segreto", contesto)!;

        var ruotato = portachiavi.RicifraConContesto(cifratoVecchio, contesto)!;

        Assert.StartsWith(portachiavi.PrefissoChiaveCorrente, ruotato);
        Assert.Equal("segreto", portachiavi.SvelaConContesto(ruotato, contesto));
        Assert.Throws<InvalidOperationException>(
            () => portachiavi.SvelaConContesto(ruotato, "tenant-b|tabella|record-a|campo"));
    }

    [Fact]
    public void SvelaConContesto_PostCutover_RifiutaPlaintextEV1()
    {
        const string passphrase = "chiave-strict-post-cutover";
        const string contesto = "tenant|tabella|record|campo";
        var p = new ProtettoreDati(passphrase, produzione: false);
        var v1 = CifraV1(passphrase, "segreto");

        Assert.Throws<InvalidOperationException>(() =>
            p.SvelaConContesto("segreto-in-chiaro", contesto));
        Assert.Throws<InvalidOperationException>(() =>
            p.SvelaConContesto(v1, contesto));
    }

    [Fact]
    public void RotazioneContestualeProduzione_UsaChiaviRawK1K2()
    {
        var k1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var k2 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string contesto = "paghe-gdpr:v1|tenant|tabella|record|campo";
        var vecchio = new ProtettoreDati(k1, produzione: true);
        var portachiavi = new ProtettoreDati(k2, [k1], produzione: true);

        var cifratoK1 = vecchio.ProteggiConContesto("dato-produzione", contesto)!;
        var cifratoK2 = portachiavi.RicifraConContesto(cifratoK1, contesto)!;

        Assert.Equal("dato-produzione", portachiavi.SvelaConContesto(cifratoK1, contesto));
        Assert.StartsWith(portachiavi.PrefissoChiaveCorrente, cifratoK2);
        Assert.Equal("dato-produzione", portachiavi.SvelaConContesto(cifratoK2, contesto));
    }

    // --- Integrazione: IBAN cifrato nella colonna del database ---

    [Fact]
    public async Task IbanDipendente_CifratoNelDb_MaRilettoInChiaro()
    {
        const string iban = "IT60X0542811101000000123456";
        var tenant = Guid.NewGuid();
        var dipId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = dipId,
                TenantId = tenant,
                CodiceFiscale = "RSSMRA80A01F205X",
                Nome = "Mario",
                Cognome = "Rossi",
                Iban = iban,
            });
            await db.SaveChangesAsync();
        }

        // Lettura grezza della colonna: NON deve contenere l'IBAN in chiaro.
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT IbanProtetto FROM Dipendenti WHERE TenantId = $tenant AND CodiceFiscale = $cf";
            cmd.Parameters.AddWithValue("$tenant", tenant);
            cmd.Parameters.AddWithValue("$cf", "RSSMRA80A01F205X");
            var grezzo = (string?)await cmd.ExecuteScalarAsync();
            Assert.NotNull(grezzo);
            Assert.StartsWith(ProtettoreDati.Corrente.PrefissoChiaveCorrente, grezzo);
            Assert.DoesNotContain(iban, grezzo);
        }

        // Lettura via EF: l'IBAN torna in chiaro.
        await using (var db = NuovoContesto())
        {
            var dip = await db.Dipendenti.FirstAsync(d => d.Id == dipId);
            Assert.Equal(iban, dip.Iban);
        }
    }

    public void Dispose() => _connection.Dispose();

    private static string CifraV1(string passphrase, string valore)
    {
        var chiave = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var chiaro = Encoding.UTF8.GetBytes(valore);
        var cifrato = new byte[chiaro.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(chiave, 16);
        aes.Encrypt(nonce, chiaro, cifrato, tag);
        return "enc:v1:" + Convert.ToBase64String([.. nonce, .. tag, .. cifrato]);
    }
}
