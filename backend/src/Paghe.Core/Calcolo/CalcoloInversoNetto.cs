using System.Globalization;
using Paghe.Core.Domain;

namespace Paghe.Core.Calcolo;

/// <summary>Risultato del calcolo inverso: quale lordo serve per un netto voluto.</summary>
public sealed record EsitoCalcoloInverso(
    bool Raggiungibile,
    decimal NettoDesiderato,
    decimal NettoOttenuto,
    decimal SuperminimoNecessario,
    decimal LordoMensile,
    decimal CostoAzienda,
    string Nota);

/// <summary>
/// Calcolo inverso netto → lordo: dato il netto che si vuole in busta, cerca il superminimo
/// individuale che lo produce, lasciando invariato tutto il resto (CCNL, livello, cartellino).
///
/// Il superminimo è l'unica componente liberamente concordabile: minimo tabellare, scatti ed
/// EDR sono fissati dal contratto collettivo. Per questo il netto <b>non può scendere</b> sotto
/// quello prodotto dal solo minimo contrattuale: se il netto richiesto è più basso, il metodo
/// lo dichiara non raggiungibile invece di forzarlo. Un importo inferiore al minimo tabellare
/// si ottiene soltanto riducendo le ore contrattuali o registrando assenze realmente avvenute.
/// </summary>
public static class CalcoloInversoNetto
{
    /// <summary>Precisione della ricerca: un centesimo di euro sul netto.</summary>
    private const decimal Tolleranza = 0.01m;

    /// <summary>Limite superiore della ricerca del superminimo (100.000 € al mese).</summary>
    private const decimal SuperminimoMassimo = 100_000m;

    /// <summary>Iterazioni della ricerca binaria: 60 bastano ampiamente per il centesimo.</summary>
    private const int IterazioniMassime = 60;

    /// <summary>Le note finiscono sotto gli occhi dell'utente: importi in formato italiano.</summary>
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    private static string Euro(decimal importo) => importo.ToString("N2", Italiano);

    public static EsitoCalcoloInverso Calcola(
        MotoreCalcoloCedolino motore,
        RapportoLavoro rapporto,
        Cartellino cartellino,
        decimal nettoDesiderato)
    {
        ArgumentNullException.ThrowIfNull(motore);
        ArgumentNullException.ThrowIfNull(rapporto);
        ArgumentNullException.ThrowIfNull(cartellino);
        if (nettoDesiderato <= 0m)
            throw new ArgumentException("Il netto desiderato deve essere positivo.", nameof(nettoDesiderato));

        // Pavimento contrattuale: il netto con superminimo azzerato.
        var minimo = motore.Calcola(rapporto.ConSuperminimo(0m), cartellino);
        if (nettoDesiderato < minimo.NettoInBusta - Tolleranza)
        {
            return new EsitoCalcoloInverso(
                Raggiungibile: false,
                NettoDesiderato: nettoDesiderato,
                NettoOttenuto: minimo.NettoInBusta,
                SuperminimoNecessario: 0m,
                LordoMensile: minimo.TotaleCompetenze,
                CostoAzienda: minimo.CostoAzienda,
                Nota: $"Con il solo minimo tabellare del CCNL il netto è già {Euro(minimo.NettoInBusta)} €. "
                    + "Non si può scendere sotto il minimo contrattuale: per un importo inferiore "
                    + "servono meno ore contrattuali o assenze realmente avvenute.");
        }

        // Il netto cresce al crescere del superminimo: ricerca binaria.
        decimal basso = 0m, alto = SuperminimoMassimo;
        var migliore = minimo;
        decimal superminimoMigliore = 0m;

        for (int i = 0; i < IterazioniMassime && alto - basso > Tolleranza; i++)
        {
            decimal medio = (basso + alto) / 2m;
            var prova = motore.Calcola(rapporto.ConSuperminimo(medio), cartellino);

            if (Math.Abs(prova.NettoInBusta - nettoDesiderato)
                < Math.Abs(migliore.NettoInBusta - nettoDesiderato))
            {
                migliore = prova;
                superminimoMigliore = medio;
            }

            if (prova.NettoInBusta < nettoDesiderato) basso = medio;
            else alto = medio;
        }

        decimal scarto = Math.Abs(migliore.NettoInBusta - nettoDesiderato);
        return new EsitoCalcoloInverso(
            Raggiungibile: scarto <= Tolleranza * 2m,
            NettoDesiderato: nettoDesiderato,
            NettoOttenuto: migliore.NettoInBusta,
            SuperminimoNecessario: Arrotondamento.Euro(superminimoMigliore),
            LordoMensile: migliore.TotaleCompetenze,
            CostoAzienda: migliore.CostoAzienda,
            Nota: scarto <= Tolleranza * 2m
                ? $"Netto {Euro(migliore.NettoInBusta)} € con un superminimo di "
                  + $"{Euro(Arrotondamento.Euro(superminimoMigliore))} € al mese."
                : $"Netto più vicino ottenibile: {Euro(migliore.NettoInBusta)} €. "
                  + "Lo scarto dipende dagli arrotondamenti al centesimo delle singole voci.");
    }
}
