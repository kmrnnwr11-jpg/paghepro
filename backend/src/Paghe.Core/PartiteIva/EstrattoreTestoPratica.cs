using System.Globalization;
using System.Text.RegularExpressions;

namespace Paghe.Core.PartiteIva;

/// <summary>Dati per una pratica ComUnica estratti da testo libero (best-effort, da confermare).</summary>
public sealed record DatiPraticaEstratti(
    TipoPraticaComUnica? TipoPratica,
    FormaGiuridicaApertura? FormaGiuridica,
    RegimeContabilePartitaIva? RegimeContabile,
    DateOnly? DataDecorrenza,
    string? Denominazione,
    string? CodiceFiscale,
    string? PartitaIva,
    string? Pec,
    string? CodiceAteco,
    string? DescrizioneAttivita,
    string? Indirizzo,
    string? Cap,
    string? Comune,
    string? Provincia,
    bool? AttivitaCommercio,
    bool? AttivitaArtigiana,
    bool? ImpresaAgricola,
    bool? HaDipendentiPrevisti,
    bool? SoggettaInail,
    IReadOnlyList<string> CampiMancanti);

/// <summary>
/// Analizza un testo incollato dall'utente (email, appunti, messaggio del cliente) e ne estrae
/// i dati per compilare una pratica ComUnica. Parser <b>euristico</b> a etichette/parole chiave:
/// i dati vanno sempre rivisti; i campi non trovati finiscono in <c>CampiMancanti</c> così
/// l'interfaccia può chiederli uno per uno.
/// </summary>
public static class EstrattoreTestoPratica
{
    public static DatiPraticaEstratti Estrai(string testo)
    {
        testo ??= "";
        var minuscolo = testo.ToLowerInvariant();

        // Anagrafica di base: riusa il parser visura (denominazione, P.IVA, CF, sede, ATECO).
        var visura = VisuraCameraleParser.Estrai(testo);

        var codiceFiscale = visura.CodiceFiscale ?? CodiceFiscalePersonaFisica(testo);
        var tipo = RilevaTipo(minuscolo);
        var forma = RilevaForma(minuscolo);
        var regime = RilevaRegime(minuscolo);
        var denominazione = visura.RagioneSociale ?? Denominazione(testo);
        // La descrizione si ferma a . o ; per non trascinarsi il resto della frase ("... . Sede: ...").
        var descrizione = DopoEtichetta(testo,
            "descrizione attivit", "attività svolta", "descrizione", "che attivit") is { } d
            ? Compatta(d.Split('.', ';')[0])
            : null;
        var pec = Pec(testo);
        var data = Data(testo);
        var (indirizzo, cap, comune, provincia) = Sede(testo, visura);

        bool? commercio = Presente(minuscolo, "negozio", "commercio", "vendita al", "bar ", "ristorant",
            "market", "ambulante", "e-commerce", "bottega");
        bool? artigiana = Presente(minuscolo, "artigian", "parrucchier", "estetist", "idraulic",
            "elettricist", "falegnam", "edil", "muratore", "meccanic", "sart");
        bool? agricola = Presente(minuscolo, "agricol", "coltiv", "allevament", "azienda agraria");
        bool? dipendenti = Presente(minuscolo, "dipendent", "assumer", "assunzion", "personale");
        bool? inail = Presente(minuscolo, "inail", "infortun");

        var mancanti = new List<string>();
        if (tipo is null) mancanti.Add("tipo pratica (apertura, variazione o cessazione)");
        if (denominazione is null) mancanti.Add("denominazione / nome e cognome");
        if (codiceFiscale is null) mancanti.Add("codice fiscale");
        if (forma is null) mancanti.Add("forma giuridica (professionista, impresa individuale, società)");
        if (regime is null) mancanti.Add("regime contabile (forfettario, semplificato, ordinario)");
        if (data is null) mancanti.Add("data di decorrenza (quando inizia, cambia o chiude)");
        if (visura.CodiceAteco is null) mancanti.Add("codice ATECO");
        if (descrizione is null) mancanti.Add("descrizione dell'attività");
        if (indirizzo is null) mancanti.Add("indirizzo della sede");
        if (cap is null) mancanti.Add("CAP");
        if (comune is null) mancanti.Add("comune");
        if (provincia is null) mancanti.Add("provincia (2 lettere)");
        if (visura.PartitaIva is null && tipo is TipoPraticaComUnica.Variazione or TipoPraticaComUnica.Cessazione)
            mancanti.Add("partita IVA (obbligatoria per variazione e cessazione)");
        if (pec is null && forma is not null && forma != FormaGiuridicaApertura.ProfessionistaPersonaFisica)
            mancanti.Add("PEC (serve quasi sempre per il Registro Imprese)");

        return new DatiPraticaEstratti(
            tipo, forma, regime, data,
            denominazione, codiceFiscale, visura.PartitaIva, pec,
            visura.CodiceAteco, descrizione,
            indirizzo, cap, comune, provincia,
            commercio, artigiana, agricola, dipendenti, inail,
            mancanti);
    }

