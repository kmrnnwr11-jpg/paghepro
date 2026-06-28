using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>Dettaglio anagrafico/contrattuale di un rapporto di lavoro (scheda dipendente).</summary>
public sealed record RapportoDettaglioDto(
    string RapportoId, string DipendenteId,
    string Nome, string Cognome, string CodiceFiscale, string? Iban, bool Anonimizzato,
    string CodiceCcnl, string Livello, string Qualifica,
    int NumeroMensilita, bool PartTime, decimal PercentualePartTime,
    int ScattiAnzianita, decimal Superminimo, string Regione, string DataAssunzione);

/// <summary>
/// Endpoint per il dettaglio di un rapporto (scheda dipendente reale), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class RapportoEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapRapportoDettaglio(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rapporti/{rapportoId:guid}", async (
            Guid rapportoId, ClaimsPrincipal user, AnagraficheLavoroService svc) =>
        {
            var r = await svc.GetRapportoAsync(Tenant(user), rapportoId);
            if (r is null)
                return Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." });
            var d = r.Dipendente;
            return Results.Ok(new RapportoDettaglioDto(
                RapportoId: r.Id.ToString(),
                DipendenteId: r.DipendenteId.ToString(),
                Nome: d?.Nome ?? "",
                Cognome: d?.Cognome ?? "",
                CodiceFiscale: d?.CodiceFiscale ?? "",
                Iban: d?.Iban,
                Anonimizzato: d?.Anonimizzato ?? false,
                CodiceCcnl: r.CodiceCcnl,
                Livello: r.Livello,
                Qualifica: r.Qualifica,
                NumeroMensilita: r.NumeroMensilita,
                PartTime: r.PartTime,
                PercentualePartTime: r.PercentualePartTime,
                ScattiAnzianita: r.ScattiAnzianita,
                Superminimo: r.Superminimo,
                Regione: r.Regione,
                DataAssunzione: r.DataAssunzione.ToString("dd/MM/yyyy")));
        }).RequireAuthorization();

        return app;
    }
}
