using System.Globalization;
using System.Xml.Linq;

namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Genera l'XML del flusso UniEmens a partire dal modello. La struttura segue lo schema
/// pubblico UniEmens (radice &lt;UniEmens&gt;, &lt;DenunciaAziendale&gt;, una
/// &lt;DenunciaIndividuale&gt; per lavoratore), ma è una <b>bozza da validare</b> con il
/// software di controllo ufficiale INPS prima di qualunque invio.
/// </summary>
public static class UniEmensXmlWriter
{
    /// <summary>Importo in euro con due decimali e punto come separatore (formato neutro).</summary>
    private static string Euro(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    public static XDocument Genera(FlussoUniEmens flusso)
    {
        ArgumentNullException.ThrowIfNull(flusso);

        var radice = new XElement("UniEmens",
            new XAttribute("versione", "bozza-non-validata"),
            new XComment(
                " BOZZA: struttura allineata allo schema pubblico UniEmens, NON validata con il " +
                "software di controllo ufficiale INPS. Non trasmettere senza validazione. "),
            new XElement("DatiMittente",
                new XElement("CodiceFiscale", flusso.Mittente.CodiceFiscale),
                new XElement("Denominazione", flusso.Mittente.Denominazione)),
            DenunciaAziendale(flusso.Azienda),
            new XElement("DenunceIndividuali",
                flusso.Lavoratori.Select(DenunciaIndividuale)));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), radice);
    }

    public static string GeneraStringa(FlussoUniEmens flusso) => Genera(flusso).ToString();

    private static XElement DenunciaAziendale(DenunciaAziendaleUniEmens a) =>
        new("DenunciaAziendale",
            new XElement("Matricola", a.MatricolaInps),
            new XElement("CodiceFiscaleAzienda", a.CodiceFiscaleAzienda),
            new XElement("AnnoMese", $"{a.Anno:0000}-{a.Mese:00}"));

    private static XElement DenunciaIndividuale(DenunciaIndividualeUniEmens d)
    {
        var el = new XElement("DenunciaIndividuale",
            new XElement("CodiceFiscale", d.CodiceFiscale),
            new XElement("Cognome", d.Cognome),
            new XElement("Nome", d.Nome),
            new XElement("Qualifica1", d.Qualifica));

        if (!string.IsNullOrWhiteSpace(d.TipoLavoratore))
            el.Add(new XElement("TipoLavoratore", d.TipoLavoratore));

        el.Add(new XElement("DatiRetributivi",
            new XElement("Imponibile", Euro(d.Imponibile)),
            new XElement("Contributo", Euro(d.Contributo)),
            new XElement("GiorniRetribuiti", d.GiorniRetribuiti),
            new XElement("SettimaneUtili", d.SettimaneUtili)));

        return el;
    }
}
