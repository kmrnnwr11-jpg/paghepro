using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Aliquote INPS specifiche per <b>settore/CCNL</b>, usate quando diverse dalla quota standard per
/// qualifica. La quota c/dipendente è ricavata da <b>buste reali</b>; la quota datore va confermata
/// dalla circolare INPS ufficiale. Estendibile: una riga per CCNL.
/// </summary>
public static class AliquoteInpsSettore
{
    private static readonly Dictionary<string, AliquotaInps> PerCcnlMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Autotrasporto / Logistica: quota c/dipendente 9,49% (verificata su 7 buste reali).
        ["TRASPORTI_LOGISTICA"] = new AliquotaInps(0.0949m, 0.2788m),
    };

    /// <summary>Aliquota INPS del CCNL se nota, altrimenti null (si usa la quota per qualifica).</summary>
    public static AliquotaInps? PerCcnl(string? codiceCcnl) =>
        !string.IsNullOrWhiteSpace(codiceCcnl) && PerCcnlMap.TryGetValue(codiceCcnl!, out var a) ? a : null;
}
