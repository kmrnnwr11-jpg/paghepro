namespace Paghe.Core.PartiteIva;

/// <summary>Forma del soggetto che vuole avviare l'attività IVA.</summary>
public enum FormaGiuridicaApertura
{
    ProfessionistaPersonaFisica,
    ImpresaIndividuale,
    SocietaPersone,
    SocietaCapitali,
}

/// <summary>Tipo di pratica gestita dalla Comunicazione Unica / Agenzia Entrate.</summary>
public enum TipoPraticaComUnica
{
    NuovaImpresa,
    Variazione,
    Cessazione,
}

/// <summary>Stato complessivo della pratica ComUnica (dalla bozza all'esito).</summary>
public enum StatoPraticaComUnica
{
    Bozza,
    Pronta,
    Presentata,
    Protocollata,
    Evasa,
    Scartata,
    Annullata,
}

/// <summary>Macchina a stati della pratica ComUnica: solo le transizioni ammesse.</summary>
public static class StatiPraticaComUnica
{
    private static readonly IReadOnlyDictionary<StatoPraticaComUnica, StatoPraticaComUnica[]> Transizioni =
        new Dictionary<StatoPraticaComUnica, StatoPraticaComUnica[]>
        {
            [StatoPraticaComUnica.Bozza] = [StatoPraticaComUnica.Pronta, StatoPraticaComUnica.Annullata],
            [StatoPraticaComUnica.Pronta] =
                [StatoPraticaComUnica.Presentata, StatoPraticaComUnica.Bozza, StatoPraticaComUnica.Annullata],
            [StatoPraticaComUnica.Presentata] = [StatoPraticaComUnica.Protocollata, StatoPraticaComUnica.Scartata],
            [StatoPraticaComUnica.Protocollata] = [StatoPraticaComUnica.Evasa, StatoPraticaComUnica.Scartata],
            [StatoPraticaComUnica.Scartata] = [StatoPraticaComUnica.Bozza],
            [StatoPraticaComUnica.Evasa] = [],
            [StatoPraticaComUnica.Annullata] = [],
        };

    public static bool PuoTransizionare(StatoPraticaComUnica da, StatoPraticaComUnica a) =>
        Transizioni.TryGetValue(da, out var ammessi) && Array.IndexOf(ammessi, a) >= 0;
}

/// <summary>Regime contabile/fiscale scelto in fase di avvio.</summary>
public enum RegimeContabilePartitaIva
{
    Forfettario,
    Semplificato,
    Ordinario,
}

/// <summary>Stato operativo di un adempimento della pratica.</summary>
public enum StatoAdempimentoPratica
{
    DaPreparare,
    Pronto,
    Presentato,
    Completato,
    NonApplicabile,
}

/// <summary>Dati raccolti per preparare una pratica di apertura partita IVA / ComUnica.</summary>
public sealed class PraticaAperturaPartitaIva
{
    /// <summary>Nuova impresa (default), variazione dati o cessazione attività.</summary>
    public TipoPraticaComUnica TipoPratica { get; init; } = TipoPraticaComUnica.NuovaImpresa;

    public required FormaGiuridicaApertura FormaGiuridica { get; init; }
    public required RegimeContabilePartitaIva RegimeContabile { get; init; }

    /// <summary>Data di decorrenza: inizio attività, effetto della variazione o cessazione.</summary>
    public required DateOnly DataInizioAttivita { get; init; }

    public required string Denominazione { get; init; }
    public required string CodiceFiscale { get; init; }
    public string? PartitaIva { get; init; }

    public required string CodiceAteco { get; init; }
    public required string DescrizioneAttivita { get; init; }

    public required string IndirizzoSede { get; init; }
    public required string Cap { get; init; }
    public required string Comune { get; init; }
    public required string Provincia { get; init; }
    public string? Pec { get; init; }

    public bool AttivitaCommercio { get; init; }
    public bool AttivitaArtigiana { get; init; }
    public bool ImpresaAgricola { get; init; }
    public bool HaDipendentiPrevisti { get; init; }
    public bool SoggettaInail { get; init; }
}

/// <summary>Singolo adempimento prodotto dal piano di apertura.</summary>
public sealed record AdempimentoAperturaPartitaIva(
    string Codice,
    string Ente,
    string Descrizione,
    bool Obbligatorio,
    StatoAdempimentoPratica Stato = StatoAdempimentoPratica.DaPreparare);

/// <summary>Output operativo: modelli, adempimenti e avvertenze per completare l'apertura.</summary>
public sealed class PianoAperturaPartitaIva
{
    public required string ModelloAgenziaEntrate { get; init; }
    public required bool RichiedeComunicazioneUnica { get; init; }
    public required bool RichiedeRegistroImprese { get; init; }
    public required bool RichiedeCameraCommercio { get; init; }
    public required IReadOnlyList<AdempimentoAperturaPartitaIva> Adempimenti { get; init; }
    public required IReadOnlyList<string> Avvertenze { get; init; }
}
