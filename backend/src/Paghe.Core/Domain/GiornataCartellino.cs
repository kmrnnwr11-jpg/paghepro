namespace Paghe.Core.Domain;

/// <summary>
/// Una giornata del cartellino: che cosa è stata (<see cref="Causale"/>) e con quali quantità.
/// È il mattone del calendario giornaliero, quello che il LUL richiede e da cui l'agricoltura
/// ricava le giornate retribuite. Le ore parziali (permesso e straordinario)
/// convivono con la causale prevalente: una giornata lavorata con 4 ore di permesso resta
/// <see cref="CausaleGiornata.Lavorata"/>.
/// </summary>
public sealed record GiornataCartellino
{
    public required DateOnly Data { get; init; }

    public required CausaleGiornata Causale { get; init; }

    /// <summary>Ore effettivamente lavorate nella giornata.</summary>
    public decimal OreOrdinarie { get; init; }

    /// <summary>Ore di permesso retribuito (ROL) godute nella giornata.</summary>
    public decimal OrePermessi { get; init; }

    /// <summary>Ore godute a titolo di ex festività.</summary>
    public decimal OreExFestivita { get; init; }

    /// <summary>
    /// Ore della giornata interamente classificata come assenza non retribuita. Il modello
    /// corrente non le sovrappone a ore lavorate o ad altre causali.
    /// </summary>
    public decimal OreAssenzaNonRetribuita { get; init; }

    public decimal OreStraordinario { get; init; }
    public decimal OreStraordinarioNotturno { get; init; }
    public decimal OreStraordinarioFestivo { get; init; }

    /// <summary>
    /// Totale progressivo delle ore effettivamente lavorate nella settimana ISO, comprese le
    /// ore lavorate nei giorni del mese precedente che appartengono alla stessa settimana.
    /// È nullable per rileggere i payload storici, ma il calendario elaborabile lo richiede.
    /// </summary>
    public decimal? ProgressivoOreSettimanali { get; init; }

    // I dati dell'evento tutelato restano sulla singola giornata: così non si può applicare
    // una media o una lordizzazione a giorni diversi senza una traccia verificabile.

    /// <summary>Numero di certificato/protocollo che identifica l'evento sanitario.</summary>
    public string? IdentificativoEvento { get; init; }

    /// <summary>Primo giorno dell'evento, anche se appartiene al mese precedente.</summary>
    public DateOnly? DataInizioEvento { get; init; }

    /// <summary>RMG determinata usando il mese precedente all'inizio dell'evento.</summary>
    public decimal? RetribuzioneMediaGiornalieraPrecedente { get; init; }

    /// <summary>Ratei giornalieri inclusi nella base dell'evento, anche quando pari a zero.</summary>
    public decimal? RateiGiornalieri { get; init; }

    /// <summary>Coefficiente effettivo di lordizzazione; 1 indica nessuna lordizzazione.</summary>
    public decimal? CoefficienteLordizzazione { get; init; }

    /// <summary>Somma delle ore di straordinario di qualsiasi tipo.</summary>
    public decimal TotaleStraordinario =>
        OreStraordinario + OreStraordinarioNotturno + OreStraordinarioFestivo;

    /// <summary>Ore di lavoro effettivo che alimentano il progressivo settimanale.</summary>
    public decimal TotaleOreLavorate => OreOrdinarie + TotaleStraordinario;

    /// <summary>La giornata conta come lavorata: serve alle denunce che ragionano a giornate.</summary>
    public bool ÈLavorata => Causale == CausaleGiornata.Lavorata;
}
