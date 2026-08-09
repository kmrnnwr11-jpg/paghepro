namespace Paghe.Core.Parametri;

/// <summary>
/// Le regole di calcolo che cambiano da contratto a contratto: divisori, maggiorazioni per lo
/// straordinario, maturazione di ferie e permessi. Prima erano numeri scritti dentro il motore
/// con il commento «es. CCNL Commercio»: applicati a tutti, giusti per uno solo.
/// Sono versionate per anno insieme al resto dei parametri.
/// </summary>
public sealed record RegoleCcnl(
    /// <summary>Divide la retribuzione mensile per ottenere la paga oraria (es. 168, 169 in agricoltura).</summary>
    decimal DivisoreOrario,
    /// <summary>Divide la retribuzione mensile per ottenere la paga giornaliera (di norma 26).</summary>
    decimal DivisoreGiornaliero,
    decimal MaggiorazioneStraordinario,
    decimal MaggiorazioneStraordinarioNotturno,
    decimal MaggiorazioneStraordinarioFestivo,
    /// <summary>Giorni di ferie maturati ogni mese a tempo pieno.</summary>
    decimal GiorniFerieMese,
    /// <summary>Ore di permesso ROL maturate ogni mese a tempo pieno.</summary>
    decimal OrePermessiMese);
