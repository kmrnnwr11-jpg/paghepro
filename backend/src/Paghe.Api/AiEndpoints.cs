using System.Security.Claims;
using Paghe.Core.Ai;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint di servizio del livello AI: stato (provider e consumo del mese dello studio)
/// e prova di funzionamento della configurazione. Mappati come metodo di estensione per
/// ridurre le modifiche a Program.cs.
/// </summary>
public static class AiEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapAi(this IEndpointRouteBuilder app)
    {
        // Stato del livello AI per lo studio: provider attivo, consumo e limite del mese.
        app.MapGet("/api/ai/stato", async (
            IEstrattoreAiDocumenti ai, AiOptions opzioni, ConsumoAiService consumi,
            ClaimsPrincipal user) =>
        {
            int consumo = await consumi.ConsumoDelMeseAsync(Tenant(user));
            return Results.Ok(new
            {
                provider = opzioni.Provider,
                attivo = ai.Attivo,
                visione = ai.VedeImmagini,
                consumoMese = consumo,
                limiteMensile = opzioni.LimiteMensileChiamate,
            });
        }).RequireAuthorization();

        // Prova di configurazione: piccola estrazione contro il provider configurato.
        // Non passa dal contatore consumi: è un controllo tecnico, non uso dello studio.
        app.MapPost("/api/ai/prova", async (IEstrattoreAiDocumenti ai) =>
        {
            if (!ai.Attivo)
                return Results.Ok(new
                {
                    ok = false,
                    messaggio = "AI spenta: impostare Ai:Provider e Ai:ApiKey nella configurazione del server.",
                });

            try
            {
                var proposta = await ai.EstraiAsync(
                    "prova di configurazione",
                    "Ragione sociale: PROVA SRL - Partita IVA: 01234567890",
                    ["partitaIva"]);
                string? valore = proposta?.Campi
                    .FirstOrDefault(c => c.Nome.Equals("partitaIva", StringComparison.OrdinalIgnoreCase))
                    ?.Valore;
                return valore == "01234567890"
                    ? Results.Ok(new { ok = true, messaggio = "AI funzionante: il provider risponde e legge correttamente." })
                    : Results.Ok(new
                    {
                        ok = false,
                        messaggio = "Il provider risponde ma la lettura non è quella attesa: "
                            + (valore ?? "nessuna proposta") + ".",
                    });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { ok = false, messaggio = "Errore dal provider: " + ex.Message });
            }
        }).RequireAuthorization();

        return app;
    }
}
