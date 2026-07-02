using System.Collections.Concurrent;

namespace Paghe.Core.Parametri;

/// <summary>
/// Carica i regimi delle addizionali IRPEF (comunale + regionale) dai file ufficiali MEF
/// incorporati come risorse, per anno. Il risultato è messo in cache (i file hanno migliaia
/// di comuni: si leggono e si interpretano una volta sola per anno).
/// </summary>
public static class CaricaRegimiMef
{
    private static readonly ConcurrentDictionary<int, RegimiAnno> Cache = new();

    /// <summary>Regimi addizionale di un anno: per regione e per codice catastale del comune.</summary>
    public sealed record RegimiAnno(
        IReadOnlyDictionary<string, RegimeAddizionale> Regionali,
        IReadOnlyDictionary<string, RegimeAddizionale> Comunali)
    {
        public static readonly RegimiAnno Vuoto = new(
            new Dictionary<string, RegimeAddizionale>(), new Dictionary<string, RegimeAddizionale>());
    }

    /// <summary>Regimi per l'anno indicato; se non ci sono i dati MEF incorporati, tutto vuoto.</summary>
    public static RegimiAnno PerAnno(int anno) => Cache.GetOrAdd(anno, Carica);

    private static RegimiAnno Carica(int anno)
    {
        var comunali = LeggiRisorsa($"mef_addizionale_comunale_{anno}.csv", ImportAddizionaliMef.LeggiComunali);
        var regionali = LeggiRisorsa($"mef_addizionale_regionale_{anno}.csv", ImportAddizionaliMef.LeggiRegionali);
        return new RegimiAnno(regionali, comunali);
    }

    private static IReadOnlyDictionary<string, RegimeAddizionale> LeggiRisorsa(
        string nomeFile,
        Func<IEnumerable<string>, IReadOnlyDictionary<string, RegimeAddizionale>> interpreta)
    {
        var asm = typeof(CaricaRegimiMef).Assembly;
        var risorsa = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(nomeFile, StringComparison.OrdinalIgnoreCase));
        if (risorsa is null)
            return new Dictionary<string, RegimeAddizionale>();

        using var stream = asm.GetManifestResourceStream(risorsa)!;
        using var reader = new StreamReader(stream);
        return interpreta(LeggiRighe(reader));
    }

    private static IEnumerable<string> LeggiRighe(StreamReader reader)
    {
        string? riga;
        while ((riga = reader.ReadLine()) is not null)
            yield return riga;
    }
}
