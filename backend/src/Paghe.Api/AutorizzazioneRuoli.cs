using System.Security.Claims;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

/// <summary>
/// Middleware RBAC: applica <see cref="RuoloPolicy"/> alle richieste <b>autenticate</b>. Un utente
/// <c>sola_lettura</c> non può eseguire scritture; le azioni riservate (anonimizzazione GDPR) sono
/// consentite solo all'<c>admin</c>. Da inserire dopo <c>UseAuthentication</c>/<c>UseAuthorization</c>.
/// </summary>
public static class AutorizzazioneRuoli
{
    public static IApplicationBuilder UseControlloRuoli(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.User.Identity?.IsAuthenticated == true)
            {
                var ruolo = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
                if (!RuoloPolicy.PuoEseguire(ruolo, ctx.Request.Method, ctx.Request.Path))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsJsonAsync(
                        new { errore = "Permessi insufficienti per questa operazione (ruolo)." });
                    return;
                }
            }
            await next();
        });
}
