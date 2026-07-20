namespace Paghe.Persistence.Entities;

/// <summary>
/// Riga append-only del registro attività (GDPR / tracciabilità): registra <b>chi</b> ha fatto
/// <b>cosa</b>, <b>quando</b> e con quale esito su un dato tenant. Per minimizzare i dati non salva
/// mai corpi, query string, valori dei campi, indirizzo IP o user-agent.
/// </summary>
public class AuditLogEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Utente che ha eseguito l'operazione (dal token), se noto.</summary>
    public Guid? UtenteId { get; set; }

    /// <summary>Accesso (richiesta API) oppure ModificaDati (interceptor EF).</summary>
    public string Categoria { get; set; } = "ModificaDati";

    /// <summary>Inserimento · Modifica · Cancellazione.</summary>
    public string Azione { get; set; } = "";

    /// <summary>Nome della tabella/entità interessata (es. "Dipendenti").</summary>
    public string Entita { get; set; } = "";

    /// <summary>Chiave primaria del record interessato (come stringa).</summary>
    public string ChiaveEntita { get; set; } = "";

    /// <summary>Per le modifiche: nomi dei campi cambiati, separati da virgola (mai i valori).</summary>
    public string? CampiModificati { get; set; }

    /// <summary>Metodo HTTP per gli accessi; null per gli eventi generati dalla persistenza.</summary>
    public string? MetodoHttp { get; set; }

    /// <summary>Template della rotta, mai il percorso reale né la query string.</summary>
    public string? PercorsoTemplate { get; set; }

    /// <summary>Riuscito, Negato, SecondoFattoreRichiesto, NonTrovato, Rifiutato, Limitato o Errore.</summary>
    public string? Esito { get; set; }

    /// <summary>Codice HTTP degli accessi; null per gli eventi di persistenza.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Identificatore tecnico per correlare accesso e modifiche della stessa richiesta.</summary>
    public string? CorrelationId { get; set; }

    public DateTime QuandoUtc { get; set; }
}
