namespace Paghe.Persistence.Entities;

/// <summary>Azienda (datore di lavoro / soggetto emittente) persistita su database.</summary>
public class AziendaEntity
{
    private string? _iban;
    private byte[]? _firmaSiglaTimbro;

    public Guid Id { get; set; }

    /// <summary>Tenant proprietario del dato (isolamento multi-tenant).</summary>
    public Guid TenantId { get; set; }

    public string RagioneSociale { get; set; } = "";
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }

    public string Indirizzo { get; set; } = "";
    public string Cap { get; set; } = "";
    public string Comune { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Nazione { get; set; } = "IT";
    public string RegimeFiscale { get; set; } = "RF01";

    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Pec { get; set; }
    public string? CodiceDestinatarioSdi { get; set; }
    public string? CodiceCatastale { get; set; }
    public string? Settore { get; set; }
    public string? CodiceAteco { get; set; }
    public string? MatricolaInps { get; set; }
    public string? CodiceSedeInps { get; set; }
    public string? CscInps { get; set; }
    /// <summary>Codici di autorizzazione INPS normalizzati, separati da virgola; "NESSUNO" e esplicito.</summary>
    public string? CodiciAutorizzazioneInps { get; set; }
    /// <summary>Codice del fondo di integrazione applicato (es. FIS) o "NESSUNO".</summary>
    public string? CodiceFondoIntegrazioneSalariale { get; set; }
    /// <summary>Agevolazione contributiva applicata o "NESSUNA".</summary>
    public string? CodiceAgevolazioneContributiva { get; set; }
    public string? PatInail { get; set; }
    public string? CcnlDefault { get; set; }
    public string? LivelloDefault { get; set; }
    /// <summary>IBAN disponibile al dominio; la proprietà non è mappata direttamente nel DB.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Iban
    {
        get => _iban;
        set
        {
            if (string.Equals(_iban, value, StringComparison.Ordinal)) return;
            _iban = value;
            IbanModificato = true;
        }
    }

    /// <summary>Envelope AES-GCM tenant-bound persistito al posto dell'IBAN in chiaro.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? IbanProtetto { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool IbanModificato { get; private set; }

    internal void CaricaIbanDecifrato(string? valore)
    {
        _iban = valore;
        IbanModificato = false;
    }

    internal void ConfermaProtezioneIban() => IbanModificato = false;
    public string? LegaleRappresentanteNome { get; set; }
    public string? LegaleRappresentanteCognome { get; set; }
    public string? LegaleRappresentanteCodiceFiscale { get; set; }
    /// <summary>null = usa il default globale (Manuale). Valori: "Email", "Sms", "WhatsApp".</summary>
    public string? CanalePredefinito { get; set; }

    /// <summary>Logo aziendale (PNG o JPEG) mostrato sui documenti, es. la busta paga. Facoltativo.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[]? Logo { get; set; }

    /// <summary>
    /// Immagine autentica caricata dal datore per apporre firma, sigla o timbro sul prospetto.
    /// Non è una firma digitale qualificata e non viene mai creata automaticamente dal software.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[]? FirmaSiglaTimbro
    {
        get => _firmaSiglaTimbro;
        set
        {
            _firmaSiglaTimbro = value;
            FirmaSiglaTimbroModificata = true;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? FirmaSiglaTimbroProtetta { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool FirmaSiglaTimbroModificata { get; private set; }

    internal void CaricaFirmaSiglaTimbroDecifrata(byte[]? valore)
    {
        _firmaSiglaTimbro = valore;
        FirmaSiglaTimbroModificata = false;
    }

    internal void ConfermaProtezioneFirmaSiglaTimbro() => FirmaSiglaTimbroModificata = false;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HaFirmaSiglaTimbro => _firmaSiglaTimbro is { Length: > 0 };

    public bool HaDipendenti { get; set; }

    /// <summary>
    /// L'azienda supera i 15 dipendenti e rientra nella CIG: la quota INPS a carico del dipendente
    /// è maggiorata dello 0,30% (es. 9,19% → 9,49%). Sta sull'azienda, non sul singolo rapporto:
    /// è una sua caratteristica, e va dichiarata perché il conteggio di legge (media del semestre,
    /// part-time pro quota, apprendisti esclusi) non si deduce dal numero di rapporti in archivio.
    /// </summary>
    public bool OltreQuindiciDipendenti { get; set; }

    /// <summary>
    /// Fondi di settore a carico del dipendente in aggiunta alla quota INPS base, come frazione
    /// (0,002667 = 0,2667%). Verificati su buste reali: FIS 0,2667% nel Multiservizi,
    /// FSBS 0,15% nella somministrazione. Zero se il CCNL non ne prevede.
    /// </summary>
    public decimal ContributiFondiSettore { get; set; }

    /// <summary>
    /// Aliquota INPS complessiva a carico del datore secondo il profilo contributivo reale
    /// dell'azienda (CSC, codici autorizzazione, media dipendenti, FIS/CIGS), come frazione
    /// (0,293133 = 29,3133%). La determina il professionista: nessuna percentuale datoriale
    /// va cablata nel codice (revisione consulente 24/08/2026). Null = tabella per qualifica,
    /// che resta un riferimento non certificato.
    /// </summary>
    public decimal? AliquotaInpsDatore { get; set; }

    /// <summary>Aliquota c/dipendente risultante dal profilo CSC/CA/organico/FIS/agevolazioni.</summary>
    public decimal? AliquotaInpsDipendente { get; set; }

    /// <summary>Fonte e data del profilo contributivo verificato; impedisce aliquote senza provenienza.</summary>
    public string? FonteProfiloContributivo { get; set; }

    /// <summary>Riferimento della visura camerale usata per verificare attivita prevalente e CCNL.</summary>
    public string? RiferimentoVisuraCamerale { get; set; }

    /// <summary>DaVerificare, Iscritta o NonIscritta. H011 richiede la verifica esplicita.</summary>
    public string StatoAlboArtigiani { get; set; } = "DaVerificare";

    /// <summary>Nota/fonte della verifica di applicabilita del CCNL all'attivita prevalente.</summary>
    public string? RiferimentoVerificaApplicabilitaCcnl { get; set; }

    /// <summary>Fonte territoriale del terzo elemento (necessaria se diverso dal nazionale).</summary>
    public string? RiferimentoTerzoElementoTerritoriale { get; set; }

    /// <summary>
    /// L'azienda aderisce ai fondi contrattuali del CCNL (assistenza sanitaria, ente
    /// bilaterale). Se no, le quote non vanno in busta; gli eventuali elementi retributivi
    /// sostitutivi previsti dal contratto restano da gestire a parte.
    /// </summary>
    public bool AderisceFondiContrattuali { get; set; } = true;

    /// <summary>
    /// Default aziendale del reddito annuo presunto degli operai agricoli OTD: vale per i
    /// rapporti che non lo indicano in scheda. Null = nessun default, si stima dal contratto.
    /// </summary>
    public decimal? RedditoPresuntoAgricoloAnnuo { get; set; }

    /// <summary>"Artigiano", "Commerciante", "Professionista", "Societa".</summary>
    public string? TipoContribuente { get; set; }
    public bool AssoggettataIva { get; set; } = true;

    public List<FatturaEntity> Fatture { get; set; } = new();
}
