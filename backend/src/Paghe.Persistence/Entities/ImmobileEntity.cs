namespace Paghe.Persistence.Entities;

/// <summary>
/// Immobile per il calcolo IMU (anagrafica minima): categoria catastale, rendita, possesso.
/// Dato nuovo gestito direttamente dall'utente (il software non lo ricava da altre fonti).
/// </summary>
public class ImmobileEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }

    public string Descrizione { get; set; } = "";
    /// <summary>Categoria catastale (es. A/2, A/10, C/1, D/1…). Determina il moltiplicatore IMU.</summary>
    public string CategoriaCatastale { get; set; } = "A/2";
    public decimal RenditaCatastale { get; set; }
    public decimal PercentualePossesso { get; set; } = 100m;
    /// <summary>Mesi di possesso nell'anno (1–12).</summary>
    public int MesiPossesso { get; set; } = 12;
    /// <summary>Aliquota IMU in per mille (‰) deliberata dal Comune.</summary>
    public decimal AliquotaPerMille { get; set; } = 8.6m;
    /// <summary>Abitazione principale non di lusso: esente IMU (categorie A/1, A/8, A/9 restano imponibili).</summary>
    public bool AbitazionePrincipale { get; set; }

    public DateTimeOffset CreatoIl { get; set; }
}
