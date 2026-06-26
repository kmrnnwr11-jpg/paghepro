using System.Globalization;
using System.Xml.Linq;
using Paghe.Core.Domain;

namespace Paghe.Core.AdeTelematico;

/// <summary>Dati del datore di lavoro letti dall'UNILAV.</summary>
public sealed record DatoreLavoroUnilav(string? CodiceFiscale, string? Denominazione);

/// <summary>Dati anagrafici del lavoratore letti dall'UNILAV.</summary>
public sealed record LavoratoreUnilav(
    string? CodiceFiscale,
    string? Cognome,
    string? Nome,
    string? Sesso,
    DateOnly? DataNascita,
    string? ComuneNascita,
    string? ProvinciaNascita,
    string? Cittadinanza,
    string? ComuneDomicilio,
    string? ProvinciaDomicilio,
    string? CapDomicilio);

/// <summary>Dati del rapporto di lavoro (contratto) letti dall'UNILAV.</summary>
public sealed record RapportoUnilav(
    DateOnly? DataInizio,
    DateOnly? DataFine,
    string? TipoContratto,
    string? Ccnl,
    string? Qualifica,
    string? Livello,
    string? TipoOrario,
    decimal? PercentualePartTime,
    decimal? OreSettimanali,
    decimal? RetribuzionePrevista);

/// <summary>Risultato dell'import di un UNILAV: datore, lavoratore e rapporto riconosciuti.</summary>
public sealed class RisultatoImportUnilav
{
    public required DatoreLavoroUnilav Datore { get; init; }
    public required LavoratoreUnilav Lavoratore { get; init; }
    public required RapportoUnilav Rapporto { get; init; }
    public IReadOnlyList<string> Avvertenze { get; init; } = [];
}

/// <summary>
/// Importa la Comunicazione Obbligatoria UNILAV (XML standard nazionale) e ne riconosce
/// datore, lavoratore e rapporto, così da creare in automatico dipendente e rapporto senza
/// inserimento manuale. La lettura è tollerante al namespace e all'annidamento (ricerca per
/// nome locale), perché i tracciati reali variano per versione e portale regionale.
/// </summary>
public static class ImportazioneUnilav
{
    public static RisultatoImportUnilav Importa(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("Il contenuto UNILAV è vuoto.");

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new ArgumentException($"UNILAV non è un XML valido: {ex.Message}");
        }

        XElement radice = doc.Root!;
        XElement datore = Sezione(radice, "DatoreLavoro", "Datore") ?? radice;
        XElement lavoratore = Sezione(radice, "Lavoratore", "Dipendente") ?? radice;
        XElement rapporto = Sezione(radice, "InizioRapporto", "Inizio", "Rapporto", "Assunzione", "UnilavInizio") ?? radice;

        var avvertenze = new List<string>();

        var datoreDto = new DatoreLavoroUnilav(
            Campo(datore, "CodiceFiscale", "CodFisc", "CFDatore"),
            Campo(datore, "Denominazione", "RagioneSociale", "DenominazioneDatore"));

        var lavoratoreDto = new LavoratoreUnilav(
            Campo(lavoratore, "CodiceFiscale", "CodFisc"),
            Campo(lavoratore, "Cognome"),
            Campo(lavoratore, "Nome"),
            Campo(lavoratore, "Sesso"),
            Data(Campo(lavoratore, "DataNascita")),
            Campo(lavoratore, "ComuneNascita", "ComuneDiNascita"),
            Campo(lavoratore, "ProvinciaNascita", "SiglaProvinciaNascita"),
            Campo(lavoratore, "Cittadinanza", "CittadinanzaStato"),
            Campo(lavoratore, "ComuneDomicilio", "Comune", "ComuneResidenza"),
            Campo(lavoratore, "ProvinciaDomicilio", "Provincia", "SiglaProvincia"),
            Campo(lavoratore, "Cap", "CAP", "CapDomicilio"));

        var rapportoDto = new RapportoUnilav(
            Data(Campo(rapporto, "DataInizio", "DataInizioRapporto")),
            Data(Campo(rapporto, "DataFine", "DataFineRapporto", "DataCessazione")),
            Campo(rapporto, "TipoContratto", "TipologiaContrattuale", "Contratto"),
            Campo(rapporto, "CCNL", "Ccnl", "ContrattoApplicato", "ContrattoCollettivo", "CodiceContratto"),
            Campo(rapporto, "Qualifica", "QualificaProfessionale", "CodiceQualifica"),
            Campo(rapporto, "Livello", "LivelloInquadramento", "LivelloContratto"),
            Campo(rapporto, "TipoOrario", "Orario"),
            Numero(Campo(rapporto, "PercentualePartTime", "PercPartTime", "PercentualeOrario")),
            Numero(Campo(rapporto, "OreSettimanali", "OreSettimanaliMedie", "OreMedieSettimanali")),
            Numero(Campo(rapporto, "Retribuzione", "RetribuzionePrevista", "RetribuzioneEffettiva", "RetribuzioneAnnua")));

