namespace Paghe.Firma;

/// <summary>Esito di una firma digitale CAdES.</summary>
/// <param name="ContenutoFirmato">Busta CAdES-BES (<c>.p7m</c>) con il contenuto incapsulato.</param>
/// <param name="NomeFile">Nome suggerito per il file firmato (con estensione <c>.p7m</c>).</param>
/// <param name="Firmatario">Nome comune (CN) del titolare del certificato di firma.</param>
/// <param name="DataFirmaUtc">Istante della firma in UTC.</param>
/// <param name="AlgoritmoDigest">Algoritmo di digest usato (es. <c>SHA-256</c>).</param>
public sealed record RisultatoFirma(
    byte[] ContenutoFirmato,
    string NomeFile,
    string Firmatario,
    DateTime DataFirmaUtc,
    string AlgoritmoDigest);
