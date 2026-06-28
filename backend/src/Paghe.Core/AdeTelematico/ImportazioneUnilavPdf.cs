using System.Globalization;
using System.Text.RegularExpressions;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Riconosce i dati di una Comunicazione Obbligatoria UNILAV dal <b>testo</b> estratto da un PDF.
/// Gestisce i moduli reali dei vari portali regionali (Emilia-Romagna, Campania/SILF, ...), che
/// differiscono per: intestazioni di sezione con prefisso ("Sezione 1 - Datore di lavoro") o senza
/// ("Datore di Lavoro", "Dati Rapporto"), etichette con o senza due punti, e <b>più campi sulla
/// stessa riga</b> ("Cognome: X Nome: Y"). Riusa i DTO dell'import XML (<see cref="RisultatoImportUnilav"/>).
///
/// <para>Strategia: individua le sezioni (datore/lavoratore/rapporto) e, per ciascun campo, cerca
/// l'etichetta come <b>parola intera</b> dentro la sezione, prendendo il valore fino alla prossima
/// etichetta nota o all'a-capo. È euristico: i campi non riconosciuti diventano avvertenze.</para>
/// </summary>
public static class ImportazioneUnilavPdf
{
    // Etichette usate per delimitare il valore quando più campi sono sulla stessa riga.
    private static readonly string[] EtichetteTaglio =
    {
        "codice fiscale", "cognome", "nome", "sesso", "data di nascita", "data nascita",
        "comune di nascita", "comune nascita", "comune di domicilio", "comune domicilio",
        "provincia di nascita", "provincia di domicilio", "provincia", "cittadinanza",
        "titolo di soggiorno", "tipo di soggiorno", "motivo titolo di soggiorno", "motivo di soggiorno",
        "scadenza titolo di soggiorno", "data scadenza titolo soggiorno", "questura di rilascio titolo di soggiorno",
        "questura", "numero titolo di soggiorno", "numero titolo soggiorno", "livello istruzione",
        "indirizzo domicilio", "indirizzo di domicilio", "indirizzo",
        "data inizio", "data fine periodo formativo", "data fine", "ente previdenziale",
        "codice ente previdenziale", "pat inail", "patinail", "tipologia contrattuale",
        "lavoro in mobilità", "lavoratore in mobilità", "lavoro stagionale", "socio lavoratore",
        "orario", "tipo orario", "ore settimanali medie", "ore settimanali",
        "qualifica professionale", "qualifica", "assunzione obbligatoria", "tipo lavorazione",
        "categoria lavoratore", "ccnl", "giornate previste", "lavoro in agricoltura",
        "livello inquadramento", "livello di inquadramento", "retribuzione/compenso", "retribuzione", "compenso",
        "settore", "comune sede legale", "cap sede legale", "cap sede di lavoro", "indirizzo sede legale",
        "comune sede di lavoro", "indirizzo sede di lavoro", "telefono", "fax", "email", "e-mail",
        "recapiti", "ragione sociale", "denominazione",
    };

