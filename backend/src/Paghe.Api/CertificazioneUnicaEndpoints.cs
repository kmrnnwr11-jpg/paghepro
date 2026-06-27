using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza di Certificazione Unica (Fase 1 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class CertificazioneUnicaEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapCertificazioneUnica(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rapporti/{rapportoId:guid}/cu/{anno:int}", async (
            Guid rapportoId, int anno, ClaimsPrincipal user, CertificazioneUnicaService svc) =>
        {
            var bozza = await svc.CostruisciAsync(Tenant(user), rapportoId, anno);
            return bozza is null
                ? Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." })
                : Results.Ok(bozza);
        }).RequireAuthorization();

        return app;
    }
}
