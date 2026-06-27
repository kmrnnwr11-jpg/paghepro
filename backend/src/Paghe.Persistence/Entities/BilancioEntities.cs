namespace Paghe.Persistence.Entities;

/// <summary>
/// Bilancio d'esercizio riclassificato (schema civilistico CEE) calcolato e archiviato per
/// azienda e anno. Conserva i totali di Stato Patrimoniale e Conto Economico, il risultato
/// d'esercizio, lo stato della pratica e una bozza leggibile. Il dettaglio dei saldi è nelle
/// <see cref="Righe"/>. Isolato per tenant.
/// </summary>
public class BilancioEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }

    /// <summary>Anno dell'esercizio a cui si riferisce il bilancio.</summary>
    public int Anno { get; set; }

    // --- Conto Economico ---
    public decimal ValoreDellaProduzione { get; set; }
    public decimal CostiDellaProduzione { get; set; }
    public decimal ProventiFinanziari { get; set; }
    public decimal OneriFinanziari { get; set; }
    public decimal Imposte { get; set; }
    public decimal UtilePerditaEsercizio { get; set; }

    // --- Stato Patrimoniale ---
    public decimal TotaleAttivo { get; set; }
    public decimal TotalePassivo { get; set; }
    public decimal PatrimonioNetto { get; set; }

    /// <summary>Differenza Attivo − Passivo: zero se il bilancio quadra.</summary>
    public decimal SbilancioAttivoPassivo { get; set; }
    public bool Quadra { get; set; }

    /// <summary>Stato della pratica (valore di <c>StatoBilancio</c> serializzato come stringa).</summary>
    public string Stato { get; set; } = "Bozza";

    /// <summary>Bilancio riclassificato serializzato (JSON) per ricostruzione/export.</summary>
    public string BilancioJson { get; set; } = "";

    /// <summary>Rappresentazione leggibile (prospetto NON ufficiale) del bilancio.</summary>
    public string Bozza { get; set; } = "";

    public DateTime CreataIl { get; set; }
    public DateTime UltimoAggiornamento { get; set; }

    public List<RigaBilancioEntity> Righe { get; set; } = new();
}

/// <summary>
/// Riga del bilancio: un conto del piano dei conti con la sua classificazione di macro-voce
/// CEE e il saldo di fine esercizio. Incorpora piano dei conti e saldi, rendendo il bilancio
/// auto-contenuto e ricostruibile.
/// </summary>
public class RigaBilancioEntity
{
    public Guid Id { get; set; }
    public Guid BilancioId { get; set; }

    public string Codice { get; set; } = "";
    public string Descrizione { get; set; } = "";

    /// <summary>Macro-voce CEE (valore di <c>MacroVoce</c> serializzato come stringa).</summary>
    public string Voce { get; set; } = "";

    /// <summary>Saldo del conto (positivo nella natura del conto: attivo/costi a dare, passivo/ricavi a avere).</summary>
    public decimal Importo { get; set; }
}
