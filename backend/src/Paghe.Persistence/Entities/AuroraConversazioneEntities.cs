namespace Paghe.Persistence.Entities;

/// <summary>
/// Scelta esplicita dell'utente di conservare le conversazioni Aurora recenti. Non coincide con
/// il consenso agli strumenti AI: abilita soltanto il workspace cifrato per massimo 24 ore.
/// </summary>
public class AuroraCronologiaPreferenzaEntity
{
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public bool Attiva { get; set; }
    public DateTime AggiornataUtc { get; set; }
    public int Versione { get; set; }
}

/// <summary>
/// Filo di lavoro recente di Aurora. Lo scope è immutabile: cambiare azienda, rapporto o progetto
/// crea un altro filo e impedisce che dati appartenenti a contesti diversi vengano mescolati.
/// Titolo ed etichetta progetto sono cifrati manualmente con AAD tenant/utente/conversazione.
/// </summary>
public class AuroraConversazioneEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public string TipoContesto { get; set; } = "Generale";
    public Guid? AziendaId { get; set; }
    public Guid? RapportoId { get; set; }
    public string? Periodo { get; set; }
    public string TitoloProtetto { get; set; } = "";
    public string? ProgettoProtetto { get; set; }
    public DateTime CreataUtc { get; set; }
    public DateTime AggiornataUtc { get; set; }
    public DateTime ScadeUtc { get; set; }
    public DateTime? ArchiviataUtc { get; set; }
    public int Versione { get; set; }
}

/// <summary>
/// Prenotazione idempotente di un turno. Il ClientTurnId nasce nel browser e non può essere
/// riutilizzato nella stessa conversazione, anche in caso di doppio click o retry di rete.
/// </summary>
public class AuroraTurnoEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public Guid ConversazioneId { get; set; }
    public Guid ClientTurnId { get; set; }
    public string Stato { get; set; } = "InCorso";
    public DateTime CreatoUtc { get; set; }
    public DateTime? CompletatoUtc { get; set; }
    public int Versione { get; set; }
}

/// <summary>
/// Messaggio visibile del workspace. Contiene solo ruolo e ciphertext: mai prompt di sistema,
/// selezioni, allegati, output tool, lease, token di proposta o thought signature.
/// </summary>
public class AuroraMessaggioEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public Guid ConversazioneId { get; set; }
    public Guid TurnoId { get; set; }
    public int Sequenza { get; set; }
    public string Ruolo { get; set; } = "";
    public string ContenutoProtetto { get; set; } = "";
    public DateTime CreatoUtc { get; set; }
}
