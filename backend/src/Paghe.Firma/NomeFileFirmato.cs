namespace Paghe.Firma;

/// <summary>Convenzioni di denominazione dei file firmati CAdES.</summary>
public static class NomeFileFirmato
{
    /// <summary>
    /// Aggiunge l'estensione <c>.p7m</c> al nome del file di origine, come previsto per i
    /// file firmati CAdES (es. <c>IT01234567890_00001.xml</c> → <c>IT01234567890_00001.xml.p7m</c>).
    /// </summary>
    public static string PerP7m(string? nomeFileOriginale) =>
        string.IsNullOrWhiteSpace(nomeFileOriginale)
            ? "documento.p7m"
            : nomeFileOriginale.Trim() + ".p7m";
}
