using System.Security.Claims;
using System.Text;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Api;

public sealed record SalvaPosizioneInailRequest(
    int Anno, string Pat, string VoceTariffa, decimal TassoPerMille);

public sealed record SalvaAutoliquidazioneInailRequest(int Anno, decimal RataGiaVersata = 0m);

public sealed record CambiaStatoInailRequest(string Stato);

public sealed record PraticaInailDto(
    Guid Id, Guid AziendaId, int Anno, string Pat, string VoceTariffa, int NumeroCedolini,
    decimal RetribuzioniAnno, decimal TassoPerMille, decimal Regolazione, decimal RataAnticipata,
    decimal RataGiaVersata, decimal TotaleDaVersare, string Stato, DateTime CreataIl);

/// <summary>
/// Endpoint per la posizione INAIL (PAT + tasso), la bozza di autoliquidazione e le pratiche
/// archiviate, isolati per tenant. Mappato come metodo di estensione per ridurre le modifiche a
/// Program.cs.
/// </summary>
public static class InailEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    private static PraticaInailDto ToDto(PraticaInailEntity p) => new(
        p.Id, p.AziendaId, p.Anno, p.Pat, p.VoceTariffa, p.NumeroCedolini,
        p.RetribuzioniAnno, p.TassoPerMille, p.Regolazione, p.RataAnticipata,
        p.RataGiaVersata, p.TotaleDaVersare, p.Stato, p.CreataIl);

    public static IEndpointRouteBuilder MapInail(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/inail/posizione", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, InailService svc) =>
        {
            var p = await svc.GetPosizioneAsync(Tenant(user), aziendaId, anno);
            return p is null
                ? Results.NotFound(new { errore = $"Posizione INAIL non registrata per l'anno {anno}." })
                : Results.Ok(new { p.Anno, p.Pat, p.VoceTariffa, p.TassoPerMille });
        }).RequireAuthorization();

        app.MapPost("/api/aziende/{aziendaId:guid}/inail/posizione", async (
            Guid aziendaId, SalvaPosizioneInailRequest req, ClaimsPrincipal user, InailService svc) =>
        {
            try
            {
                var p = await svc.SalvaPosizioneAsync(
                    Tenant(user), aziendaId, req.Anno, req.Pat, req.VoceTariffa, req.TassoPerMille);
                return Results.Ok(new { p.Anno, p.Pat, p.VoceTariffa, p.TassoPerMille });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/aziende/{aziendaId:guid}/inail/autoliquidazione", async (
            Guid aziendaId, int anno, decimal? rataVersata, ClaimsPrincipal user, InailService svc) =>
        {
            try
            {
                return Results.Ok(await svc.CalcolaAutoliquidazioneAsync(
                    Tenant(user), aziendaId, anno, rataVersata ?? 0m));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/aziende/{aziendaId:guid}/inail/autoliquidazione/salva", async (
            Guid aziendaId, SalvaAutoliquidazioneInailRequest req,
            ClaimsPrincipal user, PraticheInailService svc) =>
        {
            try
            {
                var p = await svc.CalcolaEArchiviaAsync(
                    Tenant(user), aziendaId, req.Anno, req.RataGiaVersata);
                return Results.Ok(ToDto(p));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/aziende/{aziendaId:guid}/inail/autoliquidazioni", async (
            Guid aziendaId, ClaimsPrincipal user, PraticheInailService svc) =>
        {
            var pratiche = await svc.ElencoAsync(Tenant(user), aziendaId);
            return Results.Ok(pratiche.Select(ToDto));
        }).RequireAuthorization();

        app.MapGet("/api/inail/{id:guid}", async (
            Guid id, ClaimsPrincipal user, PraticheInailService svc) =>
        {
            var p = await svc.GetAsync(Tenant(user), id);
            return p is null ? Results.NotFound() : Results.Ok(ToDto(p));
        }).RequireAuthorization();

        app.MapPost("/api/inail/{id:guid}/stato", async (
            Guid id, CambiaStatoInailRequest req, ClaimsPrincipal user, PraticheInailService svc) =>
        {
            if (!Enum.TryParse<StatoPraticaTelematica>(req.Stato, out var nuovo))
                return Results.BadRequest(new { errore = $"Stato non valido: {req.Stato}." });
            try
            {
                var p = await svc.CambiaStatoAsync(Tenant(user), id, nuovo);
                return Results.Ok(ToDto(p));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/inail/{id:guid}/bozza", async (
            Guid id, ClaimsPrincipal user, PraticheInailService svc) =>
        {
            var p = await svc.GetAsync(Tenant(user), id);
            if (p is null) return Results.NotFound();
            return Results.File(
                Encoding.UTF8.GetBytes(p.Bozza), "text/plain", $"autoliquidazione-inail-{p.Anno}.txt");
        }).RequireAuthorization();

        return app;
    }
}
