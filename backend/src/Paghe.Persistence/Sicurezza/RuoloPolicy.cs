namespace Paghe.Persistence.Sicurezza;

/// <summary>
/// Policy di autorizzazione per ruolo (RBAC). È una decisione <b>pura</b> (nessuna dipendenza HTTP,
/// solo stringhe), così è testabile e lo strato API la applica con un middleware.
/// Ruoli: <c>admin</c> · <c>operatore</c> · <c>sola_lettura</c>.
/// </summary>
public static class RuoloPolicy
{
    public const string Admin = "admin";
    public const string Operatore = "operatore";
    public const string SolaLettura = "sola_lettura";

    private static readonly HashSet<string> MetodiSicuri =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    /// <summary>Frammenti di path la cui <b>scrittura</b> è riservata all'admin (azioni sensibili/distruttive).</summary>
    private static readonly string[] RiservatiAdmin = { "/anonimizza", "/posta/caselle" };

    private static readonly string[] RiservatiSempreAdmin = { "/api/audit", "/api/retention" };

    /// <summary>
    /// True se un utente con questo ruolo può eseguire la richiesta. Regole:
    /// <list type="bullet">
    /// <item><c>sola_lettura</c> → solo metodi sicuri (GET/HEAD/OPTIONS);</item>
    /// <item>azioni riservate (es. anonimizzazione GDPR) → solo <c>admin</c>;</item>
    /// <item><c>admin</c>/<c>operatore</c> → tutto il resto.</item>
    /// </list>
    /// </summary>
    public static bool PuoEseguire(string? ruolo, string metodoHttp, string path)
    {
        var sicuro = MetodiSicuri.Contains(metodoHttp);

        if (RiservatiSempreAdmin.Any(r =>
                path.Equals(r, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase)))
            return string.Equals(ruolo, Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ruolo, AuthServiceRuoloSuperAdmin, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(ruolo, SolaLettura, StringComparison.OrdinalIgnoreCase))
            return sicuro;

        if (!sicuro && RiservatiAdmin.Any(r => path.Contains(r, StringComparison.OrdinalIgnoreCase)))
            return string.Equals(ruolo, Admin, StringComparison.OrdinalIgnoreCase);

        return string.Equals(ruolo, Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruolo, Operatore, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruolo, AuthServiceRuoloSuperAdmin, StringComparison.OrdinalIgnoreCase);
    }

    private const string AuthServiceRuoloSuperAdmin = "superadmin";
}
