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

    /// <summary>Ore di assenza non retribuita (decurtano la retribuzione mensile).</summary>
    public decimal OreAssenzaNonRetribuita { get; init; }
}
