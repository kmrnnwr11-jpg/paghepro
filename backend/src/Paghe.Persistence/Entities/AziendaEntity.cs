namespace Paghe.Persistence.Entities;

/// <summary>Azienda (datore di lavoro / soggetto emittente) persistita su database.</summary>
public class AziendaEntity
{
    private string? _iban;

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

    /// <summary>"Artigiano", "Commerciante", "Professionista", "Societa".</summary>
    public string? TipoContribuente { get; set; }
    public bool AssoggettataIva { get; set; } = true;

    public List<FatturaEntity> Fatture { get; set; } = new();
}
