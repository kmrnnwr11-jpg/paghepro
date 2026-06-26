using System.Globalization;
using System.Xml.Linq;

namespace Paghe.Core.Fatturazione;

/// <summary>
/// Generatore del tracciato XML FatturaPA (versione FPR12 — fattura tra privati).
///
/// È un SOTTOINSIEME dello standard ufficiale dell'Agenzia delle Entrate, sufficiente
/// per i casi semplici (TD01). NON è validato contro l'XSD ufficiale né firmato: prima
/// dell'invio reale al Sistema di Interscambio (SdI) vanno aggiunti validazione XSD,
/// firma digitale CAdES/XAdES e i campi opzionali richiesti dal caso specifico.
/// Lo standard è pubblico: https://www.fatturapa.gov.it
/// </summary>
public static class FatturaPaXml
{
    private static readonly XNamespace P =
        "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    public static string Genera(Fattura fattura)
    {
        var totali = CalcoloFattura.Calcola(fattura);

        var root = new XElement(P + "FatturaElettronica",
            new XAttribute(XNamespace.Xmlns + "p", P.NamespaceName),
            new XAttribute("versione", "FPR12"),
            Header(fattura),
            Body(fattura, totali));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        return doc.Declaration + Environment.NewLine + root.ToString();
    }

    private static XElement Header(Fattura f) =>
        El("FatturaElettronicaHeader",
            El("DatiTrasmissione",
                El("IdTrasmittente",
                    El("IdPaese", f.Cedente.Nazione),
                    El("IdCodice", f.Cedente.PartitaIva ?? f.Cedente.CodiceFiscale ?? "")),
                El("ProgressivoInvio", f.Numero),
                El("FormatoTrasmissione", "FPR12"),
                El("CodiceDestinatario", f.CodiceDestinatario)),
            CedentePrestatore(f.Cedente),
            CessionarioCommittente(f.Cessionario));

    private static XElement CedentePrestatore(DatiAnagrafici a) =>
        El("CedentePrestatore",
            El("DatiAnagrafici",
                IdFiscaleIva(a),
                El("Anagrafica", El("Denominazione", a.Denominazione)),
                El("RegimeFiscale", a.RegimeFiscale)),
            Sede(a));

    private static XElement CessionarioCommittente(DatiAnagrafici a)
    {
        var datiAnagrafici = new XElement((XNamespace)"" + "DatiAnagrafici");
        if (!string.IsNullOrWhiteSpace(a.PartitaIva))
            datiAnagrafici.Add(IdFiscaleIva(a));
        if (!string.IsNullOrWhiteSpace(a.CodiceFiscale))
            datiAnagrafici.Add(El("CodiceFiscale", a.CodiceFiscale));
        datiAnagrafici.Add(El("Anagrafica", El("Denominazione", a.Denominazione)));

        return El("CessionarioCommittente", datiAnagrafici, Sede(a));
    }

    private static XElement IdFiscaleIva(DatiAnagrafici a) =>
        El("IdFiscaleIVA",
            El("IdPaese", a.Nazione),
            El("IdCodice", a.PartitaIva ?? ""));

    private static XElement Sede(DatiAnagrafici a) =>
        El("Sede",
            El("Indirizzo", a.Indirizzo),
            El("CAP", a.Cap),
            El("Comune", a.Comune),
            El("Provincia", a.Provincia),
            El("Nazione", a.Nazione));

    private static XElement Body(Fattura f, TotaliFattura totali)
    {
        var beniServizi = new XElement((XNamespace)"" + "DatiBeniServizi");

        int numeroLinea = 1;
        foreach (var r in f.Righe)
        {
            var linea = El("DettaglioLinee",
                El("NumeroLinea", numeroLinea.ToString(CultureInfo.InvariantCulture)),
                El("Descrizione", r.Descrizione),
                El("Quantita", Num(r.Quantita)),
                El("PrezzoUnitario", Num(r.PrezzoUnitario)),
                El("PrezzoTotale", Num(CalcoloFattura.ImponibileRiga(r))),
                El("AliquotaIVA", Num(r.AliquotaIva)));
            if (!string.IsNullOrWhiteSpace(r.NaturaIva))
                linea.Add(El("Natura", r.NaturaIva));
            beniServizi.Add(linea);
            numeroLinea++;
        }

        foreach (var rip in totali.Riepiloghi)
        {
            var riepilogo = El("DatiRiepilogo",
                El("AliquotaIVA", Num(rip.Aliquota)));
            if (!string.IsNullOrWhiteSpace(rip.NaturaIva))
                riepilogo.Add(El("Natura", rip.NaturaIva));
            riepilogo.Add(El("ImponibileImporto", Num(rip.Imponibile)));
            riepilogo.Add(El("Imposta", Num(rip.Imposta)));
            beniServizi.Add(riepilogo);
        }

        return El("FatturaElettronicaBody",
            El("DatiGenerali",
                El("DatiGeneraliDocumento",
                    El("TipoDocumento", f.TipoDocumento),
                    El("Divisa", f.Divisa),
                    El("Data", f.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    El("Numero", f.Numero),
                    El("ImportoTotaleDocumento", Num(totali.TotaleDocumento)))),
            beniServizi);
    }

    private static XElement El(string nome, params object[] contenuto) =>
        new((XNamespace)"" + nome, contenuto);

    private static string Num(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}
