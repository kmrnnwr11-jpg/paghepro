using System.Globalization;
using System.Text.RegularExpressions;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Riconosce i dati di una Comunicazione Obbligatoria UNILAV dal <b>testo</b> estratto da un PDF
/// (modello con i quadri "Datore di lavoro", "Lavoratore", "Rapporto di lavoro"). Riusa i DTO
/// dell'import XML (<see cref="RisultatoImportUnilav"/>).
///
/// <para>È <b>euristico</b>: cerca le etichette note e prende il valore sulla stessa riga (o sulla
/// riga successiva), delimitando i campi per sezione. I PDF reali variano per portale/versione:
/// le etichette sono configurate con più alias e vanno affinate su un campione reale. I campi non
/// riconosciuti finiscono nelle <c>Avvertenze</c> invece di bloccare l'import.</para>
/// </summary>
public static class ImportazioneUnilavPdf
{
    public static RisultatoImportUnilav Importa(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo))
            throw new ArgumentException("Il testo dell'UNILAV è vuoto: il PDF non contiene testo leggibile.");

        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').Select(r => r.Trim()).Where(r => r.Length > 0).ToArray();

        // Confini delle sezioni per nome di intestazione (se assenti, si cerca su tutto il testo).
        int iDatore = IndiceIntestazione(righe, "datore di lavoro", "datore");
        int iLavoratore = IndiceIntestazione(righe, "lavoratore", "dati del lavoratore");
        int iRapporto = IndiceIntestazione(righe, "rapporto di lavoro", "inizio rapporto", "instaurazione", "dati del rapporto");

        var datoreBlk = Blocco(righe, iDatore, iLavoratore, iRapporto);
        var lavoratoreBlk = Blocco(righe, iLavoratore, iDatore, iRapporto);
        var rapportoBlk = Blocco(righe, iRapporto, iDatore, iLavoratore);

        var datore = new DatoreLavoroUnilav(
            CodiceFiscaleOPiva(Campo(datoreBlk, "codice fiscale", "cod. fiscale", "c.f.", "partita iva", "p. iva")),
            Campo(datoreBlk, "denominazione", "ragione sociale"));

        var lavoratore = new LavoratoreUnilav(
            CodiceFiscale(Campo(lavoratoreBlk, "codice fiscale", "cod. fiscale", "c.f.")),
            Campo(lavoratoreBlk, "cognome"),
            Campo(lavoratoreBlk, "nome"),
            Campo(lavoratoreBlk, "sesso"),
            Data(Campo(lavoratoreBlk, "data di nascita", "data nascita", "nato il", "nata il")),
            Campo(lavoratoreBlk, "comune di nascita", "comune nascita", "luogo di nascita"),
            Campo(lavoratoreBlk, "provincia di nascita", "provincia nascita"),
            Campo(lavoratoreBlk, "cittadinanza"),
            Campo(lavoratoreBlk, "comune di domicilio", "comune domicilio", "comune di residenza", "comune"),
            Campo(lavoratoreBlk, "provincia di domicilio", "provincia domicilio", "provincia"),
            Campo(lavoratoreBlk, "c.a.p.", "cap"));

        var rapporto = new RapportoUnilav(
            Data(Campo(rapportoBlk, "data inizio", "data di inizio", "data assunzione", "decorrenza")),
            Data(Campo(rapportoBlk, "data fine", "data di fine", "data cessazione", "data termine")),
            Campo(rapportoBlk, "tipologia contrattuale", "tipo contratto", "tipo di contratto", "tipologia"),
            Campo(rapportoBlk, "c.c.n.l.", "ccnl", "contratto collettivo", "contratto applicato"),
            Campo(rapportoBlk, "qualifica professionale", "qualifica"),
            Campo(rapportoBlk, "livello di inquadramento", "livello"),
            Campo(rapportoBlk, "tipo orario", "orario", "tipologia oraria"),
            Numero(Campo(rapportoBlk, "percentuale part-time", "percentuale part time", "perc. part-time")),
            Numero(Campo(rapportoBlk, "ore settimanali", "ore medie settimanali")),
            Numero(Campo(rapportoBlk, "retribuzione", "retribuzione prevista", "retribuzione annua")));

        var avvertenze = new List<string>();
        if (string.IsNullOrWhiteSpace(lavoratore.CodiceFiscale))
            avvertenze.Add("Codice fiscale del lavoratore non riconosciuto nel PDF: verificare e completare.");
        if (string.IsNullOrWhiteSpace(datore.CodiceFiscale) && string.IsNullOrWhiteSpace(datore.Denominazione))
            avvertenze.Add("Datore di lavoro non riconosciuto nel PDF: verificare e completare.");
        if (string.IsNullOrWhiteSpace(rapporto.Ccnl))
            avvertenze.Add("CCNL non riconosciuto: andrà indicato manualmente per il calcolo del cedolino.");
        if (string.IsNullOrWhiteSpace(rapporto.Livello))
            avvertenze.Add("Livello di inquadramento non riconosciuto: andrà indicato manualmente.");

        return new RisultatoImportUnilav
        {
            Datore = datore,
            Lavoratore = lavoratore,
            Rapporto = rapporto,
            Avvertenze = avvertenze,
        };
    }

    /// <summary>Indice della prima riga che contiene una delle intestazioni, o -1.</summary>
    private static int IndiceIntestazione(string[] righe, params string[] intestazioni)
    {
        for (int i = 0; i < righe.Length; i++)
        {
            string r = righe[i].ToLowerInvariant();
            if (intestazioni.Any(h => r.Contains(h)))
                return i;
        }
        return -1;
    }

    /// <summary>Righe di una sezione: dall'intestazione fino alla successiva intestazione di sezione.</summary>
    private static string[] Blocco(string[] righe, int inizio, params int[] altreSezioni)
    {
        if (inizio < 0)
            return righe; // nessuna intestazione: si cerca su tutto il testo
        int fine = altreSezioni.Where(i => i > inizio).DefaultIfEmpty(righe.Length).Min();
        return righe[inizio..fine];
    }

    /// <summary>
    /// Valore associato a una delle etichette: resto della riga dopo l'etichetta, o riga successiva
    /// se vuoto. Cerca <b>per etichetta</b> (in ordine di priorità) su tutto il blocco, così le
    /// etichette specifiche vincono sulle intestazioni generiche di sezione.
    /// </summary>
    private static string? Campo(string[] blocco, params string[] etichette)
    {
        foreach (string et in etichette)
        {
            for (int i = 0; i < blocco.Length; i++)
            {
                string riga = blocco[i];
                // L'etichetta deve aprire la riga (come nei moduli UNILAV): evita falsi positivi
                // come "nome" dentro "Cognome".
                if (!riga.ToLowerInvariant().StartsWith(et, StringComparison.Ordinal)) continue;

                string dopo = riga[et.Length..].TrimStart(' ', ':', '-', '.', '\t', '=');
                if (dopo.Length > 0)
                    return dopo.Trim();
                if (i + 1 < blocco.Length)
                    return blocco[i + 1].Trim();
            }
        }
        return null;
    }

    /// <summary>Estrae un codice fiscale a 16 caratteri dal testo passato.</summary>
    private static string? CodiceFiscale(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var m = Regex.Match(valore, "[A-Za-z]{6}[0-9]{2}[A-Za-z][0-9]{2}[A-Za-z][0-9]{3}[A-Za-z]");
        return m.Success ? m.Value.ToUpperInvariant() : valore.Trim();
    }

    /// <summary>Estrae un codice fiscale (16) o una partita IVA (11 cifre) dal testo passato.</summary>
    private static string? CodiceFiscaleOPiva(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var cf = Regex.Match(valore, "[A-Za-z]{6}[0-9]{2}[A-Za-z][0-9]{2}[A-Za-z][0-9]{3}[A-Za-z]");
        if (cf.Success) return cf.Value.ToUpperInvariant();
        var piva = Regex.Match(valore, "[0-9]{11}");
        return piva.Success ? piva.Value : valore.Trim();
    }

    private static DateOnly? Data(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var m = Regex.Match(valore, @"\d{1,4}[/\-.]\d{1,2}[/\-.]\d{1,4}");
        string v = m.Success ? m.Value : valore.Trim();
        string[] formati = ["dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "yyyy/MM/dd"];
        return DateOnly.TryParseExact(v, formati, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2) ? d2 : null;
    }

    private static decimal? Numero(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var m = Regex.Match(valore, @"\d+(?:[.,]\d+)?");
        if (!m.Success) return null;
        string v = m.Value;
        bool virgolaDecimale = v.Contains(',') && !v.Contains('.');
        var cultura = virgolaDecimale ? CultureInfo.GetCultureInfo("it-IT") : CultureInfo.InvariantCulture;
        return decimal.TryParse(v, NumberStyles.Number, cultura, out var n) ? n : null;
    }
}
