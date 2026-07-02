using System.Globalization;
using System.Text.RegularExpressions;

namespace Paghe.Core.Parametri;

/// <summary>
/// Interpreta i file CSV ufficiali del MEF (Dipartimento delle Finanze) delle addizionali IRPEF
/// e li trasforma in <see cref="RegimeAddizionale"/> (soglia di esenzione + scaglioni), pronti
/// per il motore di calcolo. Gestisce: aliquote in percentuale con la virgola (es. ",6" = 0,60%),
/// aliquota unica, esenzioni "a soglia" (colonna IMPORTO_ESENTE) e scaglioni di reddito.
/// </summary>
public static class ImportAddizionaliMef
{
    // ---- Addizionale COMUNALE -----------------------------------------------------------------
    // Colonne: CODICE_CATASTALE;COMUNE;PR;NUMERO_DELIBERA;DATA_DELIBERA;DATA_PUBBLICAZIONE;NOTE;
    // MULTIALIQ;ALIQUOTA;FASCIA;ALIQUOTA_2;FASCIA_2;...;ALIQUOTA_12;FASCIA_12;FLAG_NUOVA;IMPORTO_ESENTE
    private const int ColCodiceCatastale = 0;
    private const int ColPrimaAliquota = 8;   // ALIQUOTA
    private const int ColUltimaCoppia = 30;   // ALIQUOTA_12 (la fascia è a +1)

    /// <summary>Legge il CSV comunale MEF → mappa codice catastale → regime.</summary>
    public static IReadOnlyDictionary<string, RegimeAddizionale> LeggiComunali(IEnumerable<string> righe)
    {
        var mappa = new Dictionary<string, RegimeAddizionale>(StringComparer.OrdinalIgnoreCase);
        bool prima = true;
        foreach (var riga in righe)
        {
            if (string.IsNullOrWhiteSpace(riga)) continue;
            if (prima) { prima = false; continue; } // salta intestazione

            var c = riga.Split(';');
            if (c.Length < 34) continue;

            var codice = c[ColCodiceCatastale].Trim();
            if (codice.Length == 0) continue;

            decimal soglia = ParseEuro(c[^1]) ?? 0m; // IMPORTO_ESENTE (ultima colonna)

            var scaglioni = new List<ScaglioneAddizionale>();
            for (int i = ColPrimaAliquota; i <= ColUltimaCoppia && i + 1 < c.Length; i += 2)
            {
                var aliq = ParseAliquotaFrazione(c[i]);
                if (aliq is null) continue; // vuoto o "0*"

                var fascia = c[i + 1];
                // Alcuni comuni indicano l'esenzione solo nel testo della fascia (IMPORTO_ESENTE = 0).
                if (EsenzioneGenerale(fascia, out var sogliaDaFascia))
                {
                    soglia = Math.Max(soglia, sogliaDaFascia);
                    continue;
                }
                var sc = ScaglioneDaFascia(fascia, aliq.Value);
                if (sc is not null) scaglioni.Add(sc);
            }

            mappa[codice] = ComponiRegime(soglia, scaglioni);
        }
        return mappa;
    }

    // ---- Addizionale REGIONALE ----------------------------------------------------------------
    // Colonne: ANNO;REGIONE;NUMERO;DATA PUBBLICAZIONE;DISPOSIZIONE;NORME;NOTE;ALIQUOTA;FASCIA
    private const int ColRegione = 1;
    private const int ColAliquotaReg = 7;
    private const int ColFasciaReg = 8;

