namespace Paghe.Core.Contabilita;

/// <summary>
/// Macro-voci di riclassificazione del bilancio secondo lo schema civilistico
/// (Stato Patrimoniale art. 2424 c.c., Conto Economico art. 2425 c.c.).
/// Ogni conto del piano dei conti viene mappato su una di queste voci.
/// </summary>
public enum MacroVoce
{
    // --- Stato Patrimoniale — ATTIVO ---
    CreditiVersoSoci,           // A) Crediti verso soci per versamenti dovuti
    Immobilizzazioni,           // B) Immobilizzazioni (immat./mat./finanziarie)
    AttivoCircolante,           // C) Attivo circolante (rimanenze, crediti, liquidità)
    RateiRiscontiAttivi,        // D) Ratei e risconti attivi

    // --- Stato Patrimoniale — PASSIVO ---
    PatrimonioNetto,            // A) Patrimonio netto (capitale, riserve) — esclude l'utile
    FondiRischiOneri,           // B) Fondi per rischi e oneri
    TrattamentoFineRapporto,    // C) Trattamento di fine rapporto
    Debiti,                     // D) Debiti
    RateiRiscontiPassivi,       // E) Ratei e risconti passivi

    // --- Conto Economico ---
    ValoreDellaProduzione,      // A) Valore della produzione (ricavi)
    CostiDellaProduzione,       // B) Costi della produzione
    ProventiFinanziari,         // C) Proventi finanziari
    OneriFinanziari,            // C) Oneri finanziari
    Imposte                     // 20) Imposte sul reddito dell'esercizio
}

/// <summary>Conto del piano dei conti con la sua classificazione di bilancio.</summary>
public sealed record ContoBilancio(string Codice, string Descrizione, MacroVoce Voce);

/// <summary>
/// Saldo di un conto a fine periodo. Convenzione: l'importo è positivo nella natura
/// del conto (attivo/costi = saldo dare; passivo/netto/ricavi = saldo avere).
/// </summary>
public sealed record SaldoConto(string CodiceConto, decimal Importo);
