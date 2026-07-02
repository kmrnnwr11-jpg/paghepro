using Paghe.Core.Domain;

namespace Paghe.Core.Parametri;

/// <summary>Uno scaglione IRPEF: aliquota applicata alla quota di reddito nell'intervallo.</summary>
public sealed record ScaglioneIrpef(decimal DaImporto, decimal? AImporto, decimal Aliquota);

/// <summary>Aliquota contributiva INPS, quota a carico dipendente e a carico datore.</summary>
public sealed record AliquotaInps(decimal Dipendente, decimal Datore);

/// <summary>
/// Insieme dei parametri normativi nazionali per un anno: aliquote contributive,
/// scaglioni IRPEF, addizionali, parametri TFR e divisori orari.
/// Versionati per anno: aggiornarli significa creare una nuova istanza, mai mutare
/// quella esistente, così i cedolini storici restano ricalcolabili.
/// </summary>
public sealed class ParametriNormativi
{
    public required int Anno { get; init; }

    public required IReadOnlyList<ScaglioneIrpef> ScaglioniIrpef { get; init; }

    /// <summary>Aliquote contributive INPS per qualifica.</summary>
    public required IReadOnlyDictionary<Qualifica, AliquotaInps> AliquoteInps { get; init; }

    /// <summary>Aliquota addizionale regionale IRPEF (versione MVP: aliquota piatta per regione).</summary>
    public required IReadOnlyDictionary<string, decimal> AddizionaleRegionale { get; init; }

    /// <summary>Aliquota addizionale comunale IRPEF per codice catastale del comune.</summary>
    public required IReadOnlyDictionary<string, decimal> AddizionaleComunale { get; init; }

    /// <summary>
    /// Regimi addizionale regionale a scaglioni + soglia di esenzione (dai dati ufficiali MEF),
    /// per nome regione. Se presente per una regione, prevale sull'aliquota piatta.
    /// </summary>
    public IReadOnlyDictionary<string, RegimeAddizionale> RegimiAddizionaleRegionale { get; init; }
        = new Dictionary<string, RegimeAddizionale>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Regimi addizionale comunale a scaglioni + esenzione (dati MEF), per codice catastale.</summary>
    public IReadOnlyDictionary<string, RegimeAddizionale> RegimiAddizionaleComunale { get; init; }
        = new Dictionary<string, RegimeAddizionale>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Divisore orario per il calcolo della paga oraria (es. 168).</summary>
    public decimal DivisoreOrario { get; init; } = 168m;

    public decimal MaggiorazioneStraordinario { get; init; } = 0.15m;
    public decimal MaggiorazioneStraordinarioNotturno { get; init; } = 0.50m;
    public decimal MaggiorazioneStraordinarioFestivo { get; init; } = 0.30m;

    /// <summary>Divisore per la quota annua di TFR (13,5).</summary>
    public decimal TfrDivisore { get; init; } = 13.5m;

    /// <summary>Contributo al fondo TFR a carico del lavoratore (0,50%).</summary>
    public decimal TfrContributoFondo { get; init; } = 0.005m;

    public AliquotaInps AliquotaInps(Qualifica qualifica) =>
        AliquoteInps.TryGetValue(qualifica, out var a)
            ? a
            : throw new ArgumentException($"Aliquota INPS non definita per la qualifica {qualifica}.");

    /// <summary>Regime addizionale regionale (scaglioni MEF se disponibili, altrimenti aliquota piatta).</summary>
    public RegimeAddizionale RegimeRegionale(string regione) =>
        Regime(RegimiAddizionaleRegionale, AddizionaleRegionale, regione);

    /// <summary>Regime addizionale comunale per codice catastale (scaglioni MEF se disponibili, altrimenti aliquota piatta).</summary>
    public RegimeAddizionale RegimeComunale(string codiceCatastale) =>
        Regime(RegimiAddizionaleComunale, AddizionaleComunale, codiceCatastale);

    private static RegimeAddizionale Regime(
        IReadOnlyDictionary<string, RegimeAddizionale> regimi,
        IReadOnlyDictionary<string, decimal> piatte,
        string chiave)
    {
        if (string.IsNullOrEmpty(chiave)) return RegimeAddizionale.Nessuno;
        if (regimi.TryGetValue(chiave, out var r)) return r;              // scaglioni MEF
        if (piatte.TryGetValue(chiave, out var a)) return RegimeAddizionale.Unica(a); // fallback piatto
        return RegimeAddizionale.Nessuno;
    }
}
