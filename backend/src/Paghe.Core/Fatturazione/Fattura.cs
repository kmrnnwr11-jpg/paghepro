namespace Paghe.Core.Fatturazione;

/// <summary>Riga di dettaglio di una fattura.</summary>
public sealed class RigaFattura
{
    public required string Descrizione { get; init; }
    public decimal Quantita { get; init; } = 1m;
    public required decimal PrezzoUnitario { get; init; }

    /// <summary>Aliquota IVA in percentuale (es. 22 per il 22%).</summary>
    public required decimal AliquotaIva { get; init; }

    /// <summary>
    /// Natura IVA (codice FatturaPA, es. N1, N2.2...) obbligatoria quando l'aliquota è 0.
    /// </summary>
    public string? NaturaIva { get; init; }
}

/// <summary>Fattura attiva (emessa dall'azienda verso un cliente).</summary>
public sealed class Fattura
{
    public required string Numero { get; init; }
    public required DateOnly Data { get; init; }

    /// <summary>Tipo documento FatturaPA (TD01 = fattura, TD04 = nota di credito...).</summary>
    public string TipoDocumento { get; init; } = "TD01";

    public string Divisa { get; init; } = "EUR";

    /// <summary>Chi emette la fattura (azienda).</summary>
    public required DatiAnagrafici Cedente { get; init; }

    /// <summary>Il cliente destinatario.</summary>
    public required DatiAnagrafici Cessionario { get; init; }

    /// <summary>Codice destinatario SDI (7 caratteri) o "0000000" se via PEC.</summary>
    public string CodiceDestinatario { get; init; } = "0000000";

    public List<RigaFattura> Righe { get; init; } = new();
}

/// <summary>Riepilogo IVA per una singola aliquota.</summary>
public sealed class RiepilogoIva
{
    public required decimal Aliquota { get; init; }
    public required decimal Imponibile { get; init; }
    public required decimal Imposta { get; init; }
    public string? NaturaIva { get; init; }
}

/// <summary>Totali calcolati di una fattura.</summary>
public sealed class TotaliFattura
{
    public required IReadOnlyList<RiepilogoIva> Riepiloghi { get; init; }
    public required decimal TotaleImponibile { get; init; }
    public required decimal TotaleImposta { get; init; }
    public required decimal TotaleDocumento { get; init; }
}
