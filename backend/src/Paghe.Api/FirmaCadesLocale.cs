using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Firma;

namespace Paghe.Api;

/// <summary>
/// Opzioni del certificato di firma digitale (sezione di configurazione <c>"Firma"</c>).
/// Si può configurare un certificato software PFX/P12 (firma lato server / firma remota
/// esportabile) oppure una smart card / CNS via middleware PKCS#11.
/// </summary>
public sealed class FirmaOptions
{
    /// <summary>Percorso di un certificato PFX/P12 per la firma lato server.</summary>
    public string? PfxPath { get; set; }

    /// <summary>Password del PFX/P12.</summary>
    public string? PfxPassword { get; set; }

    /// <summary>Configurazione per la firma con smart card / CNS via PKCS#11.</summary>
    public CnsOptions Cns { get; set; } = new();

    public bool PfxConfigurato => !string.IsNullOrWhiteSpace(PfxPath);

    public bool CnsConfigurato => !string.IsNullOrWhiteSpace(Cns.Pkcs11LibraryPath);
}

/// <summary>Configurazione della firma con Carta Nazionale dei Servizi / smart card (PKCS#11).</summary>
public sealed class CnsOptions
{
    /// <summary>Percorso del modulo PKCS#11 del middleware della carta (es. <c>libbit4xpki.dylib</c>).</summary>
    public string? Pkcs11LibraryPath { get; set; }

    /// <summary>
    /// PIN di firma. Sconsigliato in configurazione persistente: preferire l'invio per singola
    /// richiesta (header/campo). Presente per scenari desktop/agente locale fidato.
    /// </summary>
    public string? Pin { get; set; }

    /// <summary>Etichetta del certificato di sottoscrizione da usare sulla carta (opzionale).</summary>
    public string? CertificatoLabel { get; set; }
}

/// <summary>
/// Firma CAdES-BES locale reale (busta <c>.xml.p7m</c>) per la fattura elettronica, basata sul
/// modulo <see cref="Paghe.Firma"/>. Sostituisce il segnaposto <c>FirmaCadesNonConfigurata</c>:
/// firma lato server con un certificato PFX/firma remota, oppure con smart card/CNS dove il
/// middleware PKCS#11 è disponibile sulla macchina che esegue la firma.
/// </summary>
public sealed class FirmaCadesLocale(FirmaOptions opzioni) : IFirmaFatturaElettronica
{
    private readonly FirmaOptions _opzioni = opzioni;

    public bool FirmaLocale => true;

    public Task<FatturaFirmata> FirmaAsync(byte[] xml, string nomeFileXml, CancellationToken ct = default)
    {
        var firmatore = CreaFirmatore(_opzioni);
        try
        {
            var risultato = firmatore.Firma(xml, nomeFileXml);
            return Task.FromResult(
                new FatturaFirmata(risultato.ContenutoFirmato, risultato.NomeFile, Firmato: true));
        }
        finally
        {
            (firmatore as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Costruisce il firmatario in base alla configurazione. <paramref name="pinRichiesta"/>
    /// (se presente) ha precedenza sul PIN eventualmente in configurazione, così il PIN della CNS
    /// può essere fornito al momento della firma e non conservato.
    /// </summary>
    public static IFirmatoreDigitale CreaFirmatore(FirmaOptions opzioni, string? pinRichiesta = null)
    {
        if (opzioni.PfxConfigurato)
            return FirmatorePfx.DaFile(opzioni.PfxPath!, opzioni.PfxPassword ?? "");

        string? pin = string.IsNullOrWhiteSpace(pinRichiesta) ? opzioni.Cns.Pin : pinRichiesta;
        if (opzioni.CnsConfigurato && !string.IsNullOrWhiteSpace(pin))
            return new FirmatoreCns(opzioni.Cns.Pkcs11LibraryPath!, pin!, opzioni.Cns.CertificatoLabel);

        throw new NotSupportedException(
            "Firma digitale CAdES non configurata: imposta la sezione \"Firma\" " +
            "(PfxPath/PfxPassword per certificato software, oppure Cns:Pkcs11LibraryPath + PIN per smart card/CNS).");
    }
}
