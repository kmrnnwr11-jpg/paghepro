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
        "sede legale", "sede di lavoro", "sede lavoro", "nome e cognome", "cognome e nome",
        "data e luogo di nascita", "codice ateco", "ateco", "pubblica amministrazione",
    };

    public static RisultatoImportUnilav Importa(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo))
            throw new ArgumentException("Il testo dell'UNILAV è vuoto: il PDF non contiene testo leggibile.");

        var righe = Normalizza(testo)
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
        // Il rappresentante legale ha una sezione propria: se non c'è NON si cerca altrove,
        // altrimenti si prenderebbe per errore il nome del lavoratore.
        string? rappresentanteBlk = BloccoSeEsiste(righe, intestazioni, "rappresentante");

        var sede = Sede(Campo(datoreBlk, "sede legale", "sede di lavoro", "sede lavoro"));
        var (attecoDatore, settoreDatore) = Ateco(Campo(datoreBlk, "codice ateco", "ateco", "settore"));
        var (nomeRappr, cognomeRappr, _) = NomeCognome(rappresentanteBlk);

        var datore = new DatoreLavoroUnilav(
            CodiceFiscaleOPiva(Campo(datoreBlk, "codice fiscale", "cod. fiscale", "c.f.", "partita iva", "p. iva", "p.iva")),
            Campo(datoreBlk, "ragione sociale", "denominazione"),
            Indirizzo: Campo(datoreBlk, "indirizzo sede legale", "indirizzo sede di lavoro", "indirizzo") ?? sede.Indirizzo,
            Cap: Cap(Campo(datoreBlk, "cap sede legale", "cap sede di lavoro", "c.a.p.", "cap")) ?? sede.Cap,
            Comune: Campo(datoreBlk, "comune sede legale", "comune sede di lavoro", "comune") ?? sede.Comune,
            Provincia: Provincia(Campo(datoreBlk, "provincia sede legale", "provincia")) ?? sede.Provincia,
            Telefono: SoloCifre(Campo(datoreBlk, "telefono", "tel.")),
            Email: Email(Campo(datoreBlk, "e-mail", "email", "posta elettronica")),
            CodiceAteco: attecoDatore,
            Settore: settoreDatore,
            // Matricola INPS e P.A.T. INAIL sono dati dell'azienda, ma i moduli li stampano ora
            // nella sezione del datore ora in quella del rapporto: si cercano in entrambe.
            MatricolaInps: CodiceNumerico(Campo(datoreBlk, "matricola inps", "codice ente previdenziale", "ente previdenziale")
                ?? Campo(rapportoBlk, "matricola inps", "codice ente previdenziale", "ente previdenziale"), 8),
            PatInail: CodiceNumerico(Campo(datoreBlk, "pat inail", "patinail", "p.a.t. inail")
                ?? Campo(rapportoBlk, "pat inail", "patinail", "p.a.t. inail"), 6),
            RappresentanteNome: nomeRappr,
            RappresentanteCognome: cognomeRappr,
            RappresentanteCodiceFiscale: CodiceFiscale(Campo(rappresentanteBlk, "codice fiscale", "cod. fiscale", "c.f.")));

        var (nomeLav, cognomeLav, nomeCognomeUnico) = NomeCognome(lavoratoreBlk);

        var lavoratore = new LavoratoreUnilav(
            CodiceFiscale(Campo(lavoratoreBlk, "codice fiscale", "cod. fiscale", "c.f.")),
            cognomeLav,
            nomeLav,
            Campo(lavoratoreBlk, "sesso"),
            Data(Campo(lavoratoreBlk, "data di nascita", "data nascita", "data e luogo di nascita")),
            Campo(lavoratoreBlk, "comune di nascita", "comune nascita"),
            Campo(lavoratoreBlk, "provincia di nascita", "provincia nascita"),
            Campo(lavoratoreBlk, "cittadinanza"),
            Campo(lavoratoreBlk, "comune di domicilio", "comune domicilio", "comune di residenza"),
            Campo(lavoratoreBlk, "provincia di domicilio", "provincia domicilio"),
            Campo(lavoratoreBlk, "c.a.p.", "cap"),
            nomeCognomeUnico);

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

    /// <summary>
    /// Ripulisce il testo prima della lettura. Serve perché l'utente può <b>incollare</b> l'UNILAV
    /// così com'è oppure un riepilogo scritto a elenco puntato con i grassetti (Markdown):
    /// togliendo marcatori e puntini le etichette tornano riconoscibili come nel PDF.
    /// </summary>
    private static string Normalizza(string testo)
    {
        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(r => Regex.Replace(r, @"\*\*|__|`", ""))      // grassetto/corsivo Markdown
            .Select(r => Regex.Replace(r, @"^\s*[*+\-•·–]\s+", "")) // puntini di elenco a inizio riga
            .Select(r => r.Trim());
        return string.Join('\n', righe);
    }

    /// <summary>Classifica una riga come intestazione di sezione (datore/lavoratore/rapporto/altro) o null.</summary>
    private static string? TipoSezione(string riga)
    {
        // Toglie un eventuale prefisso "Sezione N - ".
        string n = Regex.Replace(riga.Trim(), @"(?i)^sezione\s+\d+\s*[-–]\s*", "").Trim().TrimEnd(':').Trim().ToLowerInvariant();
        // ...e l'attacco discorsivo dei riepiloghi ("Dati del Datore di Lavoro", "Dati Generali della
        // Comunicazione"): resta il nome della sezione, uguale a quello dei moduli ufficiali.
        n = Regex.Replace(n, @"^dati\s+(generali\s+)?(del|dello|della|dei|degli|dell')\s+", "").Trim();

        if (n.Contains("mittente")) return "altro";                       // "Dati Mittente (se diverso da datore di lavoro)"
        if (n == "datore di lavoro") return "datore";
        if (n == "lavoratore") return "lavoratore";
        if (n is "dati rapporto" or "rapporto di lavoro" or "inizio" or "dati del rapporto" or "rapporto"
            || n.StartsWith("inizio rapporto"))
            return "rapporto";
        if (n is "rappresentante legale" or "legale rappresentante") return "rappresentante";
        if (n is "evento" or "proroga" or "sede legale" or "sede lavoro" or "comunicazione"
            or "dati invio" or "datore di lavoro estero" || n.StartsWith("dati invio")
            || n.StartsWith("soggetto abilitato"))
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

    /// <summary>Come <see cref="Blocco"/>, ma null se la sezione non c'è (niente ricerca su tutto il testo).</summary>
    private static string? BloccoSeEsiste(List<string> righe, List<(int idx, string tipo)> intestazioni, string tipo)
    {
        (int idx, string tipo) h = intestazioni.FirstOrDefault(x => x.tipo == tipo, (-1, tipo));
        if (h.idx < 0) return null;
        int fine = intestazioni.Where(x => x.idx > h.idx).Select(x => x.idx).DefaultIfEmpty(righe.Count).Min();
        return string.Join('\n', righe.GetRange(h.idx, fine - h.idx));
    }

    /// <summary>
    /// Valore di un campo: trova un'etichetta (parola intera) nel blocco e prende il testo fino alla
    /// prossima etichetta nota o all'a-capo (gestisce più campi sulla stessa riga).
    /// </summary>
    private static string? Campo(string? blocco, params string[] etichette)
    {
        if (string.IsNullOrEmpty(blocco)) return null;
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

    /// <summary>Indirizzo scritto tutto su una riga: «VIA ROMA 1, 29017 FIORENZUOLA D'ARDA (PC)».</summary>
    private static (string? Indirizzo, string? Cap, string? Comune, string? Provincia) Sede(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return (null, null, null, null);
        var m = Regex.Match(valore.Trim(), @"^(?<via>.+?)[,;]?\s+(?<cap>\d{5})\s+(?<comune>[^()]+?)\s*(\((?<prov>[A-Za-z]{2})\))?$");
        if (!m.Success) return (valore.Trim(), null, null, null);
        return (
            m.Groups["via"].Value.Trim().TrimEnd(','),
            m.Groups["cap"].Value,
            m.Groups["comune"].Value.Trim(),
            m.Groups["prov"].Success ? m.Groups["prov"].Value.ToUpperInvariant() : null);
    }

    /// <summary>Settore: separa il codice ATECO («82.99.99») dalla descrizione dell'attività.</summary>
    private static (string? Ateco, string? Settore) Ateco(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return (null, null);
        var m = Regex.Match(valore, @"\d{2}(\.\d{1,2}){0,3}");
        string descrizione = (m.Success ? valore.Remove(m.Index, m.Length) : valore).Trim().Trim('-', '–', ':').Trim();
        return (m.Success ? m.Value : null, descrizione.Length > 0 ? descrizione : null);
    }

    /// <summary>
    /// Nome e cognome: se il documento li tiene separati si usano quelli, altrimenti si divide il
    /// campo unico «Nome e Cognome». In quel caso l'ordine non è certo: chi importa lo deve controllare
    /// (l'avvertenza viene aggiunta da chi salva).
    /// </summary>
    private static (string? Nome, string? Cognome, bool DaCampoUnico) NomeCognome(string? blocco)
    {
        // Il campo unico va cercato PRIMA: su una riga «Nome e Cognome: X Y» le etichette singole
        // «nome» e «cognome» aggancerebbero pezzi della stessa riga e sporcherebbero i due valori.
        string? nomePrima = Campo(blocco, "nome e cognome");
        string? cognomePrima = nomePrima is null ? Campo(blocco, "cognome e nome") : null;
        string? unico = nomePrima ?? cognomePrima;
        if (!string.IsNullOrWhiteSpace(unico))
        {
            var parti = unico.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parti.Length == 1) return (parti[0], null, true);
            string primo = parti[0], resto = string.Join(' ', parti[1..]);
            return cognomePrima is not null ? (resto, primo, true) : (primo, resto, true);
        }
        return (Campo(blocco, "nome")?.Trim(), Campo(blocco, "cognome")?.Trim(), false);
    }

    private static string? Cap(string? valore) => Estrai(valore, @"\d{5}");

    private static string? Provincia(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var m = Regex.Match(valore.Trim(), @"(?<![A-Za-z])[A-Za-z]{2}(?![A-Za-z])");
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    private static string? Email(string? valore) => Estrai(valore, @"[^\s<>@]+@[^\s<>@]+\.[A-Za-z]{2,}");

    private static string? SoloCifre(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        string cifre = Regex.Replace(valore, @"\D", "");
        return cifre.Length > 0 ? cifre : null;
    }

    /// <summary>
    /// Codice numerico dentro un valore descrittivo, es. «01 - INPS (Codice: 6104084700)» → la
    /// matricola. Prende la sequenza di cifre più lunga, se arriva alla lunghezza minima attesa.
    /// </summary>
    private static string? CodiceNumerico(string? valore, int cifreMinime)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var piuLunga = Regex.Matches(valore, @"\d+").Select(m => m.Value)
            .OrderByDescending(v => v.Length).FirstOrDefault();
        return piuLunga is not null && piuLunga.Length >= cifreMinime ? piuLunga : null;
    }

    private static string? Estrai(string? valore, string schema)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var m = Regex.Match(valore, schema);
        return m.Success ? m.Value : null;
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
