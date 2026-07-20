using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paghe.Api;
using Paghe.Persistence;
using Paghe.Persistence.Gdpr;

namespace Paghe.Tests;

public sealed class AuditRichiesteMiddlewareTests : IDisposable
{
    private readonly SqliteConnection _connessione = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;

    public AuditRichiesteMiddlewareTests()
    {
        _connessione.Open();
        var servizi = new ServiceCollection();
        servizi.AddLogging();
        servizi.AddDbContext<PagheDbContext>(o => o.UseSqlite(_connessione));
        servizi.AddScoped<AuditLogService>();
        _provider = servizi.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<PagheDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task GetAutenticata_RegistraTemplateChiaveEsitoECorrelazione_SenzaQuery()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        var record = Guid.NewGuid();
        var middleware = CreaMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var context = CreaContesto(tenant, utente, "GET", $"/api/aziende/{record}");
        context.Request.QueryString = new QueryString("?email=segreto@example.it");
        context.Request.RouteValues["id"] = record;
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/aziende/{id:guid}"),
            0,
            EndpointMetadataCollection.Empty,
            "test"));

        await middleware.InvokeAsync(context);

        await using var scope = _provider.CreateAsyncScope();
        var riga = await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.SingleAsync();
        Assert.Equal("Accesso", riga.Categoria);
        Assert.Equal("Lettura", riga.Azione);
        Assert.Equal("Aziende", riga.Entita);
        Assert.Equal($"id={record:D}", riga.ChiaveEntita);
        Assert.Equal("/api/aziende/{id:guid}", riga.PercorsoTemplate);
        Assert.Equal("Riuscito", riga.Esito);
        Assert.Equal(200, riga.StatusCode);
        Assert.Equal("trace-test", riga.CorrelationId);
        Assert.DoesNotContain("segreto", string.Join('|',
            riga.Entita, riga.ChiaveEntita, riga.PercorsoTemplate, riga.CampiModificati));
    }

    [Fact]
    public async Task RichiestaNegataDalRbac_VieneRegistrataComeNegata()
    {
        var middleware = CreaMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        });
        var context = CreaContesto(Guid.NewGuid(), Guid.NewGuid(), "DELETE", "/api/aziende/qualcosa");

        await middleware.InvokeAsync(context);

        await using var scope = _provider.CreateAsyncScope();
        var riga = await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.SingleAsync();
        Assert.Equal("Operazione", riga.Azione);
        Assert.Equal("Negato", riga.Esito);
        Assert.Equal(403, riga.StatusCode);
        Assert.Equal("/api/(rotta non riconosciuta)", riga.PercorsoTemplate);
    }

    [Fact]
    public async Task EccezioneEndpoint_VieneRegistrataComeErroreERilanciata()
    {
        var middleware = CreaMiddleware(_ => throw new InvalidOperationException("guasto test"));
        var context = CreaContesto(Guid.NewGuid(), Guid.NewGuid(), "POST", "/api/aziende");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        await using var scope = _provider.CreateAsyncScope();
        var riga = await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.SingleAsync();
        Assert.Equal("Errore", riga.Esito);
        Assert.Equal(500, riga.StatusCode);
    }

    [Fact]
    public async Task RichiestaAnonima_NonCreaRigaSenzaTenant()
    {
        var middleware = CreaMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext { RequestServices = _provider };
        context.Request.Path = "/api/aziende";
        context.Request.Method = "GET";

        await middleware.InvokeAsync(context);

        await using var scope = _provider.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PagheDbContext>().AuditLog.ToListAsync());
    }

    private AuditRichiesteMiddleware CreaMiddleware(RequestDelegate next) =>
        ActivatorUtilities.CreateInstance<AuditRichiesteMiddleware>(_provider, next);

    private DefaultHttpContext CreaContesto(
        Guid tenant,
        Guid utente,
        string metodo,
        string percorso)
    {
        var context = new DefaultHttpContext { RequestServices = _provider };
        context.TraceIdentifier = "trace-test";
        context.Request.Method = metodo;
        context.Request.Path = percorso;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenant.ToString()),
            new Claim("sub", utente.ToString()),
            new Claim(ClaimTypes.Role, "admin"),
        ], "test"));
        return context;
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connessione.Dispose();
    }
}
