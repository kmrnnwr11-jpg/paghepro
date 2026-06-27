using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Paghe.Firma;

/// <summary>
/// Firma XAdES-BES "enveloped": la firma XML-DSig viene inserita dentro il documento stesso,
/// con le proprietà qualificanti XAdES (orario di firma e riferimento al certificato).
/// Digest e firma in SHA-256. Alternativa a CAdES accettata dallo SdI per la fattura elettronica.
/// Usa solo <see cref="SignedXml"/> del runtime .NET: nessuna dipendenza GPL.
/// </summary>
public static class FirmaXades
{
    private const string NsXades = "http://uri.etsi.org/01903/v1.3.2#";
    private static readonly string NsDs = SignedXml.XmlDsigNamespaceUrl;

    public static byte[] Firma(byte[] xml, X509Certificate2 certificato, AsymmetricAlgorithm? chiavePrivata = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(certificato);

        var doc = new XmlDocument { PreserveWhitespace = true };
        using (var ms = new MemoryStream(xml))
            doc.Load(ms);

        var rsa = (chiavePrivata as RSA) ?? certificato.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Chiave privata RSA non disponibile per la firma XAdES.");

        var signedXml = new SignedXmlXades(doc) { SigningKey = rsa };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        string idFirma = "Signature-" + Guid.NewGuid().ToString("N");
        string idProps = "SignedProperties-" + Guid.NewGuid().ToString("N");
        signedXml.Signature.Id = idFirma;

        // Riferimento al documento (firma "enveloped").
        var refDoc = new Reference { Uri = "", DigestMethod = SignedXml.XmlDsigSHA256Url };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(refDoc);

        // Proprietà qualificanti XAdES-BES come <ds:Object>.
        XmlElement qualifying = CostruisciQualifyingProperties(doc, idFirma, idProps, certificato);
        var dataObject = new DataObject { Data = qualifying.SelectNodes(".")! };
        signedXml.AddObject(dataObject);

        // Riferimento alle SignedProperties (richiesto da XAdES).
        var refProps = new Reference
        {
            Uri = "#" + idProps,
            Type = "http://uri.etsi.org/01903#SignedProperties",
            DigestMethod = SignedXml.XmlDsigSHA256Url,
        };
        refProps.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(refProps);

        // Certificato del firmatario nel KeyInfo (necessario per la verifica).
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificato));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();

        XmlElement firma = signedXml.GetXml();
        doc.DocumentElement!.AppendChild(doc.ImportNode(firma, true));

        using var outMs = new MemoryStream();
        using (var writer = XmlWriter.Create(outMs, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) }))
            doc.Save(writer);
        return outMs.ToArray();
    }

    /// <summary>Verifica una firma XAdES/XML-DSig enveloped e ne estrae i dettagli.</summary>
    public static RisultatoVerifica VerificaConDettagli(byte[] xmlFirmato)
    {
        try
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(xmlFirmato))
                doc.Load(ms);

            var nsm = new XmlNamespaceManager(doc.NameTable);
            nsm.AddNamespace("ds", NsDs);
            nsm.AddNamespace("xades", NsXades);

            if (doc.SelectSingleNode("//ds:Signature", nsm) is not XmlElement nodoFirma)
                return new RisultatoVerifica(false, "XAdES");

            var signedXml = new SignedXmlXades(doc);
            signedXml.LoadXml(nodoFirma);
            bool valido = signedXml.CheckSignature();

            return new RisultatoVerifica(
                valido, "XAdES",
                EstraiFirmatario(signedXml),
                EstraiSigningTime(doc, nsm),
                "SHA-256");
        }
        catch (Exception ex) when (ex is CryptographicException or XmlException)
        {
            return new RisultatoVerifica(false, "XAdES");
        }
    }

    private static XmlElement CostruisciQualifyingProperties(
        XmlDocument doc, string idFirma, string idProps, X509Certificate2 cert)
    {
        XmlElement qp = doc.CreateElement("xades", "QualifyingProperties", NsXades);
        qp.SetAttribute("Target", "#" + idFirma);

        XmlElement sp = doc.CreateElement("xades", "SignedProperties", NsXades);
        sp.SetAttribute("Id", idProps);

        XmlElement ssp = doc.CreateElement("xades", "SignedSignatureProperties", NsXades);

        XmlElement signingTime = doc.CreateElement("xades", "SigningTime", NsXades);
        signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        ssp.AppendChild(signingTime);

        XmlElement signingCert = doc.CreateElement("xades", "SigningCertificateV2", NsXades);
        XmlElement certEl = doc.CreateElement("xades", "Cert", NsXades);
        XmlElement certDigest = doc.CreateElement("xades", "CertDigest", NsXades);

        XmlElement digestMethod = doc.CreateElement("ds", "DigestMethod", NsDs);
        digestMethod.SetAttribute("Algorithm", SignedXml.XmlDsigSHA256Url);
        XmlElement digestValue = doc.CreateElement("ds", "DigestValue", NsDs);
        digestValue.InnerText = Convert.ToBase64String(SHA256.HashData(cert.RawData));

        certDigest.AppendChild(digestMethod);
        certDigest.AppendChild(digestValue);
        certEl.AppendChild(certDigest);
        signingCert.AppendChild(certEl);
        ssp.AppendChild(signingCert);

        sp.AppendChild(ssp);
        qp.AppendChild(sp);
        return qp;
    }

    private static string? EstraiFirmatario(SignedXml signedXml)
    {
        if (signedXml.KeyInfo is null) return null;
        foreach (var clause in signedXml.KeyInfo)
            if (clause is KeyInfoX509Data x && x.Certificates is { Count: > 0 } certs
                && certs[0] is X509Certificate2 cert)
                return cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        return null;
    }

    private static DateTime? EstraiSigningTime(XmlDocument doc, XmlNamespaceManager nsm)
    {
        if (doc.SelectSingleNode("//xades:SigningTime", nsm) is XmlNode n
            && DateTime.TryParse(n.InnerText, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t))
            return t;
        return null;
    }

    /// <summary>
    /// <see cref="SignedXml"/> che sa risolvere anche i riferimenti interni alle proprietà
    /// firmate XAdES (le <c>SignedProperties</c> stanno dentro un <c>&lt;ds:Object&gt;</c> della firma,
    /// e il <see cref="SignedXml.GetIdElement"/> standard non le cerca lì).
    /// </summary>
    private sealed class SignedXmlXades(XmlDocument document) : SignedXml(document)
    {
        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            var elemento = base.GetIdElement(document, idValue);
            if (elemento is not null) return elemento;

            foreach (DataObject oggetto in Signature.ObjectList)
                if (oggetto.GetXml().SelectSingleNode($"//*[@Id='{idValue}']") is XmlElement trovato)
                    return trovato;
            return null;
        }
    }
}
