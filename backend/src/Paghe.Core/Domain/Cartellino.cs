namespace Paghe.Core.Domain;

/// <summary>
/// Riepilogo presenze del mese: input principale del calcolo cedolino.
/// (Versione MVP: per l'impiegato a retribuzione fissa il mese è considerato pieno
/// salvo le assenze non retribuite indicate.)
/// </summary>
public sealed class Cartellino
{
    public required int Anno { get; init; }
    public required int Mese { get; init; }

    public decimal OreStraordinario { get; init; }
    public decimal OreStraordinarioNotturno { get; init; }
    public decimal OreStraordinarioFestivo { get; init; }

    public decimal GiorniFerie { get; init; }
    public decimal OrePermessi { get; init; }
    public decimal OreExFestivita { get; init; }

    public decimal GiorniMalattia { get; init; }
    /// <summary>Numero progressivo dell'evento di malattia nell'anno solare (1°, 2°, 3°, 4°...).</summary>
    public int NumeroEventoMalattiaAnno { get; init; } = 1;

    /// <summary>Giorni di inabilità temporanea per infortunio sul lavoro INAIL.</summary>
    public decimal GiorniInfortunio { get; init; }

    // Fotografia aggregata dell'unico evento sanitario rappresentabile dal motore corrente.
    // CalendarioCartellino.Compila la ricava dalle giornate dopo averne verificato coerenza e
    // sequenza. I nullable mantengono compatibili simulazioni e payload storici senza eventi.

    public string? IdentificativoEvento { get; init; }
    public DateOnly? DataInizioEvento { get; init; }
    public decimal? RetribuzioneMediaGiornalieraPrecedente { get; init; }
    public decimal? RateiGiornalieri { get; init; }
    public decimal? CoefficienteLordizzazione { get; init; }

    /// <summary>Ore di assenza non retribuita (decurtano la retribuzione mensile).</summary>
    public decimal OreAssenzaNonRetribuita { get; init; }

    /// <summary>Esito della verifica ANF del mese; DaVerificare impedisce la chiusura.</summary>
    public StatoAssegniFamiliari StatoAssegniFamiliari { get; init; }

    /// <summary>Importo ANF del mese, valorizzato solo quando gli assegni spettano.</summary>
    public decimal ImportoAssegniFamiliari { get; init; }

    // ---- Elementi retributivi agevolati del mese ------------------------------------
    // Esenti entro i limiti di legge dell'anno (vedi ParametriNormativi).
    // Vanno indicati solo se realmente spettanti: l'esenzione presuppone il presupposto.
    // La trasferta richiede che il viaggio sia avvenuto, fuori dal comune della sede, e
    // che sia documentato con spese pagate in modo tracciabile.

    /// <summary>Giorni di trasferta in Italia fuori dal comune della sede di lavoro.</summary>
    public decimal GiorniTrasfertaItalia { get; init; }

    /// <summary>Giorni di trasferta all'estero.</summary>
    public decimal GiorniTrasfertaEstero { get; init; }

    /// <summary>Indennità forfettaria riconosciuta per ogni giorno di trasferta.</summary>
    public decimal IndennitaTrasfertaGiornaliera { get; init; }

    /// <summary>Come sono trattate vitto e alloggio: riduce la soglia esente della diaria.</summary>
    public TipoRimborsoTrasferta RimborsoTrasferta { get; init; } = TipoRimborsoTrasferta.Forfettario;

    /// <summary>Rimborsi a piè di lista di spese documentate (esenti se giustificate).</summary>
    public decimal RimborsiSpeseDocumentate { get; init; }

    /// <summary>Fringe benefit erogati nel mese (beni e servizi, buoni acquisto, auto...).</summary>
    public decimal FringeBenefit { get; init; }

    /// <summary>Fringe benefit già erogati nei mesi precedenti dello stesso anno.</summary>
    public decimal FringeBenefitGiaErogatoAnno { get; init; }

    /// <summary>Alza la soglia annua dei fringe benefit (1.000 → 2.000 €).</summary>
    public bool FigliACarico { get; init; }

    /// <summary>Numero di buoni pasto del mese.</summary>
    public int GiorniBuonoPasto { get; init; }

    /// <summary>Valore facciale del singolo buono pasto.</summary>
    public decimal ValoreBuonoPasto { get; init; }

    /// <summary>Buono elettronico (limite più alto) oppure cartaceo.</summary>
    public bool BuonoPastoElettronico { get; init; } = true;

    /// <summary>Premio di risultato erogato nel mese, da contratto collettivo di secondo livello.</summary>
    public decimal PremioRisultato { get; init; }

    /// <summary>Reddito di lavoro dipendente dell'anno precedente: sopra il tetto niente agevolazione.</summary>
    public decimal RedditoAnnoPrecedente { get; init; }

    /// <summary>
    /// Identificativo del prospetto separato con cui e stata erogata la quattordicesima.
    /// Nel mese di luglio il cedolino ordinario non la incorpora automaticamente: il percorso
    /// archiviabile richiede questo riferimento e la data del pagamento.
    /// </summary>
    public string? RiferimentoQuattordicesimaSeparata { get; init; }

    public DateOnly? DataPagamentoQuattordicesima { get; init; }

    // ---- Calendario giornaliero -----------------------------------------------------
    // Il motore puro puo ancora ricevere una lista vuota per simulazioni isolate. Il percorso
    // archiviabile e il workflow, invece, la trattano come dato mancante bloccante: LUL,
    // denunce ed eventi tutelati ragionano sul giorno, non sul solo totale mensile.

    /// <summary>Le giornate del mese, se il cartellino è stato compilato giorno per giorno.</summary>
    public IReadOnlyList<GiornataCartellino> Giornate { get; init; } = [];

    /// <summary>Giornate di effettivo lavoro. Zero se il cartellino è compilato a totali.</summary>
    public int GiornateLavorate => Giornate.Count(g => g.ÈLavorata);
}