        if (string.IsNullOrWhiteSpace(lavoratoreDto.CodiceFiscale))
            avvertenze.Add("Codice fiscale del lavoratore non riconosciuto nell'UNILAV.");
        if (string.IsNullOrWhiteSpace(rapportoDto.Ccnl))
            avvertenze.Add("CCNL non riconosciuto: andrà indicato manualmente.");
        if (string.IsNullOrWhiteSpace(rapportoDto.Livello))
            avvertenze.Add("Livello di inquadramento non riconosciuto: andrà indicato manualmente.");

        return new RisultatoImportUnilav
        {
            Datore = datoreDto,
            Lavoratore = lavoratoreDto,
            Rapporto = rapportoDto,
            Avvertenze = avvertenze,
        };
    }

    /// <summary>
    /// Costruisce il rapporto di lavoro di dominio dai dati UNILAV. CCNL e livello sono
    /// obbligatori per il calcolo: se assenti nell'UNILAV vanno passati come default, altrimenti
    /// viene segnalato l'errore con l'elenco dei dati mancanti.
    /// </summary>
    public static RapportoLavoro CostruisciRapportoLavoro(
        RisultatoImportUnilav r, string? ccnlDefault = null, string? livelloDefault = null)
    {
        var rap = r.Rapporto;
        string? ccnl = !string.IsNullOrWhiteSpace(rap.Ccnl) ? rap.Ccnl : ccnlDefault;
        string? livello = !string.IsNullOrWhiteSpace(rap.Livello) ? rap.Livello : livelloDefault;

        var mancanti = new List<string>();
        if (string.IsNullOrWhiteSpace(ccnl)) mancanti.Add("CCNL");
        if (string.IsNullOrWhiteSpace(livello)) mancanti.Add("Livello");
        if (mancanti.Count > 0)
            throw new ArgumentException("Dati obbligatori assenti nell'UNILAV: " + string.Join(", ", mancanti));

        return new RapportoLavoro
        {
            CodiceCcnl = ccnl!,
            Livello = livello!,
            Qualifica = MappaQualifica(rap.Qualifica),
            PartTime = EParTime(rap.TipoOrario),
            PercentualePartTime = rap.PercentualePartTime ?? 100m,
        };
    }

    /// <summary>Mappa la qualifica UNILAV all'enum di dominio (euristica: default Impiegato).</summary>
    public static Qualifica MappaQualifica(string? qualifica)
    {
        string q = (qualifica ?? "").Trim().ToLowerInvariant();
        if (q.Contains("operai")) return Qualifica.Operaio;
        if (q.Contains("quadr")) return Qualifica.Quadro;
        if (q.Contains("dirigent")) return Qualifica.Dirigente;
        if (q.Contains("apprendist")) return Qualifica.Apprendista;
        return Qualifica.Impiegato;
    }

    /// <summary>Vero se l'orario UNILAV indica un part-time (codici che iniziano per "P").</summary>
    public static bool EParTime(string? tipoOrario) =>
        (tipoOrario ?? "").Trim().StartsWith("P", StringComparison.OrdinalIgnoreCase);

    private static XElement? Sezione(XElement radice, params string[] nomi) =>
        radice.DescendantsAndSelf()
            .FirstOrDefault(e => nomi.Any(n => string.Equals(e.Name.LocalName, n, StringComparison.OrdinalIgnoreCase)));

    private static string? Campo(XElement scope, params string[] nomi)
    {
        foreach (string n in nomi)
        {
            var el = scope.DescendantsAndSelf()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, n, StringComparison.OrdinalIgnoreCase)
                    && !e.HasElements);
            if (el is not null && !string.IsNullOrWhiteSpace(el.Value))
                return el.Value.Trim();
        }
        return null;
    }

    private static DateOnly? Data(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore))
            return null;
        string[] formati = ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyyMMdd"];
        return DateOnly.TryParseExact(valore.Trim(), formati, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateOnly.TryParse(valore.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2) ? d2 : null;
    }

    private static decimal? Numero(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore))
            return null;
        string v = valore.Trim();

        // "900,00" è decimale italiano; sotto cultura invariante la virgola sarebbe separatore di migliaia.
        bool virgolaDecimale = v.Contains(',') && !v.Contains('.');
        var prima = virgolaDecimale ? CultureInfo.GetCultureInfo("it-IT") : CultureInfo.InvariantCulture;
        var poi = virgolaDecimale ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("it-IT");

        if (decimal.TryParse(v, NumberStyles.Number, prima, out var n))
            return n;
        return decimal.TryParse(v, NumberStyles.Number, poi, out var n2) ? n2 : null;
    }
}
