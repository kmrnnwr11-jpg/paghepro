namespace Paghe.Core.Calcolo;

/// <summary>Arrotondamenti monetari secondo la prassi delle buste paga.</summary>
public static class Arrotondamento
{
    /// <summary>Arrotonda all'euro-centesimo (2 decimali), commerciale (0,5 → su).</summary>
    public static decimal Euro(decimal valore) =>
        Math.Round(valore, 2, MidpointRounding.AwayFromZero);
}
