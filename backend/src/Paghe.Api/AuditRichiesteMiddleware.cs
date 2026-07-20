using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using Paghe.Persistence.Gdpr;

namespace Paghe.Api;

/// <summary>
/// Registra gli accessi API autenticati senza copiare body, risposta, query string, IP o user-agent.
/// Va eseguito dopo Authentication e prima di limiter/autorizzazione/RBAC, così vede anche i 403.
/// La scrittura usa un nuovo scope: un DbContext fallito nell'endpoint non può contaminare l'audit.
/// </summary>
public sealed class AuditRichiesteMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditRichiesteMiddleware> logger)
{
    private static readonly HashSet<string> EndpointPubbliciAutenticazione =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/registra",
            "/api/auth/login",
            "/api/auth/refresh",
            "/api/auth/logout",
            "/api/auth/mobile/registra",
            "/api/auth/mobile/login",
            "/api/auth/mobile/refresh",
            "/api/auth/mobile/logout",
            "/api/auth/google",
        };

    public async Task InvokeAsync(HttpContext context)
    {
        var utente = context.User;
        var tenantValido = Guid.TryParse(utente.FindFirst("tenant_id")?.Value, out var tenantId);
        var idUtente = utente.FindFirst("sub")?.Value
            ?? utente.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var utenteValido = Guid.TryParse(idUtente, out var utenteId);
        var daAuditare = utente.Identity?.IsAuthenticated == true
            && tenantValido
            && utenteValido
            && context.Request.Path.StartsWithSegments("/api")
            && !HttpMethods.IsOptions(context.Request.Method)
            && !EndpointPubbliciAutenticazione.Contains(context.Request.Path.Value ?? "");

        if (!daAuditare)
        {
            await next(context);
            return;
        }

        Exception? eccezione = null;
        using (AmbientContestoAudit.Imposta(tenantId, utenteId, context.TraceIdentifier))
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                eccezione = ex;
                throw;
            }
            finally
            {
                var status = eccezione is null
                    ? context.Response.StatusCode
                    : StatusCodes.Status500InternalServerError;
                await RegistraSenzaInterrompereLaRispostaAsync(context, tenantId, utenteId, status);
            }
        }
    }

    private async Task RegistraSenzaInterrompereLaRispostaAsync(
        HttpContext context,
        Guid tenantId,
        Guid utenteId,
        int statusCode)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var servizio = scope.ServiceProvider.GetRequiredService<AuditLogService>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await servizio.RegistraRichiestaAsync(new RichiestaAudit(
                tenantId,
                utenteId,
                AzioneDaMetodo(context.Request.Method),
                RisorsaDaEndpoint(context),
                ChiaviRouteSicure(context),
                context.Request.Method.ToUpperInvariant(),
                TemplateSicuro(context),
                EsitoDaStatus(statusCode),
                statusCode,
                context.TraceIdentifier,
                DateTime.UtcNow), timeout.Token);
        }
        catch (Exception ex)
        {
            // L'utente non deve ricevere un falso errore dopo che l'operazione è già terminata.
            // Il fallimento è comunque visibile nei log infrastrutturali con il correlation id.
            logger.LogError(
                ex,
                "Scrittura registro attività fallita. TraceId: {TraceId}",
                context.TraceIdentifier);
        }
    }

    internal static string TemplateSicuro(HttpContext context)
    {
        var template = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (string.IsNullOrWhiteSpace(template))
            return "/api/(rotta non riconosciuta)";
        return template.StartsWith('/') ? template : $"/{template}";
    }

    internal static string ChiaviRouteSicure(HttpContext context) =>
        string.Join(",", context.Request.RouteValues
            .Where(v => Guid.TryParse(v.Value?.ToString(), out _))
            .OrderBy(v => v.Key, StringComparer.Ordinal)
            .Select(v => $"{v.Key}={Guid.Parse(v.Value!.ToString()!):D}"));

    internal static string RisorsaDaEndpoint(HttpContext context)
    {
        var segmento = TemplateSicuro(context)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .SkipWhile(s => string.Equals(s, "api", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(s => !s.StartsWith('{'));
        return string.IsNullOrWhiteSpace(segmento)
            ? "Api"
            : char.ToUpperInvariant(segmento[0]) + segmento[1..];
    }

    internal static string AzioneDaMetodo(string metodo) =>
        HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) ? "Lettura" : "Operazione";

    internal static string EsitoDaStatus(int status) => status switch
    {
        >= 200 and < 400 => "Riuscito",
        StatusCodes.Status401Unauthorized => "NonAutenticato",
        StatusCodes.Status403Forbidden => "Negato",
        StatusCodes.Status404NotFound => "NonTrovato",
        StatusCodes.Status429TooManyRequests => "Limitato",
        >= 500 => "Errore",
        _ => "Rifiutato",
    };
}

public static class AuditRichiesteMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditRichieste(this IApplicationBuilder app) =>
        app.UseMiddleware<AuditRichiesteMiddleware>();
}