    /// <summary>Legge il CSV regionale MEF → mappa nome regione (come nel dataset comuni) → regime.</summary>
    public static IReadOnlyDictionary<string, RegimeAddizionale> LeggiRegionali(IEnumerable<string> righe)
    {
        // Raccoglie gli scaglioni per nome MEF della regione.
        var perRegione = new Dictionary<string, List<ScaglioneAddizionale>>(StringComparer.OrdinalIgnoreCase);
        bool prima = true;
        foreach (var riga in righe)
        {
            if (string.IsNullOrWhiteSpace(riga)) continue;
            if (prima) { prima = false; continue; }

            var c = riga.Split(';');
            if (c.Length <= ColFasciaReg) continue;

            var nomeMef = c[ColRegione].Trim();
            var aliq = ParseAliquotaFrazione(c[ColAliquotaReg]);
            if (nomeMef.Length == 0 || aliq is null) continue;

            var sc = ScaglioneDaFascia(c[ColFasciaReg], aliq.Value);
            if (sc is null) continue;

            if (!perRegione.TryGetValue(nomeMef, out var lista))
                perRegione[nomeMef] = lista = new List<ScaglioneAddizionale>();
            lista.Add(sc);
        }

        // Traduce i nomi MEF nei nomi usati nel dataset comuni del frontend.
        var risultato = new Dictionary<string, RegimeAddizionale>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nomeMef, scaglioni) in perRegione)
        {
            var nome = NomeRegioneFrontend(nomeMef);
            if (nome is null) continue;
            risultato[nome] = ComponiRegime(0m, scaglioni);
        }
        return risultato;
    }

    // ---- Composizione del regime --------------------------------------------------------------

    /// <summary>Ordina, arrotonda e concatena gli scaglioni in un regime coerente (progressivo).</summary>
    private static RegimeAddizionale ComponiRegime(decimal soglia, List<ScaglioneAddizionale> scaglioni)
    {
        var validi = scaglioni.Where(s => s.Aliquota > 0m).ToList();
        if (validi.Count == 0)
            return RegimeAddizionale.Nessuno;

        if (validi.Count == 1)
            // aliquota unica sull'intero imponibile (con eventuale soglia di esenzione)
            return new RegimeAddizionale(soglia, new[] { new ScaglioneAddizionale(0m, null, validi[0].Aliquota) });

        validi = validi.OrderBy(s => s.DaImporto).ToList();
        var catena = new List<ScaglioneAddizionale>();
        decimal da = 0m;
        for (int i = 0; i < validi.Count; i++)
        {
            decimal? a = i == validi.Count - 1
                ? null // ultimo scaglione aperto
                : (validi[i].AImporto.HasValue ? Math.Round(validi[i].AImporto!.Value) : null);
            catena.Add(new ScaglioneAddizionale(da, a, validi[i].Aliquota));
            da = a ?? da;
        }
        return new RegimeAddizionale(soglia, catena);
    }

    /// <summary>
    /// Vero se la fascia è un'esenzione. Per le esenzioni <b>generali</b> per reddito
    /// (es. "Esenzione per redditi imponibili fino a euro 12.000,00") restituisce l'importo in
    /// <paramref name="soglia"/>; per quelle di <b>categoria</b> (pensionati, lavoro dipendente,
    /// figli a carico, disabilità) non imposta una soglia generale (soglia = 0).
    /// </summary>
    private static bool EsenzioneGenerale(string fascia, out decimal soglia)
    {
        soglia = 0m;
        var low = (fascia ?? "").ToLowerInvariant();
        if (!low.Contains("esenzione")) return false;

        bool categoria = low.Contains("pension") || low.Contains("dipendent") || low.Contains("assimilat")
            || low.Contains("figli") || low.Contains("handicap") || low.Contains("disabil") || low.Contains("ultra");
        if (categoria) return true; // è un'esenzione, ma di categoria: nessuna soglia generale

        var nums = EstraiEuro(fascia);
        if (nums.Count >= 1) soglia = nums[0];
        return true;
    }

    /// <summary>Ricava lo scaglione [da, a) dal testo della fascia MEF; null se è un'esenzione.</summary>
    private static ScaglioneAddizionale? ScaglioneDaFascia(string fascia, decimal aliquota)
    {
        var low = (fascia ?? "").Trim().ToLowerInvariant();
        if (low.Contains("esenzione")) return null;           // gestita dalla soglia (IMPORTO_ESENTE)
        if (low.Length == 0 || low.Contains("unica")) return new ScaglioneAddizionale(0m, null, aliquota);

        var nums = EstraiEuro(fascia);
        bool oltre = low.Contains("oltre");
        bool fino = low.Contains("fino");
        bool da = Regex.IsMatch(low, @"\bda\b");

        if (oltre && fino && nums.Count >= 2) return new ScaglioneAddizionale(nums[0], nums[1], aliquota);
        if (da && fino && nums.Count >= 2) return new ScaglioneAddizionale(nums[0], nums[1], aliquota);
        if (oltre && nums.Count >= 1) return new ScaglioneAddizionale(nums[0], null, aliquota);
        if (fino && nums.Count >= 1) return new ScaglioneAddizionale(0m, nums[0], aliquota);
        if (nums.Count >= 2) return new ScaglioneAddizionale(nums[0], nums[1], aliquota);
        if (nums.Count == 1) return new ScaglioneAddizionale(0m, nums[0], aliquota);
        return new ScaglioneAddizionale(0m, null, aliquota);
    }

    // ---- Parsing dei numeri (formato italiano) -----------------------------------------------

    /// <summary>Aliquota MEF (percentuale, es. ",6" o "0,8" o "1.23") → frazione (0,006 / 0,008 / 0,0123). "0*"/vuoto → null.</summary>
    private static decimal? ParseAliquotaFrazione(string raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0 || s.Contains('*')) return null;
        if (s.StartsWith(",")) s = "0" + s;
        if (s.Contains(',')) s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct)
            ? pct / 100m
            : null;
    }

    /// <summary>Importo in euro dal primo token numerico (gestisce "15.000,00" e "15000.00").</summary>
    private static decimal? ParseEuro(string raw)
    {
        var m = Regex.Match(raw ?? "", @"\d[\d.,]*");
        if (!m.Success) return null;
        var t = m.Value;
        if (t.Contains(','))
            t = t.Replace(".", "").Replace(",", ".");
        else if (t.Contains('.') && Regex.IsMatch(t, @"^\d{1,3}(\.\d{3})+$"))
            t = t.Replace(".", "");
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>Estrae dalla fascia gli importi euro plausibili (0 o ≥ 1000).</summary>
    private static List<decimal> EstraiEuro(string fascia)
    {
        var lista = new List<decimal>();
        foreach (Match m in Regex.Matches(fascia ?? "", @"\d[\d.,]*"))
        {
            var v = ParseEuro(m.Value);
            if (v is decimal d && (d == 0m || d >= 1000m))
                lista.Add(d);
        }
        return lista;
    }

    // ---- Nomi regione: da MEF al dataset comuni del frontend ----------------------------------
    private static readonly Dictionary<string, string> RegioniMef = new(StringComparer.OrdinalIgnoreCase)
    {
        ["REGIONE ABRUZZO"] = "Abruzzo",
        ["REGIONE BASILICATA"] = "Basilicata",
        ["REGIONE CALABRIA"] = "Calabria",
        ["REGIONE CAMPANIA"] = "Campania",
        ["REGIONE EMILIA-ROMAGNA"] = "Emilia-Romagna",
        ["REGIONE FRIULI VENEZIA GIULIA"] = "Friuli-Venezia Giulia",
        ["REGIONE LAZIO"] = "Lazio",
        ["REGIONE LIGURIA"] = "Liguria",
        ["REGIONE LOMBARDIA"] = "Lombardia",
        ["REGIONE MARCHE"] = "Marche",
        ["REGIONE MOLISE"] = "Molise",
        ["REGIONE PIEMONTE"] = "Piemonte",
        ["REGIONE PUGLIA"] = "Puglia",
        ["REGIONE SARDEGNA"] = "Sardegna",
        ["REGIONE SICILIA"] = "Sicilia",
        ["REGIONE TOSCANA"] = "Toscana",
        ["REGIONE UMBRIA"] = "Umbria",
        ["REGIONE VALLE D'AOSTA"] = "Valle d'Aosta/Vallée d'Aoste",
        ["REGIONE VENETO"] = "Veneto",
        // Il dataset comuni ha un'unica voce Trentino; il MEF distingue le due Province autonome.
        ["PROVINCIA AUTONOMA DI TRENTO"] = "Trentino-Alto Adige/Südtirol",
    };

    private static string? NomeRegioneFrontend(string nomeMef) =>
        RegioniMef.TryGetValue(nomeMef.Trim(), out var n) ? n : null;
}
