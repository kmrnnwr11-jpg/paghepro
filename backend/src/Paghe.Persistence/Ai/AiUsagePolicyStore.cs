namespace Paghe.Persistence.Ai;

/// <summary>
/// Policy d'uso per una finalità (AiUsagePolicy del piano §6.2, sottoinsieme G2): stabilisce il
/// massimo concedibile; un grant può soltanto restringerla. Nel verticale G2 corrente il massimo
/// effettivo è S1-S2 / A1, operazioni read/search, nessuna scrittura.
/// </summary>
public sealed record AiUsagePolicy(
    string PurposeCode,
    string Descrizione,
    string ScopeMassimo,          // S0 | S1 | S2
    string AutonomiaMassima,      // A1 | A2
    IReadOnlyList<string> RisorseAmmesse,
    IReadOnlyList<string> OperazioniAmmesse,
    int TtlMassimoMinuti,
    int QuotaLettureDefault,
    int QuotaLettureMassima,
    int QuotaProposteDefault = 0,
    int QuotaProposteMassima = 0,
    int QuotaEffettiDefault = 0,
    int QuotaEffettiMassima = 0);

/// <summary>
/// Elenco chiuso e versionato delle finalità ammesse (purpose code del G0 Governance Pack).
/// Una finalità non presente qui è negata; il modello non può creare o estendere una policy.
/// La versione è registrata su ogni grant emesso per riproducibilità dell'audit.
/// </summary>
public static class AiUsagePolicyStore
{
    public const string Versione = "2026-08-22.3";

    private static readonly string[] SolaLettura = ["read", "search"];

    private static readonly Dictionary<string, AiUsagePolicy> Policies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SPIEGAZIONE_CALCOLO"] = new(
                "SPIEGAZIONE_CALCOLO",
                "Spiegare un risultato del motore deterministico citando fonti e periodo.",
                ScopeMassimo: "S1", AutonomiaMassima: "A1",
                ["Cedolini", "Rapporti", "Ccnl", "ParametriNormativi"], SolaLettura,
                TtlMassimoMinuti: 30, QuotaLettureDefault: 25, QuotaLettureMassima: 100),

            ["CONTROLLO_ANOMALIE_PAGHE"] = new(
                "CONTROLLO_ANOMALIE_PAGHE",
                "Controllare completezza e incoerenze dei dati di un'azienda/periodo.",
                ScopeMassimo: "S2", AutonomiaMassima: "A1",
                ["Aziende", "Dipendenti", "Rapporti", "Cartellini", "Cedolini",
                    "Scadenzario", "Durc", "PraticheF24"], SolaLettura,
                TtlMassimoMinuti: 120, QuotaLettureDefault: 250, QuotaLettureMassima: 1000),

            ["PREPARAZIONE_MESE_PAGHE"] = new(
                "PREPARAZIONE_MESE_PAGHE",
                "Controllare il mese e, nel collaudo A3, proporre correzioni reversibili delle presenze.",
                ScopeMassimo: "S2", AutonomiaMassima: "A3",
                ["Aziende", "Dipendenti", "Rapporti", "Cartellini", "Cedolini",
                    "PraticheF24", "ParametriNormativi", "Ccnl"],
                ["read", "search", "draft", "apply_reversible"],
                TtlMassimoMinuti: 120, QuotaLettureDefault: 250, QuotaLettureMassima: 1000,
                QuotaProposteDefault: 3, QuotaProposteMassima: 10,
                // Primo pilot: un solo effetto per mandato. Per ripetere il collaudo
                // l'operatore deve revocare consapevolmente il mandato e rifare il preflight.
                QuotaEffettiDefault: 1, QuotaEffettiMassima: 1),

            ["LETTURA_DOCUMENTO"] = new(
                "LETTURA_DOCUMENTO",
                "Leggere un documento importato e proporre i campi (l'euristica vince).",
                ScopeMassimo: "S1", AutonomiaMassima: "A1", [], SolaLettura,
                TtlMassimoMinuti: 30, QuotaLettureDefault: 25, QuotaLettureMassima: 100),

            ["SCADENZE_PROMEMORIA"] = new(
                "SCADENZE_PROMEMORIA",
                "Riepilogare scadenze e adempimenti dell'azienda con bozze di promemoria.",
                ScopeMassimo: "S2", AutonomiaMassima: "A1",
                ["Scadenzario", "Durc", "PraticheF24"], SolaLettura,
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