    public static RisultatoImportUnilav Importa(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo))
            throw new ArgumentException("Il testo dell'UNILAV è vuoto: il PDF non contiene testo leggibile.");

        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').Select(r => r.Trim()).Where(r => r.Length > 0).ToList();

        // Indici delle intestazioni di sezione (qualsiasi tipo = confine di blocco).
        var intestazioni = new List<(int idx, string tipo)>();
        for (int i = 0; i < righe.Count; i++)
        {
            var t = TipoSezione(righe[i]);
            if (t is not null)
                intestazioni.Add((i, t));
        }

        string datoreBlk = Blocco(righe, intestazioni, "datore");
        string lavoratoreBlk = Blocco(righe, intestazioni, "lavoratore");
        string rapportoBlk = Blocco(righe, intestazioni, "rapporto");

        var datore = new DatoreLavoroUnilav(
            CodiceFiscaleOPiva(Campo(datoreBlk, "codice fiscale", "cod. fiscale", "c.f.", "partita iva", "p. iva", "p.iva")),
            Campo(datoreBlk, "ragione sociale", "denominazione"));

        var lavoratore = new LavoratoreUnilav(
            CodiceFiscale(Campo(lavoratoreBlk, "codice fiscale", "cod. fiscale", "c.f.")),
            Campo(lavoratoreBlk, "cognome"),
            Campo(lavoratoreBlk, "nome"),
            Campo(lavoratoreBlk, "sesso"),
            Data(Campo(lavoratoreBlk, "data di nascita", "data nascita")),
            Campo(lavoratoreBlk, "comune di nascita", "comune nascita"),
            Campo(lavoratoreBlk, "provincia di nascita", "provincia nascita"),
            Campo(lavoratoreBlk, "cittadinanza"),
            Campo(lavoratoreBlk, "comune di domicilio", "comune domicilio", "comune di residenza"),
            Campo(lavoratoreBlk, "provincia di domicilio", "provincia domicilio"),
            Campo(lavoratoreBlk, "c.a.p.", "cap"));

        var rapporto = new RapportoUnilav(
            Data(Campo(rapportoBlk, "data inizio", "data di inizio", "data assunzione", "decorrenza")),
            Data(Campo(rapportoBlk, "data fine", "data di fine", "data cessazione", "data termine")),
            Campo(rapportoBlk, "tipologia contrattuale", "tipo contratto", "tipo di contratto"),
            Campo(rapportoBlk, "ccnl", "c.c.n.l.", "contratto collettivo"),
            Campo(rapportoBlk, "qualifica professionale", "qualifica"),
            Campo(rapportoBlk, "livello di inquadramento", "livello inquadramento", "livello"),
            Campo(rapportoBlk, "tipo orario", "orario"),
            Numero(Campo(rapportoBlk, "percentuale part-time", "percentuale part time")),
            Numero(Campo(rapportoBlk, "ore settimanali medie", "ore settimanali", "ore medie settimanali")),
            Numero(Campo(rapportoBlk, "retribuzione/compenso", "retribuzione", "compenso")));

        var avvertenze = new List<string>();
        if (string.IsNullOrWhiteSpace(lavoratore.CodiceFiscale))
            avvertenze.Add("Codice fiscale del lavoratore non riconosciuto nel PDF: verificare e completare.");
        if (string.IsNullOrWhiteSpace(datore.CodiceFiscale) && string.IsNullOrWhiteSpace(datore.Denominazione))
            avvertenze.Add("Datore di lavoro non riconosciuto nel PDF: verificare e completare.");
        if (string.IsNullOrWhiteSpace(rapporto.Ccnl))
            avvertenze.Add("CCNL non riconosciuto: andrà indicato manualmente per il calcolo del cedolino.");
        if (string.IsNullOrWhiteSpace(rapporto.Livello) || EVuotoLogico(rapporto.Livello))
            avvertenze.Add("Livello di inquadramento non riconosciuto o non dichiarato: andrà indicato manualmente.");

        return new RisultatoImportUnilav
        {
            Datore = datore,
            Lavoratore = lavoratore,
            Rapporto = rapporto,
            Avvertenze = avvertenze,
        };
    }

    /// <summary>Classifica una riga come intestazione di sezione (datore/lavoratore/rapporto/altro) o null.</summary>
    private static string? TipoSezione(string riga)
    {
        // Toglie un eventuale prefisso "Sezione N - ".
        string n = Regex.Replace(riga.Trim(), @"(?i)^sezione\s+\d+\s*[-–]\s*", "").Trim().TrimEnd(':').Trim().ToLowerInvariant();

        if (n.Contains("mittente")) return "altro";                       // "Dati Mittente (se diverso da datore di lavoro)"
        if (n == "datore di lavoro") return "datore";
        if (n == "lavoratore") return "lavoratore";
        if (n is "dati rapporto" or "rapporto di lavoro" or "inizio" or "dati del rapporto" || n.StartsWith("inizio rapporto"))
            return "rapporto";
        if (n is "evento" or "rappresentante legale" or "proroga" or "sede legale" or "sede lavoro"
            or "dati invio" or "datore di lavoro estero" || n.StartsWith("dati invio"))
            return "altro";
        return null;
    }

    /// <summary>Testo della sezione richiesta: dalla sua intestazione fino alla successiva intestazione.</summary>
    private static string Blocco(List<string> righe, List<(int idx, string tipo)> intestazioni, string tipo)
    {
        (int idx, string tipo) h = intestazioni.FirstOrDefault(x => x.tipo == tipo, (-1, tipo));
        if (h.idx < 0)
            return string.Join('\n', righe); // sezione non trovata: si cerca su tutto il testo
        int fine = intestazioni.Where(x => x.idx > h.idx).Select(x => x.idx).DefaultIfEmpty(righe.Count).Min();
        return string.Join('\n', righe.GetRange(h.idx, fine - h.idx));
    }

    /// <summary>
    /// Valore di un campo: trova un'etichetta (parola intera) nel blocco e prende il testo fino alla
    /// prossima etichetta nota o all'a-capo (gestisce più campi sulla stessa riga).
    /// </summary>
    private static string? Campo(string blocco, params string[] etichette)
    {
        foreach (var et in etichette)
        {
            var m = Regex.Match(blocco, $@"(?<![\p{{L}}]){Regex.Escape(et)}(?=[\s:/]|$)\s*:?\s*", RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            int start = m.Index + m.Length;
            int end = blocco.IndexOf('\n', start);
            if (end < 0) end = blocco.Length;

            string resto = blocco[start..end];
            int taglia = resto.Length;
            foreach (var altra in EtichetteTaglio)
            {
                if (string.Equals(altra, et, StringComparison.OrdinalIgnoreCase)) continue;
                var ma = Regex.Match(resto, $@"(?<![\p{{L}}]){Regex.Escape(altra)}(?=[\s:/]|$)", RegexOptions.IgnoreCase);
                if (ma.Success && ma.Index < taglia) taglia = ma.Index;
            }

            string val = resto[..taglia].Trim().Trim(':', '-').Trim();
            if (val.Length > 0) return val;
        }
        return null;
    }

    private static bool EVuotoLogico(string? v) =>
        v is not null && (v.StartsWith("ND", StringComparison.OrdinalIgnoreCase)
            || v.Contains("non dichiarat", StringComparison.OrdinalIgnoreCase));

    private static string? CodiceFiscale(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var m = Regex.Match(valore, "[A-Za-z]{6}[0-9]{2}[A-Za-z][0-9]{2}[A-Za-z][0-9]{3}[A-Za-z]");
        return m.Success ? m.Value.ToUpperInvariant() : valore.Trim();
    }

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
