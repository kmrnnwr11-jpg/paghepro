using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Correzione dell'anagrafica azienda (es. dopo una visura/OCR letta male): aggiorna solo
/// i campi indicati ed è isolata per tenant.
/// </summary>
public sealed class CorreggiAziendaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();

    public CorreggiAziendaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> CreaAziendaAsync()
    {
        await using var db = NuovoContesto();
        // Dati "letti male" dall'OCR della visura, da correggere.
        var a = await new AziendeService(db).CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "TECN0EDIL C0STRUZIONI SRL", PartitaIva = "00000001008",
            Indirizzo = "VIA GIUSEPPE VERD1 15", Cap = "20121", Comune = "MILAN0", Provincia = "MI",
        });
        return a.Id;
    }

    [Fact]
    public async Task Correggi_CambiaSoloICampiIndicati()
    {
        var id = await CreaAziendaAsync();

        await using (var db = NuovoContesto())
        {
            var a = await new AziendeService(db).CorreggiAsync(_tenant, id,
                new AziendeService.CorrezioneAzienda(
                    RagioneSociale: "Tecnoedil Costruzioni Srl",
                    Indirizzo: "Via Giuseppe Verdi 15", Comune: "Milano"));
            Assert.NotNull(a);
        }

        await using (var db2 = NuovoContesto())
        {
            var a = await new AziendeService(db2).GetAsync(_tenant, id);
            Assert.Equal("Tecnoedil Costruzioni Srl", a!.RagioneSociale);
            Assert.Equal("Via Giuseppe Verdi 15", a.Indirizzo);
            Assert.Equal("Milano", a.Comune);
            // Non indicati: invariati.
            Assert.Equal("00000001008", a.PartitaIva);
            Assert.Equal("20121", a.Cap);
            Assert.Equal("MI", a.Provincia);
        }
    }

    [Fact]
    public async Task Correggi_TenantDiverso_NonTrova()
    {
        var id = await CreaAziendaAsync();
        await using var db = NuovoContesto();
        var a = await new AziendeService(db).CorreggiAsync(Guid.NewGuid(), id,
            new AziendeService.CorrezioneAzienda(RagioneSociale: "Intruso Srl"));
        Assert.Null(a);
    }

    public void Dispose() => _connection.Dispose();
}
