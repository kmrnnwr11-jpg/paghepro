namespace Paghe.Core.Domain;

/// <summary>
/// Una giornata del cartellino: che cosa è stata (<see cref="Causale"/>) e con quali quantità.
/// È il mattone del calendario giornaliero, quello che il LUL richiede e da cui l'agricoltura
/// ricava le giornate retribuite. Le ore parziali (permesso, straordinario, uscita anticipata)
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

    /// <summary>Ore di assenza non retribuita (es. uscita anticipata senza permesso).</summary>
    public decimal OreAssenzaNonRetribuita { get; init; }

    public decimal OreStraordinario { get; init; }
    public decimal OreStraordinarioNotturno { get; init; }
    public decimal OreStraordinarioFestivo { get; init; }

    /// <summary>Somma delle ore di straordinario di qualsiasi tipo.</summary>
    public decimal TotaleStraordinario =>
        OreStraordinario + OreStraordinarioNotturno + OreStraordinarioFestivo;

    /// <summary>La giornata conta come lavorata: serve alle denunce che ragionano a giornate.</summary>
    public bool ÈLavorata => Causale == CausaleGiornata.Lavorata;
}
