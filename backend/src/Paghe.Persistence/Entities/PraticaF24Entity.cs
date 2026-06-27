namespace Paghe.Persistence.Entities;

/// <summary>
/// F24 generato dalle paghe e archiviato, con lo stato della pratica telematica.
/// Conserva i totali, il modello serializzato (JSON) e la bozza leggibile per l'export.
/// Isolato per tenant.
/// </summary>
public class PraticaF24Entity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }

    public int Anno { get; set; }
    public int Mese { get; set; }
    public DateOnly DataVersamento { get; set; }

    public decimal TotaleDebiti { get; set; }
    public decimal TotaleCrediti { get; set; }
    public decimal SaldoFinale { get; set; }

    /// <summary>Stato della pratica (valore di <c>StatoPraticaTelematica</c> serializzato come stringa).</summary>
    public string Stato { get; set; } = "Generata";

    /// <summary>Vero se l'F24 ha superato i controlli interni al momento della generazione.</summary>
    public bool Valido { get; set; }

    /// <summary>Modello F24 serializzato (JSON) per ricostruzione/export.</summary>
    public string ModelloJson { get; set; } = "";

    /// <summary>Rappresentazione leggibile (bozza NON ufficiale) dell'F24.</summary>
    public string Bozza { get; set; } = "";

    /// <summary>Tracciato telematico UFFICIALE a lunghezza fissa (record A/M/V/Z) per il Desktop Telematico.</summary>
    public string TracciatoUfficiale { get; set; } = "";

    public DateTime CreataIl { get; set; }
    public DateTime UltimoAggiornamento { get; set; }
}
