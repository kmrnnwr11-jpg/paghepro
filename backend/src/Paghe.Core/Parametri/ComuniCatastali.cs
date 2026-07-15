namespace Paghe.Core.Parametri;

/// <summary>
/// Anagrafica minima dei comuni italiani (codice catastale → nome e provincia), letta una
/// volta sola dal CSV MEF dell'addizionale comunale più recente incorporato come risorsa.
/// Serve ai documenti che devono stampare il nome del comune partendo dal codice (es.
/// Certificazione Unica). I comuni cessati non sono nel file: in quel caso restituisce null.
/// </summary>
public static class ComuniCatastali
{
    public sealed record Comune(string Nome, string Provincia);

    private static readonly Lazy<IReadOnlyDictionary<string, Comune>> Mappa = new(Carica);

    public static Comune? Trova(string? codiceCatastale) =>
        !string.IsNullOrWhiteSpace(codiceCatastale)
        && Mappa.Value.TryGetValue(codiceCatastale.Trim().ToUpperInvariant(), out var comune)
            ? comune
            : null;

    private static IReadOnlyDictionary<string, Comune> Carica()
    {
        var mappa = new Dictionary<string, Comune>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(ComuniCatastali).Assembly;
        var risorsa = asm.GetManifestResourceNames()
            .Where(n => n.Contains("mef_addizionale_comunale_", StringComparison.OrdinalIgnoreCase))
            .OrderDescending()   // il file dell'anno più recente
            .FirstOrDefault();
        if (risorsa is null) return mappa;

        using var reader = new StreamReader(asm.GetManifestResourceStream(risorsa)!);
        reader.ReadLine(); // intestazione: CODICE_CATASTALE;COMUNE;PR;...
        string? riga;
        while ((riga = reader.ReadLine()) is not null)
        {
            var campi = riga.Split(';');
            if (campi.Length < 3 || campi[0].Trim().Length == 0) continue;
            mappa[campi[0].Trim().ToUpperInvariant()] = new Comune(campi[1].Trim(), campi[2].Trim());
        }
        return mappa;
    }
}