    // Sede: parte dai dati visura e, se l'indirizzo manca, prova l'etichetta semplice
    // "sede:" nel formato comune "Via X 1 - 20121 Milano (MI)".
    private static (string? indirizzo, string? cap, string? comune, string? provincia) Sede(
        string testo, DatiAziendaDaVisura visura)
    {
        var (indirizzo, cap, comune, provincia) = (visura.Indirizzo, visura.Cap, visura.Comune, visura.Provincia);
        if (indirizzo is not null) return (indirizzo, cap, comune, provincia);

        if (DopoEtichetta(testo, "sede") is { } sede)
        {
            var m = Regex.Match(sede, @"^(.*?)[,\-–—]?\s*(\d{5})\s+([A-Za-zÀ-ÿ' ]+?)\s*(?:\(([A-Za-z]{2})\))?\s*[.;]?\s*$");
            if (m.Success)
            {
                indirizzo = Compatta(m.Groups[1].Value.Trim(' ', ',', '-'));
                cap ??= m.Groups[2].Value;
                comune ??= Compatta(m.Groups[3].Value);
                provincia ??= m.Groups[4].Success ? m.Groups[4].Value.ToUpperInvariant() : null;
            }
            else
            {
                indirizzo = Compatta(sede.Split('.', ';')[0]);
            }
        }
        return (indirizzo, cap, comune, provincia);
    }

    private static TipoPraticaComUnica? RilevaTipo(string t)
    {
        if (Regex.IsMatch(t, @"cessazion|chiusura|chiude(re)?\b|cessa\b")) return TipoPraticaComUnica.Cessazione;
        if (Regex.IsMatch(t, @"variazion|trasferiment|cambio (di )?(sede|attivit|indirizzo)|modifica"))
            return TipoPraticaComUnica.Variazione;
        if (Regex.IsMatch(t, @"apertura|aprire|nuova (impresa|attivit|partita)|inizio attivit|apre\b"))
            return TipoPraticaComUnica.NuovaImpresa;
        return null;
    }

    private static FormaGiuridicaApertura? RilevaForma(string t)
    {
        if (Regex.IsMatch(t, @"\bs\.?r\.?l\.?s?\b|\bs\.?p\.?a\b|società di capitali"))
            return FormaGiuridicaApertura.SocietaCapitali;
        if (Regex.IsMatch(t, @"\bs\.?n\.?c\b|\bs\.?a\.?s\b|società di persone"))
            return FormaGiuridicaApertura.SocietaPersone;
        if (Regex.IsMatch(t, @"impresa individuale|ditta individuale"))
            return FormaGiuridicaApertura.ImpresaIndividuale;
        if (Regex.IsMatch(t, @"professionist|freelance|libero professionista"))
            return FormaGiuridicaApertura.ProfessionistaPersonaFisica;
        return null;
    }

    private static RegimeContabilePartitaIva? RilevaRegime(string t)
    {
        if (t.Contains("forfettario") || t.Contains("forfetario")) return RegimeContabilePartitaIva.Forfettario;
        if (t.Contains("semplificato")) return RegimeContabilePartitaIva.Semplificato;
        if (t.Contains("ordinario")) return RegimeContabilePartitaIva.Ordinario;
        return null;
    }

    // Nome/denominazione da frasi naturali ("si chiama Anna Verdi") o etichette con i due punti.
    // Cattura fino al primo segno di punteggiatura per non trascinarsi il resto della frase.
    private static string? Denominazione(string testo)
    {
        var m = Regex.Match(testo, @"si chiama\s*:?\s*([^,.;\n]+)", RegexOptions.IgnoreCase);
        if (m.Success) return Compatta(m.Groups[1].Value);
        foreach (var e in new[] { "nome e cognome", "nominativo", "nome" })
        {
            m = Regex.Match(testo, $@"{Regex.Escape(e)}\s*:\s*([^,.;\n]+)", RegexOptions.IgnoreCase);
            if (m.Success) return Compatta(m.Groups[1].Value);
        }
        return null;
    }

    private static string Compatta(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    // CF di persona fisica (16 caratteri), ovunque nel testo.
    private static string? CodiceFiscalePersonaFisica(string testo)
    {
        var m = Regex.Match(testo, @"\b([A-Za-z]{6}\d{2}[A-Za-z]\d{2}[A-Za-z]\d{3}[A-Za-z])\b");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string? Pec(string testo)
    {
        var email = Regex.Matches(testo, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");
        // Preferisce un indirizzo che contenga "pec", altrimenti quello dopo l'etichetta PEC.
        foreach (Match m in email)
            if (m.Value.Contains("pec", StringComparison.OrdinalIgnoreCase)) return m.Value;
        var dopo = DopoEtichetta(testo, "pec");
        if (dopo is not null)
        {
            var m = Regex.Match(dopo, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");
            if (m.Success) return m.Value;
        }
        return null;
    }

    private static DateOnly? Data(string testo)
    {
        // gg/mm/aaaa (o con - .), il formato in cui la gente scrive le date.
        var m = Regex.Match(testo, @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})\b");
        if (m.Success &&
            DateOnly.TryParseExact($"{int.Parse(m.Groups[1].Value):00}/{int.Parse(m.Groups[2].Value):00}/{m.Groups[3].Value}",
                "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        // aaaa-mm-gg (chi incolla da un altro programma).
        var iso = Regex.Match(testo, @"\b(\d{4})-(\d{2})-(\d{2})\b");
        if (iso.Success && DateOnly.TryParse(iso.Value, CultureInfo.InvariantCulture, out var dIso))
            return dIso;
        return null;
    }

    private static bool? Presente(string minuscolo, params string[] parole) =>
        parole.Any(minuscolo.Contains) ? true : null;

    private static string? DopoEtichetta(string testo, params string[] etichette)
    {
        foreach (var e in etichette)
        {
            var m = Regex.Match(testo, $@"{Regex.Escape(e)}\w*\s*[:]\s*(.+)", RegexOptions.IgnoreCase);
            if (m.Success && m.Groups[1].Value.Trim().Length > 0)
                return Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim();
        }
        return null;
    }
}
