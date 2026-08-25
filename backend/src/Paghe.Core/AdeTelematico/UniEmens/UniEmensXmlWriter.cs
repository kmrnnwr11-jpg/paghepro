using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Genera l'XML del flusso UniEmens secondo il tracciato ufficiale INPS (schema 4.32.0).
/// L'ordine degli elementi è vincolante — lo schema usa sequenze — quindi va rispettato
/// esattamente come qui sotto. Il file prodotto va sempre validato: prima con
/// <see cref="UniEmensValidator"/>, poi con il software di controllo ufficiale INPS, che è
/// l'unico a poter dichiarare il flusso trasmissibile.
/// </summary>
public static class UniEmensXmlWriter
{
    /// <summary>
    /// Importo in euro interi (tipo <c>Importo</c> dello schema: intero non negativo).
    /// Usato per imponibile e retribuzione teorica.
    /// </summary>
    private static string EuroInteri(decimal v) =>
        Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Importo con i centesimi separati dalla <b>virgola</b> (tipo <c>ImportoConDec</c>).
    /// Lo schema non ammette zeri non significativi in testa e vuole "0" secco per lo zero.
    /// </summary>
    private static string EuroConDecimali(decimal v)
    {
        decimal arrotondato = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        if (arrotondato == 0m) return "0";
        return arrotondato.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private static string Intero(int v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Adatta un testo anagrafico al tracciato: l'INPS ammette solo maiuscole, cifre e pochi
    /// segni, quindi «Nicolò D'Amico» va scritto «NICOLO D'AMICO». L'archivio conserva il nome
    /// come si scrive davvero: la conversione vive qui, sul confine con il file.
    /// </summary>
    private static string TestoTracciato(string? valore, int lunghezzaMassima)
    {
        string senzaAccenti = new string((valore ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();

        var ammessi = senzaAccenti
            .Where(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || AmmessiNeiNomi.Contains(c))
            .ToArray();
        string pulito = new string(ammessi).Trim();
        return pulito.Length > lunghezzaMassima ? pulito[..lunghezzaMassima] : pulito;
    }

    /// <summary>Segni ammessi dal tracciato nei nomi e nelle ragioni sociali.</summary>
    private const string AmmessiNeiNomi = " '-.*/";

    public static XDocument Genera(FlussoUniEmens flusso)
    {
        ArgumentNullException.ThrowIfNull(flusso);

        var radice = new XElement("DenunceMensili",
            Mittente(flusso.Mittente),
            Azienda(flusso.Azienda));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), radice);
    }

    public static string GeneraStringa(FlussoUniEmens flusso) => Genera(flusso).ToString();

    private static XElement Mittente(MittenteUniEmens m)
    {
        var el = new XElement("DatiMittente",
            new XAttribute("Tipo", m.Tipo),
            new XElement("CFPersonaMittente", m.CfPersonaMittente),
            new XElement("RagSocMittente", TestoTracciato(m.RagioneSociale, 50)),
            new XElement("CFMittente", m.CfMittente),
            new XElement("CFSoftwarehouse", m.CfSoftwarehouse));
        if (!string.IsNullOrWhiteSpace(m.SedeInps))
            el.Add(new XElement("SedeINPS", m.SedeInps));
        return el;
    }

    private static XElement Azienda(AziendaUniEmens a) =>
        new("Azienda",
            new XElement("AnnoMeseDenuncia", $"{a.Anno:0000}-{a.Mese:00}"),
            new XElement("CFAzienda", a.CfAzienda),
            new XElement("RagSocAzienda", TestoTracciato(a.RagioneSociale, 50)),
            Posizione(a.Posizione));

    private static XElement Posizione(PosizioneContributivaUniEmens p)
    {
        var el = new XElement("PosContributiva",
            new XAttribute("Composizione", p.Composizione),
            new XElement("Matricola", p.Matricola));
        foreach (var lavoratore in p.Lavoratori)
            el.Add(Individuale(lavoratore));
        el.Add(Aziendale(p.Aziendale));
        return el;
    }

    private static XElement Individuale(DenunciaIndividualeUniEmens d)
    {
        var el = new XElement("DenunciaIndividuale",
            new XElement("CFLavoratore", d.CodiceFiscale),
            new XElement("Cognome", TestoTracciato(d.Cognome, 30)),
            new XElement("Nome", TestoTracciato(d.Nome, 30)),
            new XElement("Qualifica1", d.Qualifica1));

        // L'ordine che segue è quello della sequenza dello schema: spostare una riga
        // rende il file non valido anche se i dati sono giusti.
        AggiungiSePresente(el, "Qualifica2", d.Qualifica2);
        AggiungiSePresente(el, "Qualifica3", d.Qualifica3);
        AggiungiSePresente(el, "TipoContribuzione", d.TipoContribuzione);
        AggiungiSePresente(el, "RegimePost95", d.RegimePost95);
        AggiungiSePresente(el, "Cittadinanza", d.Cittadinanza);
        el.Add(new XElement("UnitaOperativa", Intero(d.UnitaOperativa)));
        el.Add(new XElement("UnitaProduttiva", Intero(d.UnitaProduttiva)));
        AggiungiSePresente(el, "CodiceComune", d.CodiceComune);
        AggiungiSePresente(el, "CodiceContratto", d.CodiceContratto);
        AggiungiSePresente(el, "TipoCodiceContratto", d.TipoCodiceContratto);
        AggiungiSePresente(el, "QualProf", d.QualProf);
        AggiungiSePresente(el, "TipoPaga", d.TipoPaga);
        AggiungiSePresente(el, "DivisoreOrarioContr", d.DivisoreOrarioContr);
        AggiungiSePresente(el, "OrarioContrattuale", d.OrarioContrattuale);
        AggiungiSePresente(el, "PercPartTime", d.PercPartTime);
        AggiungiSePresente(el, "PercPartTimeMese", d.PercPartTimeMese);
        AggiungiSePresente(el, "NumMensilita", d.NumMensilita);

        el.Add(Retributivi(d.Retribuzione));
        return el;
    }

    private static XElement Retributivi(DatiRetributiviUniEmens r)
    {
        var el = new XElement("DatiRetributivi");
        AggiungiSePresente(el, "TipoLavoratore", r.TipoLavoratore);
        el.Add(new XElement("Imponibile", EuroInteri(r.Imponibile)));
        el.Add(new XElement("Contributo", EuroConDecimali(r.Contributo)));
        if (r.RetribTeorica is { } teorica)
            el.Add(new XElement("RetribTeorica", EuroInteri(teorica)));
        AggiungiSePresente(el, "OreLavorabili", r.OreLavorabili);

        foreach (var settimana in r.Settimane)
        {
            var elementoSettimana = new XElement("Settimana",
                new XElement("IdSettimana", Intero(settimana.Id)),
                new XElement("TipoCopertura", settimana.TipoCopertura));
            foreach (string codiceEvento in settimana.CodiciEvento)
                elementoSettimana.Add(new XElement("CodiceEvento", codiceEvento));
            el.Add(elementoSettimana);
        }

        foreach (var giorno in r.Giorni)
        {
            var elementoGiorno = new XElement("Giorno",
                new XAttribute("GG", giorno.Giorno),
                new XElement("Lavorato", giorno.Lavorato ? "S" : "N"));
            AggiungiSePresente(elementoGiorno, "TipoCoperturaGiorn", giorno.TipoCopertura);
            foreach (var evento in giorno.Eventi)
                elementoGiorno.Add(EventoGiornaliero(evento));
            el.Add(elementoGiorno);
        }

        foreach (var differenza in r.DifferenzeAccredito)
            el.Add(DifferenzaAccredito(differenza));

        el.Add(new XElement("GiorniRetribuiti", Intero(r.GiorniRetribuiti)));
        // Lo schema qui offre una scelta: giorni contribuiti oppure ore contribuite.
        // Le ore valgono per part-time e intermittenti, i giorni per tutti gli altri.
        if (r.OreContribuite is { } ore)
            el.Add(new XElement("OreContribuite", Intero(ore)));
        else
            AggiungiSePresente(el, "GiorniContribuiti", r.GiorniContribuiti);
        AggiungiSePresente(el, "RispettoMinimale", r.RispettoMinimale);
        AggiungiSePresente(el, "SettimaneUtili", r.SettimaneUtili);
        return el;
    }

    private static XElement DifferenzaAccredito(DifferenzaAccreditoUniEmens differenza)
    {
        var el = new XElement("DifferenzeAccredito",
            new XElement("CodiceEvento", differenza.CodiceEvento.Trim()),
            new XElement("DiffAccredito", EuroInteri(differenza.Importo)));
        foreach (var quota in differenza.QuoteEvento)
        {
            el.Add(new XElement("InfoEvento",
                new XElement("MotivoEvento",
                    new XAttribute("TipoMotivoEvento", quota.TipoMotivoEvento.Trim()),
                    quota.MotivoEvento.Trim()),
                new XElement("DiffAccreditoEvento", EuroInteri(quota.Importo))));
        }
        return el;
    }

    private static XElement EventoGiornaliero(EventoGiornalieroUniEmens evento)
    {
        var el = new XElement("EventoGiorn",
            new XElement("CodiceEventoGiorn", evento.CodiceEvento));
        AggiungiSePresente(el, "NumOreEvento", evento.NumeroOreEvento);
        if (!string.IsNullOrWhiteSpace(evento.InformazioneAggiuntiva))
            el.Add(new XElement("InfoAggEvento",
                new XAttribute(
                    "TipoInfoAggEvento", evento.TipoInformazioneAggiuntiva!),
                evento.InformazioneAggiuntiva));
        return el;
    }

    private static XElement Aziendale(DenunciaAziendaleUniEmens a) =>
        new("DenunciaAziendale",
            new XElement("TrattQuotaLav", a.TrattQuotaLav),
            new XElement("NumLavoratori", Intero(a.NumLavoratori)),
            new XElement("ForzaAziendale", Intero(a.ForzaAziendale)),
            new XElement("DatiQuadraturaRetrContr",
                new XElement("NumDenIndiv", Intero(a.NumDenIndiv)),
                new XElement("TotaleADebito", EuroInteri(a.TotaleADebito)),
                new XElement("TotaleACredito", EuroInteri(a.TotaleACredito))));

    private static void AggiungiSePresente(XElement padre, string nome, string? valore)
    {
        if (!string.IsNullOrWhiteSpace(valore)) padre.Add(new XElement(nome, valore));
    }

    private static void AggiungiSePresente(XElement padre, string nome, int? valore)
    {
        if (valore is { } v) padre.Add(new XElement(nome, Intero(v)));
    }
}
