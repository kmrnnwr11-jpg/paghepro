namespace Paghe.Firma;

/// <summary>Esito di una firma digitale.</summary>
/// <param name="ContenutoFirmato">Documento firmato (busta <c>.p7m</c> per CAdES, XML firmato per XAdES).</param>
/// <param name="NomeFile">Nome suggerito per il file firmato.</param>
/// <param name="Firmatario">Nome comune (CN) del titolare del certificato di firma.</param>
/// <param name="DataFirmaUtc">Istante della firma in UTC.</param>
/// <param name="AlgoritmoDigest">Algoritmo di digest usato (es. <c>SHA-256</c>).</param>
/// <param name="Formato">Formato prodotto: <c>CAdES</c> o <c>XAdES</c>.</param>
/// <param name="Mime">Content-type del file firmato.</param>
public sealed record RisultatoFirma(
    byte[] ContenutoFirmato,
    string NomeFile,
    string Firmatario,
    DateTime DataFirmaUtc,
    string AlgoritmoDigest,
    string Formato,
    string Mime);

/// <summary>Esito della verifica di un documento firmato.</summary>
/// <param name="Valido">Vero se la firma è crittograficamente valida e il contenuto integro.</param>
/// <param name="Formato">Formato rilevato: <c>CAdES</c>, <c>XAdES</c> o <c>Sconosciuto</c>.</param>
/// <param name="Firmatario">Nome comune (CN) del firmatario, se disponibile.</param>
/// <param name="DataFirmaUtc">Data di firma dichiarata nel documento, se presente.</param>
/// <param name="AlgoritmoDigest">Algoritmo di digest, se disponibile.</param>
public sealed record RisultatoVerifica(
    bool Valido,
    string Formato,
    string? Firmatario = null,
    DateTime? DataFirmaUtc = null,
    string? AlgoritmoDigest = null);
