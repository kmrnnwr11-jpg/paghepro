using System.Text.RegularExpressions;

namespace Paghe.Core.PartiteIva;

/// <summary>Dati di un'azienda estratti da una visura camerale (best-effort: vanno sempre controllati).</summary>
public sealed record DatiAziendaDaVisura(
    string? RagioneSociale,
    string? PartitaIva,
    string? CodiceFiscale,
    string? Indirizzo,
    string? Cap,
    string? Comune,
    string? Provincia,
    string? CodiceAteco,
    string? LegaleRappresentante);

/// <summary>
/// Estrae i dati anagrafici di un'azienda dal <b>testo</b> di una visura camerale del Registro Imprese.
/// È un parser <b>euristico</b> (basato su etichette + pattern): i formati delle visure variano, quindi
/// i dati estratti vanno <b>sempre rivisti e confermati</b> prima di salvare. Non esegue OCR su immagini:
/// richiede una visura in PDF testuale (il testo va estratto a monte).
/// </summary>
public static partial class VisuraCameraleParser
{
    public static DatiAziendaDaVisura Estrai(string testo)
    {
        testo ??= "";
        var righe = testo.Replace("\r", "").Split('\n').Select(r => r.Trim()).ToList();

        var partitaIva = DopoEtichetta(testo, "partita iva", "p\\.?\\s*iva") is { } piva
            ? SoloCifre(piva, 11)
            : PrimoMatch(testo, @"\b(\d{11})\b");

        var codiceFiscale = DopoEtichetta(testo, "codice fiscale", "cod\\.?\\s*fisc") is { } cf
            ? PrimoMatch(cf, @"\b([A-Z0-9]{11,16})\b")
            : null;

        var ragioneSociale = ValoreRiga(righe, "denominazione")
            ?? ValoreRiga(righe, "ragione sociale")
            ?? ValoreRiga(righe, "ditta");

        var (indirizzo, cap, comune, provincia) = EstraiSede(righe, testo);

        var ateco = DopoEtichetta(testo, "codice ateco", "ateco") is { } at
            ? PrimoMatch(at, @"(\d{2}[.\d]*)")
            : null;

        var legale = ValoreRiga(righe, "rappresentante legale")
            ?? ValoreRiga(righe, "legale rappresentante")
            ?? ValoreRiga(righe, "amministratore unico")
            ?? ValoreRiga(righe, "titolare");

        return new DatiAziendaDaVisura(
            Pulisci(ragioneSociale), partitaIva, codiceFiscale,
            Pulisci(indirizzo), cap, Pulisci(comune), provincia?.ToUpperInvariant(),
            ateco, Pulisci(legale));
    }

    /// <summary>Testo che segue un'etichetta (sulla stessa riga), provando più varianti di etichetta.</summary>
    private static string? DopoEtichetta(string testo, params string[] etichette)
    {
        foreach (var e in etichette)
        {
            var m = Regex.Match(testo, $@"{e}\s*[:.]?\s*(.+)", RegexOptions.IgnoreCase);
            if (m.Success && m.Groups[1].Value.Trim().Length > 0)
                return m.Groups[1].Value.Trim();
        }
        return null;
    }

    /// <summary>Valore dopo un'etichetta cercata riga per riga (gestisce "Etichetta: valore" e "Etichetta\nvalore").</summary>
    private static string? ValoreRiga(List<string> righe, string etichetta)
    {
        for (int i = 0; i < righe.Count; i++)
        {
            if (!righe[i].Contains(etichetta, StringComparison.OrdinalIgnoreCase)) continue;
            var dopoSep = Regex.Match(righe[i], $@"{Regex.Escape(etichetta)}\s*[:.]?\s*(.+)", RegexOptions.IgnoreCase);
            if (dopoSep.Success && dopoSep.Groups[1].Value.Trim().Length > 0)
                return dopoSep.Groups[1].Value.Trim();
            // valore sulla riga successiva
            if (i + 1 < righe.Count && righe[i + 1].Length > 0)
                return righe[i + 1].Trim();
        }
        return null;
    }

    private static (string? indirizzo, string? cap, string? comune, string? prov) EstraiSede(
        List<string> righe, string testo)
    {
        var rigaSede = ValoreRiga(righe, "indirizzo sede")
            ?? ValoreRiga(righe, "sede legale")
            ?? ValoreRiga(righe, "indirizzo");

        var cap = PrimoMatch(testo, @"\b(\d{5})\b");
        var prov = rigaSede is not null ? PrimoMatch(rigaSede, @"\(([A-Za-z]{2})\)") : null;

        string? indirizzo = rigaSede, comune = null;
        if (rigaSede is not null)
        {
            // formato tipico: "VIA ROMA 10 - 20121 MILANO (MI)"
            var m = Regex.Match(rigaSede, @"^(.*?)[,\-]?\s*\d{5}\s+([A-Za-zÀ-ÿ' ]+?)\s*(\([A-Za-z]{2}\))?$");
            if (m.Success)
            {
                indirizzo = m.Groups[1].Value.Trim(' ', ',', '-');
                comune = m.Groups[2].Value.Trim();
            }
        }
        return (indirizzo, cap, comune, prov);
    }

    private static string? SoloCifre(string s, int lunghezza)
    {
        var m = Regex.Match(s, $@"(\d{{{lunghezza}}})");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? PrimoMatch(string s, string pattern)
    {
        var m = Regex.Match(s, pattern, RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? Pulisci(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var v = Regex.Replace(s, @"\s+", " ").Trim();
        return v.Length == 0 ? null : v;
    }
}
