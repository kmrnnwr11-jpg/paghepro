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

    /// <summary>Divisore per la paga giornaliera quando il CCNL non ne indica uno suo.</summary>
    public decimal DivisoreGiornaliero { get; init; } = 26m;

    public decimal MaggiorazioneStraordinario { get; init; } = 0.15m;
    public decimal MaggiorazioneStraordinarioNotturno { get; init; } = 0.50m;
    public decimal MaggiorazioneStraordinarioFestivo { get; init; } = 0.30m;

    /// <summary>Giorni di ferie maturati al mese quando il CCNL non ha una regola sua.</summary>
    public decimal GiorniFerieMese { get; init; } = 2.1667m;

    /// <summary>Ore di permesso ROL maturate al mese quando il CCNL non ha una regola sua.</summary>
    public decimal OrePermessiMese { get; init; } = 6.00m;

    /// <summary>
    /// Regole per CCNL, per codice contratto. Quando un contratto non c'è si usano i valori
    /// generali qui sopra: meglio un default dichiarato che un numero nascosto nel motore.
    /// </summary>
    public IReadOnlyDictionary<string, RegoleCcnl> RegoleCcnl { get; init; }
        = new Dictionary<string, RegoleCcnl>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Divisore per la quota annua di TFR (13,5).</summary>
    public decimal TfrDivisore { get; init; } = 13.5m;

    /// <summary>Contributo al fondo TFR a carico del lavoratore (0,50%).</summary>
    public decimal TfrContributoFondo { get; init; } = 0.005m;

    /// <summary>Variazione ISTAT per la rivalutazione TFR dell'anno (es. 0,01 = +1%). Null se non inserita.</summary>
    public decimal? TfrVariazioneIstat { get; init; }

    // ---- Limiti degli elementi retributivi agevolati (valori 2026) -------------------
    // Sono soglie di legge, quindi cambiano per anno come tutto il resto di questa classe.
    // ATTENZIONE: l'esenzione presuppone che l'elemento sia realmente spettante. Una
    // trasferta mai avvenuta non e' un'agevolazione: e' retribuzione mascherata.

    /// <summary>Indennità di trasferta in Italia esente per giorno (art. 51 c.5 TUIR): 46,48 €.</summary>
    public decimal TrasfertaItaliaEsenteGiorno { get; init; } = 46.48m;

    /// <summary>Indennità di trasferta all'estero esente per giorno: 77,47 €.</summary>
    public decimal TrasfertaEsteroEsenteGiorno { get; init; } = 77.47m;

    /// <summary>Soglia annua di esenzione dei fringe benefit senza figli a carico: 1.000 €.</summary>
    public decimal FringeBenefitSogliaAnnua { get; init; } = 1000m;

    /// <summary>Soglia annua di esenzione dei fringe benefit con figli a carico: 2.000 €.</summary>
    public decimal FringeBenefitSogliaAnnuaConFigli { get; init; } = 2000m;

    /// <summary>Buono pasto elettronico esente per giorno: 10 € dal 2026 (era 8 €).</summary>
    public decimal BuonoPastoElettronicoEsenteGiorno { get; init; } = 10m;

    /// <summary>Buono pasto cartaceo esente per giorno: 4 €.</summary>
    public decimal BuonoPastoCartaceoEsenteGiorno { get; init; } = 4m;

    /// <summary>Premio di risultato agevolabile nell'anno: 5.000 € per il biennio 2026-2027.</summary>
    public decimal PremioRisultatoLimiteAnnuo { get; init; } = 5000m;

    /// <summary>Imposta sostitutiva sul premio di risultato: 1% per il biennio 2026-2027.</summary>
    public decimal PremioRisultatoAliquota { get; init; } = 0.01m;

    /// <summary>Reddito dell'anno precedente oltre il quale il premio non è agevolabile: 80.000 €.</summary>
    public decimal PremioRisultatoRedditoMassimo { get; init; } = 80000m;

    public AliquotaInps AliquotaInps(Qualifica qualifica) =>
        AliquoteInps.TryGetValue(qualifica, out var a)
            ? a
            : throw new ArgumentException($"Aliquota INPS non definita per la qualifica {qualifica}.");

    /// <summary>
    /// Regole del contratto indicato; se non ce ne sono, quelle generali dell'anno. Stesso
    /// schema dei regimi addizionali: valore specifico se c'è, altrimenti il default.
    /// </summary>
    public RegoleCcnl Regole(string? codiceCcnl) =>
        codiceCcnl is not null && RegoleCcnl.TryGetValue(codiceCcnl, out var r)
            ? r
            : new RegoleCcnl(
                DivisoreOrario, DivisoreGiornaliero,
                MaggiorazioneStraordinario, MaggiorazioneStraordinarioNotturno,
                MaggiorazioneStraordinarioFestivo,
                GiorniFerieMese, OrePermessiMese);

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
