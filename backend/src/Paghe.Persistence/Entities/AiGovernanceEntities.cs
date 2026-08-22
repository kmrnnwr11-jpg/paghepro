namespace Paghe.Persistence.Entities;

/// <summary>
/// Mandato AI (AiScopeGrantRecord del piano G2): registra server-side cosa una singola
/// esecuzione AI può vedere e fare, per quanto tempo e con quali quote. Il modello non vede
/// mai questo record: riceve solo il manifest descrittivo; i tool presentano la lease opaca.
/// Nessun campo contiene dati professionali: solo nomi di risorse del catalogo e contatori.
/// </summary>
public class AiScopeGrantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Operatore che ha concesso il mandato (dal token).</summary>
    public Guid UtenteId { get; set; }

    /// <summary>Finalità dichiarata, dall'elenco chiuso della policy (G0).</summary>
    public string PurposeCode { get; set; } = "";

    /// <summary>Stretta | Guidata (Workspace e Programmata arrivano con G4/G6).</summary>
    public string Modalita { get; set; } = "Guidata";

    /// <summary>S0 | S1 | S2 (S3/S4 non concedibili in G2).</summary>
    public string ScopeLevel { get; set; } = "S1";

    /// <summary>Azienda del mandato (obbligatoria per S1/S2).</summary>
    public Guid? AziendaId { get; set; }

    /// <summary>Periodo del mandato in formato AAAA-MM (per S2).</summary>
    public string? Periodo { get; set; }

    /// <summary>Nomi delle risorse del catalogo raggiungibili, separati da virgola.</summary>
    public string RisorseAmmesse { get; set; } = "";

    /// <summary>Operazioni concesse, separate da virgola: read, search, draft.</summary>
    public string OperazioniAmmesse { get; set; } = "";

    /// <summary>
    /// Coppie esatte risorsa|operazione separate da virgola. Evita che un grant misto
    /// trasformi, per prodotto cartesiano, una risorsa di sola lettura in scrivibile.
    /// </summary>
    public string PermessiAmmessi { get; set; } = "";

    /// <summary>
    /// Identità esatte nome@versione dei tool A3. È valorizzato dal server, mai dal prompt
    /// o dal browser, così un futuro tool sulla stessa risorsa non eredita questo mandato.
    /// </summary>
    public string StrumentiAmmessi { get; set; } = "";

    /// <summary>
    /// ATTIVA soltanto per un mandato A3 spendibile. L'indice univoco obbliga a revocare
    /// consapevolmente il pilot precedente prima di emetterne un altro.
    /// </summary>
    public string? SlotA3Attivo { get; set; }

    /// <summary>A1 (lettura/ricerca) o A2 (anche bozze). A3+ non concedibili in G2.</summary>
    public string AutonomiaMax { get; set; } = "A1";

    /// <summary>Versione della policy che ha autorizzato l'emissione (audit/riproducibilità).</summary>
    public string PolicyVersione { get; set; } = "";

    /// <summary>Anti-replay: la lease vale solo se porta questo nonce.</summary>
    public string Nonce { get; set; } = "";

    public DateTime CreatoUtc { get; set; }
    public DateTime ScadeUtc { get; set; }

    /// <summary>Minuti di inattività oltre i quali il mandato non è più utilizzabile.</summary>
    public int TimeoutInattivitaMinuti { get; set; }

    public DateTime UltimoUsoUtc { get; set; }

    /// <summary>Quota di letture (record/documenti) concessa; il checkpoint scatta al limite.</summary>
    public int MaxLetture { get; set; }

    public int LettureUsate { get; set; }

    /// <summary>Quote distinte: preparare una proposta non consuma né nasconde letture.</summary>
    public int MaxProposte { get; set; }
    public int ProposteUsate { get; set; }

    /// <summary>Effetti reversibili realmente confermati dall'utente.</summary>
    public int MaxEffetti { get; set; }
    public int EffettiUsati { get; set; }

    /// <summary>Attivo | Revocato | Scaduto.</summary>
    public string Stato { get; set; } = "Attivo";

    public DateTime? RevocatoUtc { get; set; }

    /// <summary>Token di concorrenza: i contatori si aggiornano in modo atomico (retry su conflitto).</summary>
    public int Versione { get; set; }
}

/// <summary>
/// Audit AI append-only e tamper-evident (catena hash per tenant): ogni emissione, accesso,
/// diniego e revoca è una riga concatenata alla precedente. Non duplica mai contenuti
/// professionali: solo nomi di risorse, operazioni, quantità e motivi.
/// </summary>
public class AiAuditEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Progressivo per tenant (unico): l'ordine è parte della catena.</summary>
    public long Sequenza { get; set; }

    public Guid? GrantId { get; set; }
    public Guid? UtenteId { get; set; }

    /// <summary>Preflight · EmissioneGrant · Accesso · Diniego · Revoca · ArrestoTotale.</summary>
    public string Evento { get; set; } = "";

    /// <summary>Descrizione semantica (risorsa, operazione, quantità), mai payload o valori.</summary>
    public string Dettaglio { get; set; } = "";

    /// <summary>Per i dinieghi: la ragione esatta della bocciatura.</summary>
    public string? Motivo { get; set; }

    public DateTime QuandoUtc { get; set; }

    /// <summary>Hash della riga precedente della catena del tenant ("GENESI" per la prima).</summary>
    public string HashPrecedente { get; set; } = "";

    /// <summary>SHA-256 esadecimale dei campi canonici + hash precedente.</summary>
    public string Hash { get; set; } = "";
}
