namespace Paghe.Api;

/// <summary>
/// Limiti e riconoscimento minimo degli upload usati dagli endpoint di firma.
/// Il Content-Type arriva dal client e non costituisce una prova del formato reale.
/// </summary>
public static class SicurezzaUploadFirma
{
    public const long DimensioneMassimaDocumento = 10 * 1024 * 1024;
    public const long DimensioneMassimaPfx = 1024 * 1024;
    public const long DimensioneMassimaDocumentoFirmato = 15 * 1024 * 1024;
    public const long DimensioneMassimaRichiesta = DimensioneMassimaDocumentoFirmato + DimensioneMassimaPfx + 64 * 1024;

    public static string? ValidaDimensione(IFormFile? file, long massimo, string descrizione)
    {
        if (file is null || file.Length == 0)
            return $"{descrizione} mancante.";
        if (file.Length > massimo)
            return $"{descrizione} troppo grande: massimo {FormatoMegabyte(massimo)} MB.";
        return null;
    }

    public static bool SembraPkcs12(ReadOnlySpan<byte> dati)
    {
        // PFX/PKCS#12 usa una struttura ASN.1 DER, che inizia con SEQUENCE (0x30).
        // Il caricatore crittografico esegue poi la validazione completa del contenuto.
        return dati.Length >= 4 && dati[0] == 0x30;
    }

    public static bool SembraDocumentoFirmato(ReadOnlySpan<byte> dati)
    {
        int i = 0;
        if (dati.Length >= 3 && dati[0] == 0xEF && dati[1] == 0xBB && dati[2] == 0xBF)
            i = 3;
        while (i < dati.Length && dati[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            i++;

        // CAdES è ASN.1 DER; XAdES è XML.
        return i < dati.Length && dati[i] is 0x30 or (byte)'<';
    }

    private static long FormatoMegabyte(long byteTotali) => byteTotali / (1024 * 1024);
}
