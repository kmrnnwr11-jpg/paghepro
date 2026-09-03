using Paghe.Core.Domain;

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

    /// <summary>
    /// Sesso anagrafico "M"/"F" (null = non indicato). Se presente viene verificato contro il
    /// codice fiscale al salvataggio: un giorno di nascita oltre 40 identifica una donna, e
    /// un'anagrafica incoerente va fermata prima di finire su LUL e denunce.
    /// </summary>
    public string? Sesso { get; set; }
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

    /// <summary>
    /// Netto mensile pattuito col lavoratore («netto concordato»): a ogni elaborazione il motore
    /// ricalcola il superminimo che lo produce, mai sotto il minimo CCNL. Null = paga dal lordo.
    /// </summary>
    public decimal? NettoConcordatoMensile { get; set; }

    /// <summary>
    /// Mese da cui vale il netto concordato (normalizzato al primo giorno). Mai retroattivo:
    /// i periodi precedenti restano calcolati dal lordo, le buste passate non si ricostruiscono.
    /// </summary>
    public DateOnly? NettoConcordatoDecorrenza { get; set; }

    /// <summary>
    /// Reddito annuo presunto del rapporto (usato per gli agricoli: aliquota media IRPEF,
    /// detrazioni, trattamento integrativo). Null = vale il default aziendale, altrimenti
    /// la stima dal contratto; l'origine del valore usato resta tracciata sulla busta.
    /// </summary>
    public decimal? RedditoPresuntoAnnuo { get; set; }

    public string Regione { get; set; } = "";
    public string CodiceCatastaleComune { get; set; } = "";

    /// <summary>Decorrenza del domicilio fiscale usato per le addizionali.</summary>
    public DateOnly? DataDecorrenzaDomicilioFiscale { get; set; }

    /// <summary>Fonte dei progressivi fiscali importati quando non sono tutti nell'archivio PaghePro.</summary>
    public string? RiferimentoProgressiviFiscaliPregressi { get; set; }

    /// <summary>
    /// Sigla della provincia dell'unità operativa dove il lavoratore presta servizio, per gli
    /// elementi provinciali del CCNL (es. terzo elemento del Terziario). Null = vale la
    /// provincia della sede aziendale.
    /// </summary>
    public string? ProvinciaLavoro { get; set; }
    // ---- Inquadramento contributivo per la denuncia UniEmens -------------------------
    // Dati che il tracciato INPS chiede sul lavoratore e che non si ricavano dal cedolino.
    // Restano nulli finché non li fornisce chi tiene l'anagrafica: la denuncia li dichiara
    // mancanti invece di scrivere un valore plausibile.

    /// <summary>Tipo di part-time: "P" orizzontale, "V" verticale, "M" misto (Qualifica2).</summary>
    public string? TipoPartTimeInps { get; set; }

    /// <summary>Cittadinanza nella codifica delle Comunicazioni Obbligatorie (tre cifre).</summary>
    public string? CittadinanzaInps { get; set; }

    /// <summary>Qualifica professionale ISTAT, es. "8.1.3.3.0.2" (elemento opzionale).</summary>
    public string? QualificaProfessionaleIstat { get; set; }

    /// <summary>Tipo di contribuzione INPS per i casi particolari (apprendisti, intermittenti).</summary>
    public string? TipoContribuzioneInps { get; set; }

    public bool DetrazioniLavoroDipendente { get; set; } = true;
    public int GiorniDetrazioneAnno { get; set; } = 365;
    public DateOnly DataAssunzione { get; set; }

    /// <summary>Fine del rapporto (dimissioni/licenziamento); null = ancora in forza.</summary>
    public DateOnly? DataCessazione { get; set; }

    // ---- Addizionali da conguaglio dell'anno precedente ------------------------------
    // Caricate dal professionista (dal conguaglio o dal cedolino di dicembre): con questi
    // importi il motore trattiene le rate vere invece dell'anteprima presuntiva mensile.

    /// <summary>Addizionale regionale dovuta sul reddito dell'anno precedente (annuo da rateizzare in 11 rate).</summary>
    public decimal? AddizionaleRegionaleConguaglioAnnuo { get; set; }

    /// <summary>Saldo dell'addizionale comunale dell'anno precedente (annuo da rateizzare in 11 rate).</summary>
    public decimal? AddizionaleComunaleSaldoAnnuo { get; set; }

    /// <summary>Acconto 30% dell'addizionale comunale dell'anno corrente (da rateizzare in 9 rate da marzo).</summary>
    public decimal? AddizionaleComunaleAccontoAnnuo { get; set; }

    /// <summary>Fondo TFR maturato prima dell'uso di questo software (per la rivalutazione).</summary>
    public decimal? FondoTfrIniziale { get; set; }

    /// <summary>Riporto ferie disponibile al 1° gennaio, in giorni.</summary>
    public decimal FerieResidueIniziali { get; set; }

    /// <summary>Riporto ROL/ex festività disponibile al 1° gennaio, in ore.</summary>
    public decimal PermessiResiduiIniziali { get; set; }

    public decimal? RolResiduiIniziali { get; set; }
    public decimal? ExFestivitaResidueIniziali { get; set; }
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

    /// <summary>
    /// Versione ottimistica della bozza: una proposta Aurora non può sovrascrivere un
    /// cartellino corretto nel frattempo dalla pagina Presenze.
    /// </summary>
    public int Versione { get; set; } = 1;

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

    // ---- Calendario giornaliero -----------------------------------------------------
    // Dice quali giorni sono stati malattia o infortunio, quindi è dato sanitario quanto la
    // terna qui sopra: sta in un payload cifrato, non in una tabella figlia leggibile.
    // Lista vuota = cartellino compilato a totali, come prima del calendario.

    private IReadOnlyList<GiornataCartellino> _giornate = [];

    /// <summary>Le giornate del mese, disponibili al dominio; non mappate direttamente.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<GiornataCartellino> Giornate
    {
        get => _giornate;
        set
        {
            _giornate = value ?? [];
            GiornateModificate = true;
        }
    }

    /// <summary>Payload cifrato e tenant-bound del calendario. Vuoto = nessun calendario.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string GiornateProtette { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool GiornateModificate { get; private set; }

    internal void CaricaGiornate(IReadOnlyList<GiornataCartellino> giornate)
    {
        _giornate = giornate;
        GiornateModificate = false;
    }

    internal void ConfermaProtezioneGiornate() => GiornateModificate = false;

    public decimal OreAssenzaNonRetribuita { get; set; }

    /// <summary>Esito esplicito della verifica ANF del mese.</summary>
    public string StatoAssegniFamiliari { get; set; } =
        nameof(Paghe.Core.Domain.StatoAssegniFamiliari.DaVerificare);

    /// <summary>Importo ANF erogato nel mese; zero quando non spettante.</summary>
    public decimal ImportoAssegniFamiliari { get; set; }

    // ---- Elementi retributivi agevolati del mese ------------------------------------
    // Vanno archiviati con il resto del cartellino: il PDF della busta si rigenera
    // ricalcolando da qui, e senza questi campi mostrerebbe una busta priva di trasferta,
    // fringe benefit e buoni pasto, con totali diversi da quelli archiviati.
    public decimal GiorniTrasfertaItalia { get; set; }
    public decimal GiorniTrasfertaEstero { get; set; }
    public decimal IndennitaTrasfertaGiornaliera { get; set; }
    public string RimborsoTrasferta { get; set; } = nameof(TipoRimborsoTrasferta.Forfettario);
    public decimal RimborsiSpeseDocumentate { get; set; }
    public decimal FringeBenefit { get; set; }
    public decimal FringeBenefitGiaErogatoAnno { get; set; }
    public bool FigliACarico { get; set; }
    public int GiorniBuonoPasto { get; set; }
    public decimal ValoreBuonoPasto { get; set; }
    public bool BuonoPastoElettronico { get; set; } = true;
    public decimal PremioRisultato { get; set; }
    public decimal RedditoAnnoPrecedente { get; set; }

    /// <summary>Riferimento del prospetto separato della quattordicesima dovuta il 1 luglio.</summary>
    public string? RiferimentoQuattordicesimaSeparata { get; set; }

    public DateOnly? DataPagamentoQuattordicesima { get; set; }
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

    public decimal RetribuzioneFissa { get; set; }
    public decimal TariffaOraria { get; set; }
    public decimal TariffaGiornaliera { get; set; }

    public decimal TotaleCompetenze { get; set; }
    public decimal TotaleTrattenute { get; set; }
    public decimal ImponibileInps { get; set; }
    public decimal ContributiDipendente { get; set; }
    public decimal ContributiDatore { get; set; }
    public decimal ImponibileIrpef { get; set; }
    public decimal IrpefLorda { get; set; }
    public decimal DetrazioniLavoroDipendente { get; set; }
    public decimal IrpefNetta { get; set; }
    public decimal AddizionaleRegionale { get; set; }
    public decimal AddizionaleComunale { get; set; }
    public decimal NettoInBusta { get; set; }
    public decimal TotaleEsenti { get; set; }
    public decimal PremioRisultatoAgevolato { get; set; }
    public decimal ImpostaSostitutivaPremio { get; set; }
    public decimal QuotaTfr { get; set; }
    public decimal PrestazioniRecuperabili { get; set; }
    public decimal CostoAzienda { get; set; }

    /// <summary>
    /// Costo medio mensile annualizzato (retribuzione annua con 13ª/14ª, contributi c/datore e
    /// TFR, diviso 12). Null sui cedolini archiviati prima dell'introduzione del dato.
    /// </summary>
    public decimal? CostoMedioAnnualizzato { get; set; }

    /// <summary>
    /// Ulteriore detrazione «cuneo fiscale» del mese (L. 207/2024), separata dalla detrazione
    /// da lavoro dipendente. Null sui cedolini archiviati prima della separazione.
    /// </summary>
    public decimal? UlterioreDetrazioneCuneo { get; set; }

    public decimal FerieMaturate { get; set; }
    public decimal FerieGodute { get; set; }
    public decimal FerieResidue { get; set; }

    public decimal PermessiMaturati { get; set; }
    public decimal PermessiGoduti { get; set; }
    public decimal PermessiResidui { get; set; }
    public decimal RolSaldoPrecedente { get; set; }
    public decimal RolMaturati { get; set; }
    public decimal RolGoduti { get; set; }
    public decimal RolResidui { get; set; }
    public decimal ExFestivitaSaldoPrecedente { get; set; }
    public decimal ExFestivitaMaturate { get; set; }
    public decimal ExFestivitaGodute { get; set; }
    public decimal ExFestivitaResidue { get; set; }

    /// <summary>Conguaglio IRPEF applicato in busta (solo dicembre; >0 a debito, &lt;0 a credito).</summary>
    public decimal ConguaglioIrpef { get; set; }

    /// <summary>Identità ufficiale del CCNL applicato nel mese, congelata al calcolo.</summary>
    public string? CodiceCnel { get; set; }
    public string DescrizioneCcnl { get; set; } = "";
    public string? FonteCcnl { get; set; }

    /// <summary>
    /// Stato della macchina a stati. Le nuove elaborazioni nascono come Bozza; i record anteriori
    /// al workflow sono marcati PreWorkflow e restano immutabili, senza snapshot sintetici.
    /// </summary>
    public string Stato { get; set; } = nameof(StatoCedolino.Bozza);

    /// <summary>Token di concorrenza incrementato a ogni transizione.</summary>
    public int Versione { get; set; } = 1;

    /// <summary>Parametri effettivamente applicati, in JSON canonico cifrato tenant-bound.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ParametriApplicatiProtetti { get; set; } = "";
    public string HashParametriSha256 { get; set; } = "";

    public DateTime? ControllatoIl { get; set; }
    public Guid? ControllatoDa { get; set; }
    public DateTime? ValidatoIl { get; set; }
    public Guid? ValidatoDa { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DatiValidazioneProtetti { get; set; }
    public DateTime? ChiusoIl { get; set; }
    public Guid? ChiusoDa { get; set; }

    public DateTime CalcolatoIl { get; set; }
    public List<CedolinoVoceEntity> Voci { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public CedolinoSnapshotEntity? Snapshot { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public CedolinoDocumentoEntity? Documento { get; set; }
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

    /// <summary>
    /// Posizione della voce nel cedolino. Serve a ristampare la busta nello stesso ordine in cui
    /// è stata calcolata: senza, il database restituirebbe le righe in un ordine qualsiasi.
    /// </summary>
    public int Ordine { get; set; }

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
