namespace Paghe.Core.Domain;

/// <summary>
/// Tipo di operaio agricolo. Cambia il contratto, la denuncia trimestrale e — soprattutto —
/// il minimo di detrazione fiscale: il tempo determinato ha un minimo annuo non rapportabile
/// ai giorni (art. 13 c.1 TUIR), ed è la ragione per cui il bracciante stagionale con pochi
/// mesi di lavoro non paga IRPEF.
/// </summary>
public enum TipoOperaioAgricolo
{
    /// <summary>OTD: operaio a tempo determinato (avventizio, stagionale).</summary>
    Determinato,

    /// <summary>OTI: operaio a tempo indeterminato (fisso aziendale).</summary>
    Indeterminato
}

/// <summary>
/// Rapporto di lavoro di un operaio agricolo. Non usa la retribuzione mensile fissa del
/// <see cref="RapportoLavoro"/>: in agricoltura si paga per ore effettive alla tariffa del
/// contratto <b>provinciale</b> (CPL), quindi il rapporto punta a provincia e livello e la
/// tariffa arriva dai parametri dell'anno.
/// </summary>
public sealed class RapportoAgricolo
{
    /// <summary>Sigla della provincia del CPL applicato (es. "CN"): le tariffe sono provinciali.</summary>
    public required string Provincia { get; init; }

    public required TipoOperaioAgricolo Tipo { get; init; }

    /// <summary>Livello/area di inquadramento nella tabella salariale provinciale.</summary>
    public required string Livello { get; init; }

    public bool DetrazioniLavoroDipendente { get; init; } = true;

    /// <summary>
    /// Reddito annuo presunto usato per scegliere aliquota e detrazioni. Per uno stagionale
    /// annualizzare il mese × 12 è falso — dichiarerebbe 16.000 € a chi ne guadagna 2.400 —
    /// quindi qui si indica la stima reale del rapporto. Se manca si annualizza il mese.
    /// </summary>
    public decimal? RedditoPresuntoAnnuo { get; init; }

    /// <summary>Regione di residenza (addizionale regionale IRPEF). Vuota = nessuna addizionale.</summary>
    public string Regione { get; init; } = "";

    /// <summary>Codice catastale del comune di residenza (addizionale comunale).</summary>
    public string CodiceCatastaleComune { get; init; } = "";
}

/// <summary>
/// Presenze del mese di un operaio agricolo. Ore e giornate sono due dati distinti e servono
/// a due cose diverse: le ore pagano la busta, le giornate vanno all'INPS (denuncia trimestrale,
/// disoccupazione agricola, estratto conto) e non si ricavano dividendo le ore per un numero
/// fisso, perché la giornata agricola dura quello che dura.
/// </summary>
public sealed class CartellinoAgricolo
{
    public required int Anno { get; init; }
    public required int Mese { get; init; }

    /// <summary>Ore ordinarie effettivamente lavorate nel mese.</summary>
    public required decimal OreOrdinarie { get; init; }

    /// <summary>Giornate lavorate del mese (campo "GG Lav"): è il dato che l'INPS registra.</summary>
    public required int GiornateLavorate { get; init; }

    /// <summary>Giorni di detrazione fiscale spettanti nel mese.</summary>
    public int GiorniDetrazione { get; init; }

    /// <summary>Una tantum o altre competenze imponibili erogate nel mese.</summary>
    public decimal UnaTantum { get; init; }

    /// <summary>
    /// Giorni di trasferta in Italia fuori dal comune della sede di lavoro. Come nel motore
    /// ordinario: l'esenzione spetta solo se la trasferta è avvenuta davvero e documentata —
    /// il motore applica il limite giornaliero, non attesta i presupposti.
    /// </summary>
    public decimal GiorniTrasferta { get; init; }

    /// <summary>Indennità forfettaria riconosciuta per ogni giorno di trasferta.</summary>
    public decimal IndennitaTrasfertaGiornaliera { get; init; }

    /// <summary>Rimborso a piè di lista di spese documentate e tracciabili (esente).</summary>
    public decimal RimborsiSpeseDocumentate { get; init; }

    /// <summary>
    /// Detrazioni già godute nei mesi precedenti dell'anno: la detrazione minima del tempo
    /// determinato è annua, non mensile, e senza questo dato verrebbe riconosciuta ogni mese.
    /// </summary>
    public decimal DetrazioniGiaGoduteAnno { get; init; }

    /// <summary>
    /// Arrotondamenti del netto già anticipati al lavoratore nei mesi precedenti. In busta il
    /// netto si arrotonda all'euro e la differenza si trascina: l'ultimo cedolino la chiude.
    /// </summary>
    public decimal ArrotondamentoRiportato { get; init; }

    /// <summary>Ultimo cedolino del rapporto: il netto non si arrotonda e si recupera il riportato.</summary>
    public bool UltimoCedolino { get; init; }
}
