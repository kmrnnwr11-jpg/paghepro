namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Numerazione delle settimane usata dall'UniEmens. Dall'allegato tecnico INPS:
/// <c>IdSettimana</c> è «il progressivo assoluto della settimana (da domenica a sabato), o
/// frazione, nell'anno», da 1 a 54.
///
/// Quindi la settimana 1 è la frazione che va dal 1° gennaio al primo sabato dell'anno; dalla
/// 2 in poi ogni settimana comincia di domenica. Non è la numerazione ISO 8601 (lunedì–domenica)
/// che userebbe qualunque libreria standard: usarla produrrebbe numeri sbagliati.
/// </summary>
public static class SettimaneInps
{
    /// <summary>Progressivo INPS della settimana che contiene la data indicata.</summary>
    public static int IdSettimana(DateOnly data)
    {
        var primoGennaio = new DateOnly(data.Year, 1, 1);

        // Prima domenica dell'anno: apre la settimana 2. Se il 1° gennaio è domenica,
        // la frazione iniziale è la settimana 1 e la 2 comincia sette giorni dopo.
        int giorniAllaDomenica = (7 - (int)primoGennaio.DayOfWeek) % 7;
        if (giorniAllaDomenica == 0) giorniAllaDomenica = 7;
        var primaDomenica = primoGennaio.AddDays(giorniAllaDomenica);

        if (data < primaDomenica) return 1;
        return 2 + (data.DayNumber - primaDomenica.DayNumber) / 7;
    }

    /// <summary>
    /// Progressivi delle settimane che toccano il mese indicato, in ordine. Una settimana a
    /// cavallo di due mesi appartiene a entrambi: l'ultima di dicembre può arrivare a 53 o 54.
    /// </summary>
    public static IReadOnlyList<int> DelMese(int anno, int mese)
    {
        int giorni = DateTime.DaysInMonth(anno, mese);
        var progressivi = new List<int>();
        for (int giorno = 1; giorno <= giorni; giorno++)
        {
            int id = IdSettimana(new DateOnly(anno, mese, giorno));
            if (progressivi.Count == 0 || progressivi[^1] != id) progressivi.Add(id);
        }
        return progressivi;
    }
}
