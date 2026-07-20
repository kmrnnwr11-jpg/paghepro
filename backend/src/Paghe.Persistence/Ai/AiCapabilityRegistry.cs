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
            ["strict", "guided"], ["Scadenzario"], ["RIEPILOGO_SCADENZE", "CONTROLLO_ANOMALIE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.anagrafica.azienda", "Anagrafiche", "read",
            ["strict", "guided"], ["Aziende"], ["CONTROLLO_ANOMALIE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.dipendenti.inquadramento", "Paghe", "read",
            ["strict", "guided"], ["Dipendenti"], ["CONTROLLO_ANOMALIE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        new("controllo.presenze.mese", "Paghe", "read",
            ["guided"], ["Cartellini", "Rapporti", "Cedolini"], ["CONTROLLO_ANOMALIE", "PREPARAZIONE_MESE_PAGHE"],
            EffettiEsterni: false, GateRichiesto: "G2"),

        // Richiedono un modello: restano spente finché l'isolamento G1 e il gate G3 non sono provati.
        new("spiegazione.calcolo.cedolino", "Paghe", "unavailable",
            ["strict", "guided"], ["Cedolini", "Rapporti", "Ccnl", "ParametriNormativi"], ["SPIEGAZIONE_CALCOLO"],
            EffettiEsterni: false, GateRichiesto: "G3"),

        new("lettura.documento.importato", "Documenti", "unavailable",
            ["strict", "guided"], [], ["LETTURA_DOCUMENTO_IMPORTATO"],
            EffettiEsterni: false, GateRichiesto: "G4"),
    ];

    public static IReadOnlyList<AiCapability> Tutte => Capabilities;

    public static AiCapability? Trova(string capabilityId) =>
        Capabilities.FirstOrDefault(c =>
            string.Equals(c.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase));
}
