namespace Paghe.Persistence.Entities;

/// <summary>
/// Casella email o PEC collegata da un tenant (IMAP, funziona anche coi provider PEC italiani
/// che non offrono OAuth). La password è un segreto: cifrata a riposo, mai restituita in chiaro
/// dall'API. Più caselle per tenant sono ammesse (es. una email + una PEC).
/// </summary>
public class CasellaEmailEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Etichetta { get; set; } = "";

    /// <summary>"Email" oppure "Pec".</summary>
    public string Tipo { get; set; } = "Email";

    public string Host { get; set; } = "";
    public int Porta { get; set; } = 993;
    public string Utente { get; set; } = "";
    public string? Password { get; set; }
    public bool Ssl { get; set; } = true;
    public bool Attiva { get; set; } = true;

    /// <summary>Ultimo UID IMAP sincronizzato nella INBOX: evita di rileggere tutta la casella a ogni sync.</summary>
    public long? UltimoUidSincronizzato { get; set; }

    // DateTime (UTC), non DateTimeOffset: EF Core su SQLite non traduce ORDER BY su DateTimeOffset.
    public DateTime CreataIl { get; set; }
    public DateTime? UltimaSincronizzazioneIl { get; set; }
}

/// <summary>
/// Messaggio importato da una casella email/PEC. Il triage (urgente/pubblicità) è sempre una
/// PROPOSTA finché l'utente non la conferma esplicitamente — mai applicata in automatico, per
/// restare dentro il perimetro AI approvato (Email/PEC = solo proposta, mai azione autonoma).
/// </summary>
public class MessaggioEmailEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CasellaId { get; set; }

    /// <summary>UID IMAP nella INBOX di origine: dedup alla risincronizzazione.</summary>
    public long UidImap { get; set; }

    public string Mittente { get; set; } = "";
    public string Oggetto { get; set; } = "";
    // DateTime (UTC): serve l'ORDER BY per data, non tradotto da EF Core su SQLite per DateTimeOffset.
    public DateTime DataRicezione { get; set; }

    /// <summary>Anteprima breve non cifrata (poche righe), utile per l'elenco senza decifrare il corpo.</summary>
    public string Anteprima { get; set; } = "";

    public bool Letto { get; set; }

    /// <summary>Esito confermato dall'utente. Null = non ancora deciso.</summary>
    public bool? Urgente { get; set; }
    public bool? Pubblicita { get; set; }

    /// <summary>Proposta in attesa di conferma (euristica o AI) — non ancora applicata.</summary>
    public bool? UrgenteProposto { get; set; }
    public bool? PubblicitaProposto { get; set; }
    public decimal? ConfidenzaUrgente { get; set; }
    public decimal? ConfidenzaPubblicita { get; set; }

    /// <summary>"Euristica" o "Ai": da dove viene la proposta corrente, per trasparenza in UI.</summary>
    public string? FonteProposta { get; set; }

    public string? Corpo { get; set; }
}
