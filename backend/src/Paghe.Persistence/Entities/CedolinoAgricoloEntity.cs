namespace Paghe.Persistence.Entities;

/// <summary>
/// Busta paga agricola archiviata. Vive in una tabella <b>propria</b>, separata dai cedolini
/// ordinari, e non è un capriccio di modello: in agricoltura la retribuzione nasce dalle ore
/// effettive alla tariffa del contratto provinciale e i contributi li liquida l'INPS sulla
/// denuncia mensile PosAgri. Se questi documenti finissero nella tabella <c>Cedolini</c>
/// verrebbero raccolti dai flussi ordinari che leggono di là — F24 da paghe, UniEmens, CU —
/// producendo adempimenti sbagliati in silenzio.
///
/// L'archiviazione conserva il PDF prodotto (protetto come gli altri documenti di paga) e i
/// dati che l'hanno generato: la busta si ritrova e si riscarica identica a com'è stata
/// consegnata. Resta un prospetto da validare col professionista: la macchina a stati
/// Controllata/Validata/Chiusa dei cedolini ordinari qui non si applica, perché i suoi
/// controlli (calendario giornaliero, CNEL, ANF) sono del ciclo ordinario.
/// </summary>
public sealed class CedolinoAgricoloEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public Guid RapportoLavoroId { get; set; }

    public int Anno { get; set; }
    public int Mese { get; set; }

    // --- Presenze e variabili del mese, come dichiarate al momento dell'archiviazione ---
    public decimal OreOrdinarie { get; set; }
    public int GiornateLavorate { get; set; }
    public int GiorniDetrazione { get; set; }
    public decimal UnaTantum { get; set; }
    public decimal GiorniTrasferta { get; set; }
    public decimal IndennitaTrasfertaGiornaliera { get; set; }

    /// <summary>Gruppi di trasferta (giorni × importo) in JSON, quando usati al posto dei due campi.</summary>
    public string? TrasferteJson { get; set; }
    public decimal RimborsiSpeseDocumentate { get; set; }
    public decimal DetrazioniGiaGoduteAnno { get; set; }
    public decimal ArrotondamentoRiportato { get; set; }
    public bool UltimoCedolino { get; set; }
    public decimal RedditoPresuntoAnnuo { get; set; }

    // --- Risultato del calcolo: elenchi e riscontri senza riaprire il PDF ---
    public decimal TotaleCompetenze { get; set; }
    public decimal TotaleTrattenute { get; set; }
    public decimal ImponibileInps { get; set; }
    public decimal ContributiDipendente { get; set; }
    public decimal ImponibileIrpef { get; set; }
    public decimal IrpefNetta { get; set; }
    public decimal NettoInBusta { get; set; }

    /// <summary>Sigla della provincia del CPL applicato: le tariffe agricole sono provinciali.</summary>
    public string ProvinciaCpl { get; set; } = "";

    /// <summary>Livello della tabella salariale provinciale applicato.</summary>
    public string LivelloTariffa { get; set; } = "";

    /// <summary>Fonte della tabella applicata: una paga senza fonte non è verificabile.</summary>
    public string FonteTabelle { get; set; } = "";

    /// <summary>PDF archiviato, protetto come gli altri documenti di paga (base64 cifrato).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string PdfProtetto { get; set; } = "";

    /// <summary>Impronta del PDF archiviato: dice se il documento riletto è quello di allora.</summary>
    public string HashPdfSha256 { get; set; } = "";

    public DateTime ArchiviatoIl { get; set; }
    public Guid ArchiviatoDa { get; set; }

    /// <summary>
    /// Nominativo (ed eventuale qualifica) del professionista che ha dichiarato di aver
    /// controllato la busta al momento dell'archiviazione. Null = prospetto da validare:
    /// il PDF conserva la dicitura «BOZZA DA VALIDARE».
    /// </summary>
    public string? ValidataDa { get; set; }

    /// <summary>Quando è stata dichiarata la validazione (coincide con l'archiviazione).</summary>
    public DateTime? ValidataIl { get; set; }

    /// <summary>Quante volte la busta del periodo è stata riarchiviata dopo una correzione.</summary>
    public int Versione { get; set; } = 1;
}
