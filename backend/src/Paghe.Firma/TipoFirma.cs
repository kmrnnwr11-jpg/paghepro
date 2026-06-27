namespace Paghe.Firma;

/// <summary>Formato della firma digitale prodotta.</summary>
public enum TipoFirma
{
    /// <summary>CAdES-BES: busta CMS/PKCS#7 con estensione <c>.p7m</c> (richiesto da SdI per FatturaPA).</summary>
    Cades,

    /// <summary>XAdES-BES: firma XML "enveloped" dentro il documento stesso (alternativa per FatturaPA).</summary>
    Xades,
}
