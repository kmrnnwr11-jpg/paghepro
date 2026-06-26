namespace Paghe.Core.Contabilita;

/// <summary>
/// Piano dei conti di esempio (classificazione CEE) e un set di saldi quadrato,
/// utili per demo e test. Valori illustrativi.
/// </summary>
public static class SeedPianoConti
{
    public static IReadOnlyList<ContoBilancio> PianoConti() => new[]
    {
        new ContoBilancio("10.10", "Impianti e macchinari",       MacroVoce.Immobilizzazioni),
        new ContoBilancio("20.10", "Crediti verso clienti",       MacroVoce.AttivoCircolante),
        new ContoBilancio("20.20", "Banca c/c",                   MacroVoce.AttivoCircolante),
        new ContoBilancio("30.10", "Capitale sociale",            MacroVoce.PatrimonioNetto),
        new ContoBilancio("30.20", "Riserva legale",              MacroVoce.PatrimonioNetto),
        new ContoBilancio("40.10", "Fondo TFR",                   MacroVoce.TrattamentoFineRapporto),
        new ContoBilancio("50.10", "Debiti verso fornitori",      MacroVoce.Debiti),
        new ContoBilancio("60.10", "Ricavi delle vendite",        MacroVoce.ValoreDellaProduzione),
        new ContoBilancio("70.10", "Acquisti di merci",           MacroVoce.CostiDellaProduzione),
        new ContoBilancio("70.20", "Costi del personale",         MacroVoce.CostiDellaProduzione),
        new ContoBilancio("80.10", "Interessi passivi",           MacroVoce.OneriFinanziari),
        new ContoBilancio("90.10", "Imposte dell'esercizio",      MacroVoce.Imposte),
    };

    /// <summary>Set di saldi quadrato (totale dare = totale avere = 280.000).</summary>
    public static IReadOnlyList<SaldoConto> SaldiEsempio() => new[]
    {
        new SaldoConto("10.10",  50000m),
        new SaldoConto("20.10",  30000m),
        new SaldoConto("20.20",  20000m),
        new SaldoConto("30.10",  40000m),
        new SaldoConto("40.10",  10000m),
        new SaldoConto("50.10",  30000m),
        new SaldoConto("60.10", 200000m),
        new SaldoConto("70.10", 120000m),
        new SaldoConto("70.20",  50000m),
        new SaldoConto("80.10",   2000m),
        new SaldoConto("90.10",   8000m),
    };
}
