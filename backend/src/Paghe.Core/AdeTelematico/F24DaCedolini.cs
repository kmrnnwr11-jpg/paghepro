using Paghe.Core.Calcolo;
using Paghe.Core.Domain;

namespace Paghe.Core.AdeTelematico;

/// <summary>Dati del sostituto d'imposta (datore di lavoro) che versa con F24.</summary>
public sealed record DatiSostitutoImposta(
    string CodiceFiscale,
    string Denominazione,
    string MatricolaInps,
    string CodiceSedeInps = "");

/// <summary>
/// Importi di un cedolino che confluiscono nell'F24, con il territorio di riferimento per
/// le addizionali (regione e codice catastale del comune del lavoratore).
/// </summary>
public sealed record RigaPagaPerF24(
    decimal RitenutaIrpef,
    decimal AddizionaleRegionale,
    decimal AddizionaleComunale,
    decimal ContributiInpsDipendente,
    decimal ContributiInpsDatore,
    string CodiceRegione = "",
    string CodiceComuneCatastale = "")
{
    /// <summary>Estrae da un cedolino calcolato gli importi rilevanti per l'F24.</summary>
    public static RigaPagaPerF24 DaCedolino(Cedolino c, string codiceRegione = "", string codiceComuneCatastale = "") =>
        new(c.IrpefNetta, c.AddizionaleRegionale, c.AddizionaleComunale,
            c.ContributiDipendente, c.ContributiDatore, codiceRegione, codiceComuneCatastale);
}

/// <summary>
/// Codici tributo/causale usati nell'F24 delle paghe. Valori di default di prassi diffusa:
/// vanno verificati con un Consulente del Lavoro e, dove serve, parametrizzati per anno.
/// </summary>
public sealed class CodiciTributoF24Paghe
{
    public string RitenuteIrpefLavoroDipendente { get; init; } = "1001";
    public string AddizionaleRegionale { get; init; } = "3802";
    public string AddizionaleComunaleSaldo { get; init; } = "3848";
    public string CausaleInps { get; init; } = "DM10";
}

/// <summary>
/// Costruisce il modello F24 da versare a partire dai cedolini di un periodo: ritenute IRPEF,
/// addizionali regionale/comunale e contributi INPS. Prepara solo i dati: il controllo
/// ufficiale e l'invio restano demandati ai canali ufficiali.
/// </summary>
public static class F24DaCedolini
{
    /// <summary>Scadenza ordinaria di versamento: il 16 del mese successivo al periodo di paga.</summary>
    public static DateOnly ScadenzaVersamento(int annoPeriodo, int mesePeriodo)
    {
        var periodo = new DateOnly(annoPeriodo, mesePeriodo, 1).AddMonths(1);
        return new DateOnly(periodo.Year, periodo.Month, 16);
    }

    public static ModelloF24 Costruisci(
        DatiSostitutoImposta sostituto,
        IReadOnlyList<RigaPagaPerF24> righe,
        int annoPeriodo,
        int mesePeriodo,
        DateOnly? dataVersamento = null,
        CodiciTributoF24Paghe? codici = null)
    {
        ArgumentNullException.ThrowIfNull(sostituto);
        ArgumentNullException.ThrowIfNull(righe);
        codici ??= new CodiciTributoF24Paghe();

        var erario = new List<VoceErario>();
        var regioni = new List<VoceRegioni>();
        var locali = new List<VoceImuTributiLocali>();
        var inps = new List<VoceInps>();

        decimal irpef = Arrotondamento.Euro(righe.Sum(r => r.RitenutaIrpef));
        if (irpef > 0m)
            erario.Add(new VoceErario(codici.RitenuteIrpefLavoroDipendente, annoPeriodo, ImportoDebito: irpef));

        foreach (var g in righe
                     .Where(r => r.AddizionaleRegionale > 0m && !string.IsNullOrWhiteSpace(r.CodiceRegione))
                     .GroupBy(r => r.CodiceRegione))
        {
            decimal tot = Arrotondamento.Euro(g.Sum(r => r.AddizionaleRegionale));
            regioni.Add(new VoceRegioni(g.Key, codici.AddizionaleRegionale, annoPeriodo, ImportoDebito: tot));
        }

        foreach (var g in righe
                     .Where(r => r.AddizionaleComunale > 0m && !string.IsNullOrWhiteSpace(r.CodiceComuneCatastale))
                     .GroupBy(r => r.CodiceComuneCatastale))
        {
            decimal tot = Arrotondamento.Euro(g.Sum(r => r.AddizionaleComunale));
            locali.Add(new VoceImuTributiLocali(g.Key, codici.AddizionaleComunaleSaldo, annoPeriodo, ImportoDebito: tot, Saldo: true));
        }

        decimal contributi = Arrotondamento.Euro(righe.Sum(r => r.ContributiInpsDipendente + r.ContributiInpsDatore));
        if (contributi > 0m)
        {
            string periodo = $"{mesePeriodo:00}/{annoPeriodo}";
            inps.Add(new VoceInps(
                sostituto.CodiceSedeInps, codici.CausaleInps, sostituto.MatricolaInps,
                periodo, periodo, ImportoDebito: contributi));
        }

        return new ModelloF24
        {
            Contribuente = new ContribuenteF24
            {
                CodiceFiscale = sostituto.CodiceFiscale,
                Tipo = TipoSoggettoF24.SoggettoDiverso,
                Denominazione = sostituto.Denominazione,
            },
            DataVersamento = dataVersamento ?? ScadenzaVersamento(annoPeriodo, mesePeriodo),
            Erario = erario,
            Regioni = regioni,
            TributiLocali = locali,
            Inps = inps,
        };
    }
}
