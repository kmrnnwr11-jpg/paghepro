namespace Paghe.Persistence.Entities;

/// <summary>Lavoratore (persona fisica), persistito.</summary>
public class DipendenteEntity
{
    private string? _iban;

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CodiceFiscale { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Cognome { get; set; } = "";
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

    /// <summary>GDPR (diritto all'oblio): true se i dati personali sono stati anonimizzati.</summary>
    public bool Anonimizzato { get; set; }

    public List<RapportoLavoroEntity> Rapporti { get; set; } = new();
}

/// <summary>Rapporto di lavoro persistito: collega dipendente e azienda con l'inquadramento.</summary>
public class RapportoLavoroEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public Guid DipendenteId { get; set; }
    public DipendenteEntity? Dipendente { get; set; }

    public string? Matricola { get; set; }
    public string CodiceCcnl { get; set; } = "";
    public string Livello { get; set; } = "";
    public string Qualifica { get; set; } = "Impiegato"; // nome enum Qualifica
    public string TipoContratto { get; set; } = "Tempo indeterminato";
    public decimal OreSettimanali { get; set; } = 40m;
    public int NumeroMensilita { get; set; } = 14;
    public bool PartTime { get; set; }
    public decimal PercentualePartTime { get; set; } = 100m;
    public int ScattiAnzianita { get; set; }
    public decimal Superminimo { get; set; }
    public string Regione { get; set; } = "";
    public string CodiceCatastaleComune { get; set; } = "";
    public bool DetrazioniLavoroDipendente { get; set; } = true;
    public int GiorniDetrazioneAnno { get; set; } = 365;
    public DateOnly DataAssunzione { get; set; }

    /// <summary>Fine del rapporto (dimissioni/licenziamento); null = ancora in forza.</summary>
    public DateOnly? DataCessazione { get; set; }

    /// <summary>Fondo TFR maturato prima dell'uso di questo software (per la rivalutazione).</summary>
    public decimal? FondoTfrIniziale { get; set; }
}

/// <summary>Cartellino presenze mensile persistito.</summary>
public class CartellinoEntity
{
    private decimal _giorniMalattia;
    private int _numeroEventoMalattiaAnno = 1;
    private decimal _giorniInfortunio;

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RapportoLavoroId { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }
    public decimal OreStraordinario { get; set; }
    public decimal OreStraordinarioNotturno { get; set; }
    public decimal OreStraordinarioFestivo { get; set; }
    public decimal GiorniFerie { get; set; }
    public decimal OrePermessi { get; set; }
    public decimal OreExFestivita { get; set; }
    /// <summary>Dato salute disponibile al dominio, escluso dalla mappatura diretta.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal GiorniMalattia
    {
        get => _giorniMalattia;
        set
        {
            if (_giorniMalattia == value) return;
            _giorniMalattia = value;
            DatiSaluteModificati = true;
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int NumeroEventoMalattiaAnno
    {
        get => _numeroEventoMalattiaAnno;
        set
        {
            if (_numeroEventoMalattiaAnno == value) return;
            _numeroEventoMalattiaAnno = value;
            DatiSaluteModificati = true;
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal GiorniInfortunio
    {
        get => _giorniInfortunio;
        set
        {
            if (_giorniInfortunio == value) return;
            _giorniInfortunio = value;
            DatiSaluteModificati = true;
        }
    }

    /// <summary>Payload fisso con la terna salute, cifrato e tenant-bound.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DatiSaluteProtetti { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool DatiSaluteModificati { get; private set; }

    internal void CaricaDatiSalute(decimal malattia, int evento, decimal infortunio)
    {
        _giorniMalattia = malattia;
        _numeroEventoMalattiaAnno = evento;
        _giorniInfortunio = infortunio;
        DatiSaluteModificati = false;
    }

    internal void ConfermaProtezioneDatiSalute() => DatiSaluteModificati = false;
    public decimal OreAssenzaNonRetribuita { get; set; }
}

/// <summary>Cedolino calcolato e archiviato (testata con i totali).</summary>
public class CedolinoEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public Guid RapportoLavoroId { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }

    public decimal TotaleCompetenze { get; set; }
    public decimal TotaleTrattenute { get; set; }
    public decimal ImponibileInps { get; set; }
    public decimal ContributiDipendente { get; set; }
    public decimal ContributiDatore { get; set; }
    public decimal ImponibileIrpef { get; set; }
    public decimal IrpefNetta { get; set; }
    public decimal AddizionaleRegionale { get; set; }
    public decimal AddizionaleComunale { get; set; }
    public decimal NettoInBusta { get; set; }
    public decimal QuotaTfr { get; set; }
    public decimal CostoAzienda { get; set; }

    public decimal FerieMaturate { get; set; }
    public decimal FerieGodute { get; set; }
    public decimal FerieResidue { get; set; }

    public decimal PermessiMaturati { get; set; }
    public decimal PermessiGoduti { get; set; }
    public decimal PermessiResidui { get; set; }

    /// <summary>Conguaglio IRPEF applicato in busta (solo dicembre; >0 a debito, &lt;0 a credito).</summary>
    public decimal ConguaglioIrpef { get; set; }

    public DateTime CalcolatoIl { get; set; }
    public List<CedolinoVoceEntity> Voci { get; set; } = new();
}

/// <summary>Riga (voce) di un cedolino archiviato.</summary>
public class CedolinoVoceEntity
{
    private string _codice = "";
    private string _descrizione = "";
    private string _tipo = "";
    private decimal? _quantita;
    private decimal? _base;
    private decimal _importo;

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CedolinoId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Codice { get => _codice; set { _codice = value; DatiVoceModificati = true; } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Descrizione { get => _descrizione; set { _descrizione = value; DatiVoceModificati = true; } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Tipo { get => _tipo; set { _tipo = value; DatiVoceModificati = true; } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal? Quantita { get => _quantita; set { _quantita = value; DatiVoceModificati = true; } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal? Base { get => _base; set { _base = value; DatiVoceModificati = true; } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal Importo { get => _importo; set { _importo = value; DatiVoceModificati = true; } }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DatiVoceProtetti { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool DatiVoceModificati { get; private set; }

    internal void CaricaDatiVoce(
        string codice,
        string descrizione,
        string tipo,
        decimal? quantita,
        decimal? @base,
        decimal importo)
    {
        _codice = codice;
        _descrizione = descrizione;
        _tipo = tipo;
        _quantita = quantita;
        _base = @base;
        _importo = importo;
        DatiVoceModificati = false;
    }

    internal void ConfermaProtezioneDatiVoce() => DatiVoceModificati = false;
}
