namespace Paghe.Core.AdeTelematico;

/// <summary>Tipo di soggetto intestatario del modello F24.</summary>
public enum TipoSoggettoF24
{
    PersonaFisica,
    SoggettoDiverso,
}

/// <summary>Dati anagrafici del contribuente intestatario del modello F24.</summary>
public sealed class ContribuenteF24
{
    public required string CodiceFiscale { get; init; }
    public required TipoSoggettoF24 Tipo { get; init; }

    // Persona fisica
    public string? Cognome { get; init; }
    public string? Nome { get; init; }
    public string? SessoMF { get; init; }
    public string? ComuneNascita { get; init; }
    public string? ProvinciaNascita { get; init; }

    // Soggetto diverso da persona fisica
    public string? Denominazione { get; init; }

    // Domicilio fiscale
    public string? Comune { get; init; }
    public string? Provincia { get; init; }
    public string? Indirizzo { get; init; }
}

/// <summary>Intermediario che presenta/trasmette il modello, quando previsto.</summary>
public sealed class IntermediarioF24
{
    public required string CodiceFiscale { get; init; }
    public string? Denominazione { get; init; }
}

/// <summary>Riga della sezione Erario (es. ritenute IRPEF, codice tributo 1001).</summary>
public sealed record VoceErario(
    string CodiceTributo,
    int AnnoRiferimento,
    decimal ImportoDebito = 0m,
    decimal ImportoCredito = 0m,
    string? Rateazione = null);

/// <summary>Riga della sezione INPS (contributi: causale, matricola, periodo).</summary>
public sealed record VoceInps(
    string CodiceSede,
    string CausaleContributo,
    string MatricolaCodice,
    string PeriodoDa,
    string? PeriodoA = null,
    decimal ImportoDebito = 0m,
    decimal ImportoCredito = 0m);

/// <summary>Riga della sezione Regioni (es. addizionale regionale IRPEF, tributo 3802).</summary>
public sealed record VoceRegioni(
    string CodiceRegione,
    string CodiceTributo,
    int AnnoRiferimento,
    decimal ImportoDebito = 0m,
    decimal ImportoCredito = 0m,
    string? Rateazione = null);

/// <summary>Riga della sezione IMU e altri tributi locali (es. addizionale comunale, tributo 3848).</summary>
public sealed record VoceImuTributiLocali(
    string CodiceEnteComune,
    string CodiceTributo,
    int AnnoRiferimento,
    decimal ImportoDebito = 0m,
    decimal ImportoCredito = 0m,
    bool Ravvedimento = false,
    bool ImmobiliVariati = false,
    bool Acconto = false,
    bool Saldo = false,
    int NumeroImmobili = 0,
    string? Rateazione = null);

/// <summary>Riga della sezione Altri enti previdenziali e assicurativi (es. INAIL).</summary>
public sealed record VoceAltriEnti(
    string CodiceEnte,
    string CodiceSede,
    string CausaleContributo,
    string CodicePosizione,
    string? PeriodoDa = null,
    string? PeriodoA = null,
    decimal ImportoDebito = 0m,
    decimal ImportoCredito = 0m);

/// <summary>
/// Modello F24 ordinario: contribuente, eventuale intermediario e le sezioni dei tributi.
/// Aggregato di puro dominio: rappresenta i dati, non esegue alcun invio telematico.
/// </summary>
public sealed class ModelloF24
{
    public required ContribuenteF24 Contribuente { get; init; }
    public IntermediarioF24? Intermediario { get; init; }
    public required DateOnly DataVersamento { get; init; }

    public IReadOnlyList<VoceErario> Erario { get; init; } = [];
    public IReadOnlyList<VoceInps> Inps { get; init; } = [];
    public IReadOnlyList<VoceRegioni> Regioni { get; init; } = [];
    public IReadOnlyList<VoceImuTributiLocali> TributiLocali { get; init; } = [];
    public IReadOnlyList<VoceAltriEnti> AltriEnti { get; init; } = [];

    /// <summary>Tutte le righe dell'F24, indipendentemente dalla sezione, come coppie (debito, credito).</summary>
    public IEnumerable<(decimal Debito, decimal Credito)> TutteLeVoci()
    {
        foreach (var v in Erario) yield return (v.ImportoDebito, v.ImportoCredito);
        foreach (var v in Inps) yield return (v.ImportoDebito, v.ImportoCredito);
        foreach (var v in Regioni) yield return (v.ImportoDebito, v.ImportoCredito);
        foreach (var v in TributiLocali) yield return (v.ImportoDebito, v.ImportoCredito);
        foreach (var v in AltriEnti) yield return (v.ImportoDebito, v.ImportoCredito);
    }

    public decimal TotaleDebiti => TutteLeVoci().Sum(v => v.Debito);
    public decimal TotaleCrediti => TutteLeVoci().Sum(v => v.Credito);

    /// <summary>Saldo finale del modello: totale a debito meno totale a credito compensato.</summary>
    public decimal SaldoFinale => TotaleDebiti - TotaleCrediti;
}
