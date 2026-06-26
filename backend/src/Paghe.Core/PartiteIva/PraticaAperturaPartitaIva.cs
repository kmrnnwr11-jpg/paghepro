namespace Paghe.Core.PartiteIva;

/// <summary>Forma del soggetto che vuole avviare l'attività IVA.</summary>
public enum FormaGiuridicaApertura
{
    ProfessionistaPersonaFisica,
    ImpresaIndividuale,
    SocietaPersone,
    SocietaCapitali,
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
    public required FormaGiuridicaApertura FormaGiuridica { get; init; }
    public required RegimeContabilePartitaIva RegimeContabile { get; init; }
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
