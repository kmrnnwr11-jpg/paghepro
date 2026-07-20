using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Paghe.Persistence.Gdpr.Retention;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

public sealed record SalvaPolicyRetentionRequest(
    int VersioneAttesa,
    int? GiorniConservazione,
    bool Attiva,
    bool Approvata,
    string? BaseGiuridica,
    string? RiferimentoDecisione,
    string? IstruzioniBackup);

public sealed record CreaBloccoConservazioneRequest(string Categoria, string MotivoCodice);

public sealed record EseguiRetentionRequest(
    Guid AnteprimaId,
    Guid ChiaveIdempotenza,
    string CodiceConferma,
    int DimensioneLotto = 100);

public static class RetentionEndpoints
{
    public static IEndpointRouteBuilder MapRetention(this IEndpointRouteBuilder app)
    {
        var gruppo = app.MapGroup("/api/retention")
            .RequireAuthorization()
            .RequireRateLimiting("sensibili");

        gruppo.MapGet("", async (
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(
                    await servizio.ElencaPolicyAsync(Tenant(user), ct))));

        gruppo.MapPut("/{categoria}", async (
            string categoria,
            SalvaPolicyRetentionRequest richiesta,
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(await servizio.AggiornaPolicyAsync(
                    Tenant(user),
                    Utente(user),
                    categoria,
                    new ModificaPolicyRetention(
                        richiesta.VersioneAttesa,
                        richiesta.GiorniConservazione,
                        richiesta.Attiva,
                        richiesta.Approvata,
                        richiesta.BaseGiuridica,
                        richiesta.RiferimentoDecisione,
                        richiesta.IstruzioniBackup),
                    ct))));

        gruppo.MapPost("/{categoria}/anteprima", async (
            string categoria,
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(await servizio.CreaAnteprimaAsync(
                    Tenant(user), Utente(user), categoria, ct))));

        gruppo.MapGet("/blocchi", async (
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(
                    await servizio.ElencaBlocchiAsync(Tenant(user), ct))));

        gruppo.MapPost("/blocchi", async (
            CreaBloccoConservazioneRequest richiesta,
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(await servizio.CreaBloccoAsync(
                    Tenant(user),
                    Utente(user),
                    richiesta.Categoria,
                    richiesta.MotivoCodice,
                    ct))));

        gruppo.MapPost("/blocchi/{id:guid}/revoca", async (
            Guid id,
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(await servizio.RevocaBloccoAsync(
                    Tenant(user), Utente(user), id, ct))));

        gruppo.MapGet("/esecuzioni", async (
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(
                    await servizio.ElencaEsecuzioniAsync(Tenant(user), ct))));

        gruppo.MapPost("/esegui", async (
            EseguiRetentionRequest richiesta,
            ClaimsPrincipal user,
            RetentionService servizio,
            CancellationToken ct) =>
            SoloAdmin(user) is { } vietato
                ? vietato
                : await Sicuro(async () => Results.Ok(await servizio.EseguiAsync(
                    Tenant(user),
                    Utente(user),
                    richiesta.AnteprimaId,
                    richiesta.ChiaveIdempotenza,
                    richiesta.CodiceConferma,
                    richiesta.DimensioneLotto,
                    ct))));

        return app;
    }

    private static IResult? SoloAdmin(ClaimsPrincipal user) =>
        string.Equals(user.FindFirst(ClaimTypes.Role)?.Value,
            RuoloPolicy.Admin, StringComparison.OrdinalIgnoreCase)
            ? null
            : Results.Forbid();

    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("Tenant non presente nel token.");

    private static Guid Utente(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("Utente non presente nel token.");

    private static async Task<IResult> Sicuro(Func<Task<IResult>> azione)
    {
        try
        {
            return await azione();
        }
        catch (RetentionNonTrovataException e)
        {
            return Results.NotFound(new { errore = e.Message });
        }
        catch (RetentionConflittoException e)
        {
            return Results.Conflict(new { errore = e.Message });
        }
        catch (RetentionNonValidaException e)
        {
            return Results.UnprocessableEntity(new { errore = e.Message });
        }
    }
}
