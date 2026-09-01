using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>Preferenze d'uso per utente: whitelist, isolamento tenant, upsert e rimozione.</summary>
public sealed class PreferenzeUtenteTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PreferenzeUtenteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Salva_Rilegge_Aggiorna_E_RimuoveConValoreVuoto()
    {
        var (tenant, utente) = (Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var svc = new PreferenzeUtenteService(db);

        await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "colori");
        await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.FontDocumenti, "carlito");
        Assert.Equal("colori", await svc.LeggiAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "classico"));

        await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "editoriale");
        var tutte = await svc.LeggiTutteAsync(tenant, utente);
        Assert.Equal(2, tutte.Count);
        Assert.Equal("editoriale", tutte[PreferenzeUtenteService.StileBusta]);

        await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "");
        Assert.Equal("classico", await svc.LeggiAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "classico"));
    }

    [Fact]
    public async Task ChiaviSconosciuteEValoriFuoriLista_VengonoRifiutati()
    {
        var (tenant, utente) = (Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var svc = new PreferenzeUtenteService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, "chiave_inventata", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "zebrato"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.ColoreAccento, "#FF0000"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.DicituraStudio, new string('x', 91)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.DicituraStudio, "riga\ncontrollo"));
    }

    [Fact]
    public async Task LePreferenzeDiUnTenant_NonSiVedonoDaUnAltro()
    {
        var utente = Guid.NewGuid();
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var svc = new PreferenzeUtenteService(db);

        await svc.SalvaAsync(tenantA, utente, PreferenzeUtenteService.DicituraStudio, "Studio Rossi");

        Assert.Empty(await svc.LeggiTutteAsync(tenantB, utente));
        Assert.Equal("", await svc.LeggiAsync(tenantB, utente, PreferenzeUtenteService.DicituraStudio, ""));
    }
}
