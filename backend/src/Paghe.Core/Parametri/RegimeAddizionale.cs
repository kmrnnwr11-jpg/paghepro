namespace Paghe.Core.Parametri;

/// <summary>
/// Uno scaglione di un'addizionale IRPEF (regionale o comunale): l'<see cref="Aliquota"/>
/// (in forma di frazione, es. 0,008 = 0,80%) si applica alla quota di reddito compresa tra
/// <see cref="DaImporto"/> e <see cref="AImporto"/> (estremo superiore aperto = ultimo scaglione).
/// </summary>
public sealed record ScaglioneAddizionale(decimal DaImporto, decimal? AImporto, decimal Aliquota);

/// <summary>
/// Regime di un'addizionale IRPEF per un comune o una regione, come pubblicato dal MEF:
/// una <see cref="SogliaEsenzione"/> (se l'imponibile non la supera l'addizionale non è dovuta)
/// e uno o più <see cref="Scaglioni"/> progressivi. Un'aliquota unica è modellata come un solo
/// scaglione [0, +∞).
/// </summary>
public sealed record RegimeAddizionale(decimal SogliaEsenzione, IReadOnlyList<ScaglioneAddizionale> Scaglioni)
{
    /// <summary>Nessuna addizionale (comune/regione che non l'ha istituita).</summary>
    public static readonly RegimeAddizionale Nessuno = new(0m, Array.Empty<ScaglioneAddizionale>());

    /// <summary>Regime ad aliquota unica sull'intero imponibile, con eventuale soglia di esenzione.</summary>
    public static RegimeAddizionale Unica(decimal aliquota, decimal sogliaEsenzione = 0m) =>
        aliquota <= 0m
            ? Nessuno
            : new RegimeAddizionale(sogliaEsenzione, new[] { new ScaglioneAddizionale(0m, null, aliquota) });
}
