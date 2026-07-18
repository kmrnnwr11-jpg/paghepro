namespace Paghe.Persistence.Entities;

/// <summary>
/// Autoliquidazione INAIL calcolata e archiviata per un'azienda e un anno, con lo stato della
/// pratica. Conserva i totali e la bozza leggibile per l'export; nessun tracciato telematico
/// ufficiale (l'invio avviene sui servizi online INAIL, non tramite Desktop Telematico).
/// Isolata per tenant.
/// </summary>
public class PraticaInailEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }

    public int Anno { get; set; }
    public string Pat { get; set; } = "";
    public string VoceTariffa { get; set; } = "";

    public int NumeroCedolini { get; set; }
    public decimal RetribuzioniAnno { get; set; }
    public decimal TassoPerMille { get; set; }
    public decimal Regolazione { get; set; }
    public decimal RataAnticipata { get; set; }
    public decimal RataGiaVersata { get; set; }
    public decimal TotaleDaVersare { get; set; }

    /// <summary>Stato della pratica (valore di <c>StatoPraticaTelematica</c> serializzato come stringa).</summary>
    public string Stato { get; set; } = "Generata";

    /// <summary>Rappresentazione leggibile (bozza NON ufficiale) dell'autoliquidazione.</summary>
    public string Bozza { get; set; } = "";

    public DateTime CreataIl { get; set; }
    public DateTime UltimoAggiornamento { get; set; }
}
