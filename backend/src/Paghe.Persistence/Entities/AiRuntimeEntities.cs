namespace Paghe.Persistence.Entities;

/// <summary>
/// Un turno del runtime Aurora (AiRun del master plan): quanto è entrato nel contesto del
/// modello è riconciliabile da qui, ma per digest e conteggi — mai per contenuto. Il testo
/// dei messaggi non viene conservato (piano limiti §8.2: la chat completa non si conserva);
/// i dati letti sono già nella catena audit del Governor.
/// </summary>
public class AiRunEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }

    /// <summary>Mandato di sessione usato dal turno (null solo nei turni senza strumenti).</summary>
    public Guid? GrantId { get; set; }

    /// <summary>Etichetta della schermata corrente, già normalizzata (mai testo libero).</summary>
    public string? SezioneUi { get; set; }

    /// <summary>Modello dichiarato dal client AI al momento del turno.</summary>
    public string Modello { get; set; } = "";

    public DateTime InizioUtc { get; set; }
    public DateTime? FineUtc { get; set; }

    /// <summary>Giri di conversazione col modello effettuati nel turno.</summary>
    public int Giri { get; set; }

    /// <summary>Token consumati secondo il provider, quando dichiarati nella risposta.</summary>
    public int? TokenTotali { get; set; }

    /// <summary>Ok · Proposta · Checkpoint · Revocato · Guardia · Errore.</summary>
    public string Esito { get; set; } = "";

    public string? Motivo { get; set; }

    /// <summary>SHA-256 della sequenza completa dei messaggi visti dal modello (model-visible ⟺ logged).</summary>
    public string DigestMessaggi { get; set; } = "";
}

/// <summary>
/// Una chiamata a strumento dentro un run (ToolCall del master plan): nome, hash degli
/// argomenti, esito e durata. Gli argomenti stessi non vengono duplicati qui.
/// </summary>
public class AiToolCallEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }

    /// <summary>Posizione progressiva della chiamata nel turno (1-based).</summary>
    public int Ordine { get; set; }

    /// <summary>Nome dello strumento invocato; troncato se il modello ne inventa uno.</summary>
    public string Strumento { get; set; } = "";

    public string ArgomentiHash { get; set; } = "";

    /// <summary>Ok · Proposta · Negato · Sconosciuto · Ripetuto · Timeout.</summary>
    public string Esito { get; set; } = "";

    public string? Motivo { get; set; }
    public int DurataMs { get; set; }

    /// <summary>Righe restituite dal broker (0 per dinieghi e scritture).</summary>
    public int Righe { get; set; }
}

/// <summary>
/// Proposta di scrittura preparata dal runtime e in attesa della conferma dell'utente
/// (piano Aurora §3.2): il server conserva argomenti e diff, il browser rimanda soltanto il
/// token firmato. Argomenti ed esito possono contenere dati personali: sono cifrati con AAD
/// legata a tenant e proposta, come gli altri payload protetti.
/// </summary>
public class AiPropostaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public Guid? GrantId { get; set; }
    public Guid? RunId { get; set; }

    public string Strumento { get; set; } = "";

    /// <summary>Argomenti JSON cifrati: sono l'unica fonte al momento dell'applicazione.</summary>
    public string ArgomentiProtetti { get; set; } = "";

    /// <summary>SHA-256 degli argomenti in chiaro: lega il token a ciò che l'utente ha visto.</summary>
    public string ArgomentiHash { get; set; } = "";

    public string Titolo { get; set; } = "";

    /// <summary>Diff leggibile (righe etichetta/da/a) serializzato, mostrato prima della conferma.</summary>
    public string DiffJson { get; set; } = "";

    public DateTime CreataUtc { get; set; }
    public DateTime ScadeUtc { get; set; }

    /// <summary>InAttesa · InApplicazione · Applicata · Annullata · Scaduta.</summary>
    public string Stato { get; set; } = "InAttesa";

    public DateTime? ApplicataUtc { get; set; }

    /// <summary>Esito dell'applicazione, cifrato (può citare dati del record creato).</summary>
    public string? EsitoProtetto { get; set; }

    /// <summary>Token di concorrenza: una proposta non si applica mai due volte in parallelo.</summary>
    public int Versione { get; set; }
}
