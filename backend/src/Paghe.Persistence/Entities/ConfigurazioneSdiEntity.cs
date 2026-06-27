namespace Paghe.Persistence.Entities;

/// <summary>
/// Configurazione del canale di trasmissione SdI scelta dall'utente, una per tenant.
/// Permette il collegamento self-service di un provider accreditato senza modificare la
/// configurazione del server. L'ApiKey è un segreto: non viene mai restituita in chiaro dall'API.
/// </summary>
public class ConfigurazioneSdiEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>"Manuale" (prepara il file), "Simulato" (demo), "Rest" (provider accreditato).</summary>
    public string Modalita { get; set; } = "Manuale";

    /// <summary>Nome leggibile del provider scelto (es. "Aruba", "Fatture in Cloud", "Acube").</summary>
    public string Provider { get; set; } = "";

    public string BaseUrl { get; set; } = "";
    public string? ApiKey { get; set; }
    public string EndpointInvio { get; set; } = "invoices";
    public string EndpointEsito { get; set; } = "invoices/{id}";
    public string CampoId { get; set; } = "id";
    public string CampoStato { get; set; } = "status";

    /// <summary>Vero se il provider richiede la fattura già firmata localmente (.p7m).</summary>
    public bool FirmaLocale { get; set; }

    /// <summary>Vero quando la configurazione è attiva e usata per le trasmissioni.</summary>
    public bool Attivo { get; set; }

    public DateTimeOffset AggiornataIl { get; set; }
}
