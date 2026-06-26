namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Validazioni formali di codice fiscale e partita IVA con algoritmi pubblici e
/// deterministici (carattere di controllo CF, check digit P.IVA mod 10).
/// Sono controlli formali: non verificano l'esistenza reale del soggetto presso l'Agenzia.
/// </summary>
public static class ValidatoreFiscale
{
    // Tabella di conversione per le posizioni dispari (1-indicizzate) del codice fiscale.
    private static readonly IReadOnlyDictionary<char, int> ValoriDispari = new Dictionary<char, int>
    {
        ['0'] = 1, ['1'] = 0, ['2'] = 5, ['3'] = 7, ['4'] = 9,
        ['5'] = 13, ['6'] = 15, ['7'] = 17, ['8'] = 19, ['9'] = 21,
        ['A'] = 1, ['B'] = 0, ['C'] = 5, ['D'] = 7, ['E'] = 9,
        ['F'] = 13, ['G'] = 15, ['H'] = 17, ['I'] = 19, ['J'] = 21,
        ['K'] = 2, ['L'] = 4, ['M'] = 18, ['N'] = 20, ['O'] = 11,
        ['P'] = 3, ['Q'] = 6, ['R'] = 8, ['S'] = 12, ['T'] = 14,
        ['U'] = 16, ['V'] = 10, ['W'] = 22, ['X'] = 25, ['Y'] = 24, ['Z'] = 23,
    };

    /// <summary>
    /// Vero se il valore è un codice fiscale valido di persona fisica (16 caratteri
    /// alfanumerici con carattere di controllo) oppure un codice fiscale numerico di
    /// soggetto diverso (11 cifre, stesso formato della partita IVA).
    /// </summary>
    public static bool CodiceFiscaleValido(string? codiceFiscale)
    {
        if (string.IsNullOrWhiteSpace(codiceFiscale))
            return false;

        string cf = codiceFiscale.Trim().ToUpperInvariant();
        return cf.Length == 11 ? PartitaIvaValida(cf) : CodiceFiscalePersonaFisicaValido(cf);
    }

    /// <summary>Vero se è un codice fiscale di persona fisica formalmente valido (16 caratteri).</summary>
    public static bool CodiceFiscalePersonaFisicaValido(string? codiceFiscale)
    {
        if (string.IsNullOrWhiteSpace(codiceFiscale))
            return false;

        string cf = codiceFiscale.Trim().ToUpperInvariant();
        if (cf.Length != 16)
            return false;

        foreach (char c in cf)
            if (!char.IsLetterOrDigit(c) || c > 'Z')
                return false;

        int somma = 0;
        for (int i = 0; i < 15; i++)
        {
            char c = cf[i];
            bool posizioneDispari = (i + 1) % 2 == 1;
            somma += posizioneDispari ? ValoriDispari[c] : ValorePari(c);
        }

        char attesa = (char)('A' + somma % 26);
        return cf[15] == attesa;
    }

    /// <summary>Vero se la partita IVA è formalmente valida (11 cifre, check digit mod 10).</summary>
    public static bool PartitaIvaValida(string? partitaIva)
    {
        if (string.IsNullOrWhiteSpace(partitaIva))
            return false;

        string piva = partitaIva.Trim();
        if (piva.Length != 11 || !piva.All(char.IsDigit))
            return false;

        int somma = 0;
        for (int i = 0; i < 10; i++)
        {
            int cifra = piva[i] - '0';
            if (i % 2 == 0)
            {
                somma += cifra;
            }
            else
            {
                int doppio = cifra * 2;
                somma += doppio > 9 ? doppio - 9 : doppio;
            }
        }

        int controllo = (10 - somma % 10) % 10;
        return controllo == piva[10] - '0';
    }

    private static int ValorePari(char c) => char.IsDigit(c) ? c - '0' : c - 'A';
}
