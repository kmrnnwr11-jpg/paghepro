namespace Paghe.Persistence.Ai;

/// <summary>Registro di maturità per capability (piano §9.5): cosa l'assistente sa fare e a che stato.</summary>
public sealed record AiCapability(
    string CapabilityId,
    string Modulo,
    /// <summary>unavailable | read | draft | reversible_write | scheduled | external_one_shot.</summary>
    string StatoLocale,
    string[] ModalitaSupportate,
    string[] RisorseUsate,
    string[] PurposeCode,
    bool EffettiEsterni,
    string GateRichiesto);

/// <summary>
/// AiCapabilityRegistry del piano §7.1: elenco chiuso delle capacità dichiarate. Una capability
/// non registrata non esiste; lo stato non può superare il gate provato (oggi: deterministiche
/// in lettura sotto G2; tutto ciò che richiede un modello resta «unavailable» fino a G1+G3).
/// </summary>
public static class AiCapabilityRegistry
{
    private static readonly AiCapability[] Capabilities =
    [
        new("controllo.scadenze", "Adempimenti", "read",
            ["strict", "guided"], ["Scadenzario"], ["SCADENZE_PROMEMORIA", "CONTROLLO_ANOMALIE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.durc_f24", "Adempimenti", "read",
            ["strict", "guided"], ["Durc", "PraticheF24"],
            ["SCADENZE_PROMEMORIA", "CONTROLLO_ANOMALIE_PAGHE", "PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.anagrafica.azienda", "Anagrafiche", "read",
            ["strict", "guided"], ["Aziende"],
            ["CONTROLLO_ANOMALIE_PAGHE", "PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.dipendenti.inquadramento", "Paghe", "read",
            ["strict", "guided"], ["Dipendenti"],
            ["CONTROLLO_ANOMALIE_PAGHE", "PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.presenze.mese", "Paghe", "read",
            ["guided"], ["Cartellini", "Rapporti", "Cedolini", "Ccnl", "ParametriNormativi"],
            ["CONTROLLO_ANOMALIE_PAGHE", "PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("compila.presenze.assenza_non_retribuita", "Paghe", "reversible_write",
            ["guided"], ["Dipendenti", "Rapporti", "Cartellini", "Cedolini", "PraticheF24"],
            ["PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "A3"),

        new("elabora.cedolino.bozza_mese", "Paghe", "reversible_write",
            ["guided"], ["Dipendenti", "Rapporti", "Cartellini", "Cedolini", "PraticheF24"],
            ["PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "A3"),

        // Richiedono un modello: restano spente finché l'isolamento G1 e il gate G3 non sono provati.
        new("spiegazione.calcolo.cedolino", "Paghe", "unavailable",
            ["strict", "guided"], ["Cedolini", "Rapporti", "Ccnl", "ParametriNormativi"], ["SPIEGAZIONE_CALCOLO"],
            EffettiEsterni: false, GateRichiesto: "G3"),

        new("lettura.documento.importato", "Documenti", "unavailable",
            ["strict", "guided"], [], ["LETTURA_DOCUMENTO"],
            EffettiEsterni: false, GateRichiesto: "G4"),
    ];

    public static IReadOnlyList<AiCapability> Tutte => Capabilities;

    public static AiCapability? Trova(string capabilityId) =>
        Capabilities.FirstOrDefault(c =>
            string.Equals(c.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Verifica che ogni risorsa richiesta sia coperta da una capability realmente disponibile
    /// per finalità, modalità e gate correnti. La policy da sola non può quindi elevare una
    /// capability G3/G4 o ancora marcata «unavailable».
    /// </summary>
    public static (bool Consentito, string? Motivo) VerificaUsoG2(
        string purposeCode, string modalita, IEnumerable<string> risorse)
    {
        string modo = modalita switch
        {
            "Stretta" => "strict",
            "Guidata" => "guided",
            _ => "",
        };

        foreach (var risorsa in risorse)
        {
            var candidate = Capabilities.Where(c =>
                c.PurposeCode.Contains(purposeCode, StringComparer.OrdinalIgnoreCase)
                && c.RisorseUsate.Contains(risorsa, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (candidate.Length == 0)
                return (false, $"Nessuna capability collega la finalità '{purposeCode}' alla risorsa '{risorsa}'.");
            if (!candidate.Any(c => c.StatoLocale != "unavailable"
                && c.GateRichiesto == "G2"
                && c.ModalitaSupportate.Contains(modo, StringComparer.OrdinalIgnoreCase)))
                return (false, $"La capability per '{risorsa}' non è disponibile in modalità {modalita} al gate G2.");
        }

        return (true, null);
    }

    /// <summary>
    /// Verifica una coppia esatta risorsa/operazione. Le letture seguono G2; draft e
    /// apply_reversible richiedono una capability reversibile A3 per quella stessa risorsa.
    /// </summary>
    public static (bool Consentito, string? Motivo) VerificaPermesso(
        string purposeCode, string modalita, string risorsa, string operazione)
    {
        if (operazione is "read" or "search")
            return VerificaUsoG2(purposeCode, modalita, [risorsa]);

        if (operazione is not ("draft" or "apply_reversible"))
            return (false, $"Operazione '{operazione}' non riconosciuta.");

        string modo = modalita switch
        {
            "Stretta" => "strict",
            "Guidata" => "guided",
            _ => "",
        };
        var capability = Capabilities.FirstOrDefault(c =>
            c.StatoLocale == "reversible_write"
            && c.GateRichiesto == "A3"
            && c.PurposeCode.Contains(purposeCode, StringComparer.OrdinalIgnoreCase)
            && c.RisorseUsate.Contains(risorsa, StringComparer.OrdinalIgnoreCase)
            && c.ModalitaSupportate.Contains(modo, StringComparer.OrdinalIgnoreCase));
        return capability is null
            ? (false, $"La coppia '{risorsa}:{operazione}' non è disponibile al gate A3.")
            : (true, null);
    }
}
