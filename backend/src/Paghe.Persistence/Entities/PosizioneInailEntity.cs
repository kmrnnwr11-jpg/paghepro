namespace Paghe.Persistence.Entities;

/// <summary>
/// Posizione assicurativa INAIL di un'azienda per anno: PAT, voce di tariffa e tasso
/// applicato (per mille), presi dalla lettera "basi di calcolo" INAIL. Serve al calcolo
/// dell'autoliquidazione. Isolata per tenant, una per (azienda, anno).
/// </summary>
public class PosizioneInailEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public int Anno { get; set; }

    /// <summary>Posizione assicurativa territoriale (codice PAT).</summary>
    public string Pat { get; set; } = "";
    public string VoceTariffa { get; set; } = "";

    /// <summary>Tasso applicato, per mille (es. 12,5 = 1,25%).</summary>
    public decimal TassoPerMille { get; set; }

    public DateTimeOffset AggiornataIl { get; set; }
}
