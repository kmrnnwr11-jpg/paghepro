namespace Paghe.Core.Scadenzario;

public enum TipoRipetizione
{
    Variabile,
    Mensile16,
    UltimoGiornoMese,
    Trimestrale,       // 16/5, 16/8, 16/11, 16/3 anno+1
    Annuale16Feb,
    Annuale28Feb,
    Annuale16Mar,
    Annuale30Apr,
    Annuale16Mag,
    Annuale30Giu,
    Annuale16Giu,
    Annuale31Lug,
    Annuale31Ott,
    Annuale15Ott,
    Annuale30Nov,
    Annuale16Dic,
}

public sealed record TipoScadenza(
    string Codice,
    string Descrizione,
    TipoRipetizione Ripetizione,
    int GiorniPreavviso,
    string? Nota = null);

/// <summary>
/// Catalogo fisso di tutti i tipi di scadenza fiscale/contributiva che possono
/// essere generati automaticamente come promemoria per ogni azienda cliente.
/// </summary>
public static class ScadenzeCalendario
{
    private static readonly List<TipoScadenza> _tutti =
    [
        // --- Mensili ---
        new("F24Irpef",       "F24 ritenute IRPEF",              TipoRipetizione.Mensile16,     5, "Solo aziende con dipendenti"),
        new("F24Inps",        "F24 contributi INPS dipendenti",  TipoRipetizione.Mensile16,     5, "Solo aziende con dipendenti"),
        new("IvaMensile",     "Liquidazione IVA mensile",        TipoRipetizione.Mensile16,     5, "Solo contribuenti mensili IVA"),
        new("UniEmens",       "UniEmens INPS",                   TipoRipetizione.UltimoGiornoMese, 7, "Solo aziende con dipendenti"),

        // --- Trimestrali ---
        new("IvaTrimestrale", "Liquidazione IVA trimestrale",    TipoRipetizione.Trimestrale,  10, "Solo contribuenti trimestrali IVA"),
        new("Lipe",           "LIPE – Liquidazione Periodica IVA", TipoRipetizione.Trimestrale, 10, "Tutti i soggetti IVA"),
        new("InpsArtigiani",  "Contributi INPS fissi artigiani/commercianti", TipoRipetizione.Trimestrale, 10, "Artigiani e commercianti"),

        // --- Annuali ---
        new("InailSaldo",     "Autoliquidazione INAIL – saldo",  TipoRipetizione.Annuale16Feb, 20, "Tutti i datori di lavoro"),
        new("CuLavoratori",   "Certificazione Unica ai lavoratori", TipoRipetizione.Annuale28Feb, 15, "Datori di lavoro"),
        new("CuAde",          "CU telematica all'AdE",            TipoRipetizione.Annuale16Mar, 15, "Datori di lavoro"),
        new("DichIva",        "Dichiarazione IVA annuale",        TipoRipetizione.Annuale30Apr, 20, "Tutti i soggetti IVA"),
        new("InailAcconto",   "Autoliquidazione INAIL – acconto", TipoRipetizione.Annuale16Mag, 15, "Tutti i datori di lavoro"),
        new("SaldoIrpef",     "Saldo IRPEF + 1° acconto",        TipoRipetizione.Annuale30Giu, 20, "Tutti i titolari P.IVA"),
        new("ImuAcconto",     "IMU acconto",                     TipoRipetizione.Annuale16Giu, 15, "Chi possiede immobili"),
        new("Modello770",     "Modello 770",                     TipoRipetizione.Annuale31Ott, 20, "Datori di lavoro"),
        new("ModelliRedditi", "Modello Redditi",                 TipoRipetizione.Annuale15Ott, 20, "Tutti i titolari P.IVA"),
        new("Acconto2Irpef",  "2° acconto IRPEF",                TipoRipetizione.Annuale30Nov, 15, "Tutti i titolari P.IVA"),
        new("Acconto2Inps",   "2° acconto INPS artigiani/commercianti", TipoRipetizione.Annuale30Nov, 15, "Artigiani e commercianti"),
        new("ImuSaldo",       "IMU saldo",                       TipoRipetizione.Annuale16Dic, 15, "Chi possiede immobili"),
    ];

    public static IReadOnlyList<TipoScadenza> Tutti => _tutti;

    /// <summary>
    /// Calcola le date di scadenza effettive per un anno dato il tipo di ripetizione.
    /// Per TipoRipetizione.Variabile restituisce una sequenza vuota (la scadenza dipende dal documento).
    /// </summary>
    public static IEnumerable<DateOnly> CalcolaDate(TipoRipetizione tipo, int anno)
    {
        return tipo switch
        {
            TipoRipetizione.Mensile16 =>
                Enumerable.Range(1, 12).Select(m => new DateOnly(anno, m, 16)),
            TipoRipetizione.UltimoGiornoMese =>
                Enumerable.Range(1, 12).Select(m => new DateOnly(anno, m, DateTime.DaysInMonth(anno, m))),
            TipoRipetizione.Trimestrale =>
                new[] { new DateOnly(anno, 5, 16), new DateOnly(anno, 8, 16),
                        new DateOnly(anno, 11, 16), new DateOnly(anno + 1, 3, 16) },
            TipoRipetizione.Annuale16Feb  => [new DateOnly(anno, 2, 16)],
            TipoRipetizione.Annuale28Feb  => [new DateOnly(anno, 2, 28)],
            TipoRipetizione.Annuale16Mar  => [new DateOnly(anno, 3, 16)],
            TipoRipetizione.Annuale30Apr  => [new DateOnly(anno, 4, 30)],
            TipoRipetizione.Annuale16Mag  => [new DateOnly(anno, 5, 16)],
            TipoRipetizione.Annuale30Giu  => [new DateOnly(anno, 6, 30)],
            TipoRipetizione.Annuale16Giu  => [new DateOnly(anno, 6, 16)],
            TipoRipetizione.Annuale31Lug  => [new DateOnly(anno, 7, 31)],
            TipoRipetizione.Annuale31Ott  => [new DateOnly(anno, 10, 31)],
            TipoRipetizione.Annuale15Ott  => [new DateOnly(anno, 10, 15)],
            TipoRipetizione.Annuale30Nov  => [new DateOnly(anno, 11, 30)],
            TipoRipetizione.Annuale16Dic  => [new DateOnly(anno, 12, 16)],
            _                             => Array.Empty<DateOnly>(),
        };
    }

    /// <summary>Indica se un tipo di scadenza si applica a questa azienda in base al suo profilo.</summary>
    public static bool Applicabile(TipoScadenza tipo, bool haDipendenti, string? tipoContribuente, bool assoggettataIva)
    {
        return tipo.Codice switch
        {
            "F24Irpef" or "F24Inps" or "UniEmens" or "CuLavoratori" or "CuAde"
                or "InailSaldo" or "InailAcconto" or "Modello770" => haDipendenti,
            "IvaMensile" or "IvaTrimestrale" or "Lipe" or "DichIva" => assoggettataIva,
            "InpsArtigiani" or "Acconto2Inps" =>
                tipoContribuente is "Artigiano" or "Commerciante",
            _ => true, // SaldoIrpef, Acconto2Irpef, ModelliRedditi, Imu*
        };
    }
}
