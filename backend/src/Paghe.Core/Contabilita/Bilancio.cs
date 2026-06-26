namespace Paghe.Core.Contabilita;

/// <summary>
/// Bilancio riclassificato: Stato Patrimoniale (Attivo/Passivo) e Conto Economico,
/// con il risultato d'esercizio e la verifica di quadratura.
/// </summary>
public sealed class Bilancio
{
    // --- Conto Economico ---
    public required decimal ValoreDellaProduzione { get; init; }   // A
    public required decimal CostiDellaProduzione { get; init; }    // B
    public required decimal ProventiFinanziari { get; init; }
    public required decimal OneriFinanziari { get; init; }
    public required decimal Imposte { get; init; }

    /// <summary>A − B: differenza tra valore e costi della produzione.</summary>
    public decimal DifferenzaValoreCosti => ValoreDellaProduzione - CostiDellaProduzione;

    /// <summary>C: proventi e oneri finanziari netti.</summary>
    public decimal ProventiOneriFinanziari => ProventiFinanziari - OneriFinanziari;

    public decimal RisultatoPrimaDelleImposte => DifferenzaValoreCosti + ProventiOneriFinanziari;

    /// <summary>21) Utile (positivo) o perdita (negativo) dell'esercizio.</summary>
    public decimal UtilePerditaEsercizio => RisultatoPrimaDelleImposte - Imposte;

    // --- Stato Patrimoniale — ATTIVO ---
    public required decimal CreditiVersoSoci { get; init; }
    public required decimal Immobilizzazioni { get; init; }
    public required decimal AttivoCircolante { get; init; }
    public required decimal RateiRiscontiAttivi { get; init; }

    public decimal TotaleAttivo =>
        CreditiVersoSoci + Immobilizzazioni + AttivoCircolante + RateiRiscontiAttivi;

    // --- Stato Patrimoniale — PASSIVO ---
    /// <summary>Patrimonio netto comprensivo dell'utile/perdita dell'esercizio.</summary>
    public required decimal PatrimonioNetto { get; init; }
    public required decimal FondiRischiOneri { get; init; }
    public required decimal TrattamentoFineRapporto { get; init; }
    public required decimal Debiti { get; init; }
    public required decimal RateiRiscontiPassivi { get; init; }

    public decimal TotalePassivo =>
        PatrimonioNetto + FondiRischiOneri + TrattamentoFineRapporto + Debiti + RateiRiscontiPassivi;

    /// <summary>Differenza Attivo − Passivo: dev'essere zero se il bilancio quadra.</summary>
    public decimal SbilancioAttivoPassivo => TotaleAttivo - TotalePassivo;

    public bool Quadra => SbilancioAttivoPassivo == 0m;
}
