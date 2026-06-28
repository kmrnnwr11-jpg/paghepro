using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test GDPR: cifratura applicativa dei dati sensibili (IBAN). Verifica il round-trip del
/// <see cref="ProtettoreDati"/>, la retrocompatibilità coi dati storici in chiaro, il fallimento
/// con chiave errata e — a livello di integrazione — che l'IBAN sia <b>cifrato nella colonna del DB</b>
/// ma riletto in chiaro via EF.
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
        var p = new ProtettoreDati("chiave-di-test");
        const string iban = "IT60X0542811101000000123456";

        var cifrato = p.Proteggi(iban);

        Assert.NotNull(cifrato);
        Assert.StartsWith("enc:v1:", cifrato);
        Assert.NotEqual(iban, cifrato);
        Assert.Equal(iban, p.Svela(cifrato));
    }

    [Fact]
    public void Proteggi_DueVolte_ProduceCifratiDiversiMaStessoChiaro()
    {
        var p = new ProtettoreDati("chiave-di-test");
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
        var p = new ProtettoreDati("chiave-di-test");
        Assert.Equal(valore, p.Proteggi(valore));
        Assert.Equal(valore, p.Svela(valore));
    }

    [Fact]
    public void Svela_DatoStoricoInChiaro_RestituitoInvariato()
    {
        var p = new ProtettoreDati("chiave-di-test");
        const string ibanInChiaro = "IT60X0542811101000000123456"; // senza prefisso enc:v1:
        Assert.Equal(ibanInChiaro, p.Svela(ibanInChiaro));
    }

    [Fact]
    public void Svela_ConChiaveErrata_Lancia()
    {
        var cifrato = new ProtettoreDati("chiave-giusta").Proteggi("IT60X0542811101000000123456");
        var altro = new ProtettoreDati("chiave-sbagliata");
        Assert.Throws<InvalidOperationException>(() => altro.Svela(cifrato));
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
                Id = dipId, TenantId = tenant,
                CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi", Iban = iban,
            });
            await db.SaveChangesAsync();
        }

        // Lettura grezza della colonna: NON deve contenere l'IBAN in chiaro.
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Iban FROM Dipendenti WHERE CodiceFiscale = $cf";
            cmd.Parameters.AddWithValue("$cf", "RSSMRA80A01F205X");
            var grezzo = (string?)await cmd.ExecuteScalarAsync();
            Assert.NotNull(grezzo);
            Assert.StartsWith("enc:v1:", grezzo);
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
}
