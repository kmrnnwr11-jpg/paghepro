using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Documenti;
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
        Assert.Equal("colori", await svc.LeggiAsync(
            tenant, utente, PreferenzeUtenteService.StileBusta,
            CatalogoStiliCedolino.ChiavePredefinita));

        await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "editoriale");
        var tutte = await svc.LeggiTutteAsync(tenant, utente);
        Assert.Equal(2, tutte.Count);
        Assert.Equal("editoriale", tutte[PreferenzeUtenteService.StileBusta]);

        await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, "");
        Assert.Equal("colori", await svc.LeggiAsync(
            tenant, utente, PreferenzeUtenteService.StileBusta,
            CatalogoStiliCedolino.ChiavePredefinita));
    }

    [Fact]
    public async Task TuttiIQuindiciStili_SonoAmmessiDallaPreferenza()
    {
        var (tenant, utente) = (Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var svc = new PreferenzeUtenteService(db);

        foreach (string stile in CatalogoStiliCedolino.Chiavi)
        {
            await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.StileBusta, stile);
            Assert.Equal(stile, await svc.LeggiAsync(
                tenant, utente, PreferenzeUtenteService.StileBusta,
                CatalogoStiliCedolino.ChiavePredefinita));
        }

        Assert.Empty(await svc.LeggiTutteAsync(Guid.NewGuid(), utente));
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
        var (utenteA, utenteB) = (Guid.NewGuid(), Guid.NewGuid());
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var svc = new PreferenzeUtenteService(db);

        await svc.SalvaAsync(tenantA, utenteA, PreferenzeUtenteService.DicituraStudio, "Studio Rossi");
        await svc.SalvaAsync(tenantA, utenteA, PreferenzeUtenteService.StileBusta, "classico");

        Assert.Empty(await svc.LeggiTutteAsync(tenantB, utenteA));
        Assert.Empty(await svc.LeggiTutteAsync(tenantA, utenteB));
        Assert.Equal("", await svc.LeggiAsync(tenantB, utenteA, PreferenzeUtenteService.DicituraStudio, ""));
        Assert.Equal("colori", await svc.LeggiAsync(
            tenantB, utenteA, PreferenzeUtenteService.StileBusta,
            CatalogoStiliCedolino.ChiavePredefinita));
        Assert.Equal("colori", await svc.LeggiAsync(
            tenantA, utenteB, PreferenzeUtenteService.StileBusta,
            CatalogoStiliCedolino.ChiavePredefinita));
        Assert.Equal("classico", await svc.LeggiAsync(
            tenantA, utenteA, PreferenzeUtenteService.StileBusta,
            CatalogoStiliCedolino.ChiavePredefinita));
    }

    [Fact]
    public async Task TemaEAccentoInterfaccia_AccettanoSoloIlCatalogo()
    {
        var (tenant, utente) = (Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var svc = new PreferenzeUtenteService(db);

        foreach (string tema in PreferenzeUtenteService.TemiInterfaccia)
        {
            await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.TemaInterfaccia, tema);
            Assert.Equal(tema, await svc.LeggiAsync(
                tenant, utente, PreferenzeUtenteService.TemaInterfaccia, "chiaro"));
        }
        foreach (string accento in PreferenzeUtenteService.AccentiInterfaccia)
        {
            await svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.AccentoInterfaccia, accento);
            Assert.Equal(accento, await svc.LeggiAsync(
                tenant, utente, PreferenzeUtenteService.AccentoInterfaccia, "salvia"));
        }

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.TemaInterfaccia, "notte"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SalvaAsync(tenant, utente, PreferenzeUtenteService.AccentoInterfaccia, "#ff0000"));
        // Il tema resta una scelta dell'utente: nessuna preferenza cambia i numeri dei documenti.
        Assert.Equal("automatico", (await svc.LeggiTutteAsync(tenant, utente))[PreferenzeUtenteService.TemaInterfaccia]);
    }
}
