namespace Paghe.Persistence.Entities;

/// <summary>
/// Versione append-only di una policy di conservazione. L'assenza di righe equivale a policy
/// disattivata: PaghePro non inventa durate legali né esegue cancellazioni per impostazione iniziale.
/// </summary>
public class PolicyRetentionEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Categoria { get; set; } = "";
    public int Versione { get; set; }
    public int? GiorniConservazione { get; set; }
    public bool Attiva { get; set; }
    public bool Approvata { get; set; }
    public string? BaseGiuridica { get; set; }
    public string? RiferimentoDecisione { get; set; }
    public string? IstruzioniBackup { get; set; }
    public string Finalita { get; set; } = "";
    public string Decorrenza { get; set; } = "";
    public string Azione { get; set; } = "";
    public DateTime CreataUtc { get; set; }
    public Guid CreataDaUtenteId { get; set; }
}

/// <summary>
/// Sospende la retention per un'intera categoria del tenant. Il motivo è un codice controllato:
/// non si copiano nel database dettagli di contenziosi, persone o documenti.
/// </summary>
public class BloccoConservazioneEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Categoria { get; set; } = "";
    public string MotivoCodice { get; set; } = "";
    public DateTime CreatoUtc { get; set; }
    public Guid CreatoDaUtenteId { get; set; }
    public DateTime? RevocatoUtc { get; set; }
    public Guid? RevocatoDaUtenteId { get; set; }
}

/// <summary>
/// Snapshot tecnico, privo di dati personali, che vincola conferma ed esecuzione agli stessi
/// conteggi, policy e blocchi osservati dall'amministratore.
/// </summary>
public class AnteprimaRetentionEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Categoria { get; set; } = "";
    public int VersionePolicy { get; set; }
    public DateTime SogliaUtc { get; set; }
    public int Candidati { get; set; }
    public int Bloccati { get; set; }
    public int Elaborabili { get; set; }
    public string ImprontaCandidati { get; set; } = "";
    public string ImprontaBlocchi { get; set; } = "";
    public DateTime CreataUtc { get; set; }
    public DateTime ScadeUtc { get; set; }
    public Guid CreataDaUtenteId { get; set; }
    public DateTime? ConsumataUtc { get; set; }
}

/// <summary>
/// Ricevuta append-only dell'esecuzione. Contiene solo metadati e conteggi aggregati, mai valori
/// dei record eliminati. La catena di digest rende verificabili alterazioni accidentali.
/// </summary>
public class EsecuzioneRetentionEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AnteprimaId { get; set; }
    public Guid ChiaveIdempotenza { get; set; }
    public string Categoria { get; set; } = "";
    public int VersionePolicy { get; set; }
    public DateTime SogliaUtc { get; set; }
    public int CandidatiAnteprima { get; set; }
    public int Bloccati { get; set; }
    public int Elaborati { get; set; }
    public int RestantiStimati { get; set; }
    public string Esito { get; set; } = "Completata";
    public DateTime IniziataUtc { get; set; }
    public DateTime CompletataUtc { get; set; }
    public Guid EseguitaDaUtenteId { get; set; }
    public string? CorrelationId { get; set; }
    public string? DigestPrecedente { get; set; }
    public string Digest { get; set; } = "";
}
