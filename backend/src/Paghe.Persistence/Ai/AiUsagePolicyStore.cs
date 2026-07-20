namespace Paghe.Persistence.Ai;

/// <summary>
/// Policy d'uso per una finalità (AiUsagePolicy del piano §6.2, sottoinsieme G2): stabilisce il
/// massimo concedibile; un grant può soltanto restringerla. In G2 il massimo assoluto è
/// S0-S2 / A1-A2, operazioni read/search/draft, nessuna scrittura.
/// </summary>
public sealed record AiUsagePolicy(
    string PurposeCode,
    string Descrizione,
    string ScopeMassimo,          // S0 | S1 | S2
    string AutonomiaMassima,      // A1 | A2
    IReadOnlyList<string> OperazioniAmmesse,
    int TtlMassimoMinuti,
    int QuotaLettureDefault,
    int QuotaLettureMassima);

/// <summary>
/// Elenco chiuso e versionato delle finalità ammesse (purpose code del G0 Governance Pack).
/// Una finalità non presente qui è negata; il modello non può creare o estendere una policy.
/// La versione è registrata su ogni grant emesso per riproducibilità dell'audit.
/// </summary>
public static class AiUsagePolicyStore
{
    public const string Versione = "2026-07-16.1";

    private static readonly string[] LetturaEBozza = ["read", "search", "draft"];
    private static readonly string[] SolaLettura = ["read", "search"];

    private static readonly Dictionary<string, AiUsagePolicy> Policies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SPIEGAZIONE_CALCOLO"] = new(
                "SPIEGAZIONE_CALCOLO",
                "Spiegare un risultato del motore deterministico citando fonti e periodo.",
                ScopeMassimo: "S1", AutonomiaMassima: "A1", SolaLettura,
                TtlMassimoMinuti: 30, QuotaLettureDefault: 25, QuotaLettureMassima: 100),

            ["CONTROLLO_ANOMALIE"] = new(
                "CONTROLLO_ANOMALIE",
                "Controllare completezza e incoerenze dei dati di un'azienda/periodo.",
                ScopeMassimo: "S2", AutonomiaMassima: "A2", LetturaEBozza,
                TtlMassimoMinuti: 120, QuotaLettureDefault: 250, QuotaLettureMassima: 1000),

            ["PREPARAZIONE_MESE_PAGHE"] = new(
                "PREPARAZIONE_MESE_PAGHE",
                "Preparare la pratica mensile paghe: presenze, variabili, bozze cedolino.",
                ScopeMassimo: "S2", AutonomiaMassima: "A2", LetturaEBozza,
                TtlMassimoMinuti: 120, QuotaLettureDefault: 250, QuotaLettureMassima: 1000),

            ["LETTURA_DOCUMENTO_IMPORTATO"] = new(
                "LETTURA_DOCUMENTO_IMPORTATO",
                "Leggere un documento importato e proporre i campi (l'euristica vince).",
                ScopeMassimo: "S1", AutonomiaMassima: "A2", LetturaEBozza,
                TtlMassimoMinuti: 30, QuotaLettureDefault: 25, QuotaLettureMassima: 100),

            ["RIEPILOGO_SCADENZE"] = new(
                "RIEPILOGO_SCADENZE",
                "Riepilogare scadenze e adempimenti dell'azienda con bozze di promemoria.",
                ScopeMassimo: "S2", AutonomiaMassima: "A2", LetturaEBozza,
                TtlMassimoMinuti: 60, QuotaLettureDefault: 100, QuotaLettureMassima: 500),
        };

    /// <summary>Null se la finalità non è ammessa (default DENY).</summary>
    public static AiUsagePolicy? Trova(string purposeCode) =>
        string.IsNullOrWhiteSpace(purposeCode) ? null : Policies.GetValueOrDefault(purposeCode.Trim());

    public static IReadOnlyCollection<AiUsagePolicy> Tutte => Policies.Values;

    /// <summary>Confronto d'ordine fra scope: S0 &lt; S1 &lt; S2 (S3/S4 mai in G2).</summary>
    public static bool ScopeConsentito(string richiesto, string massimo)
    {
        static int Rango(string s) => s switch { "S0" => 0, "S1" => 1, "S2" => 2, _ => int.MaxValue };
        return Rango(richiesto) <= Rango(massimo) && Rango(richiesto) != int.MaxValue;
    }
}
