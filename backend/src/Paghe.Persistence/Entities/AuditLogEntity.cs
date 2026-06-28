namespace Paghe.Persistence.Entities;

/// <summary>
/// Riga di audit log (GDPR / tracciabilità): registra <b>chi</b> ha fatto <b>cosa</b> e <b>quando</b>
/// su un dato tenant-scoped. Per non duplicare dati personali, <b>non</b> salva i valori dei campi:
/// solo l'azione, l'entità, la chiave e l'elenco dei <b>nomi</b> dei campi modificati.
/// </summary>
public class AuditLogEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Utente che ha eseguito l'operazione (dal token), se noto.</summary>
    public Guid? UtenteId { get; set; }

    /// <summary>Inserimento · Modifica · Cancellazione.</summary>
    public string Azione { get; set; } = "";

    /// <summary>Nome della tabella/entità interessata (es. "Dipendenti").</summary>
    public string Entita { get; set; } = "";

    /// <summary>Chiave primaria del record interessato (come stringa).</summary>
    public string ChiaveEntita { get; set; } = "";

    /// <summary>Per le modifiche: nomi dei campi cambiati, separati da virgola (mai i valori).</summary>
    public string? CampiModificati { get; set; }

    public DateTime QuandoUtc { get; set; }
}
