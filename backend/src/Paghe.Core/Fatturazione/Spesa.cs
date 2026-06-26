namespace Paghe.Core.Fatturazione;

/// <summary>
/// Costo/spesa aziendale (fattura passiva o nota spese), con IVA eventualmente
/// detraibile. Alimenta la contabilità (costo) e il registro IVA acquisti.
/// </summary>
public sealed class Spesa
{
    public required DateOnly Data { get; init; }
    public required string Descrizione { get; init; }

    /// <summary>Conto di costo (collegabile al piano dei conti).</summary>
    public required string ContoCosto { get; init; }

    public string? Fornitore { get; init; }

    public required decimal Imponibile { get; init; }
    public decimal AliquotaIva { get; init; } = 22m;

    /// <summary>Percentuale di IVA detraibile (100 = interamente detraibile).</summary>
    public decimal PercentualeDetraibilita { get; init; } = 100m;
}
