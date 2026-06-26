namespace Paghe.Core.AdeTelematico;

/// <summary>Tipo di adempimento telematico gestito dalla pratica.</summary>
public enum TipoModelloTelematico
{
    F24,
    CertificazioneUnica,
    Modello770,
    AA7_10,
    AA9_12,
    ComUnica,
}

/// <summary>
/// Stato di una pratica telematica nel flusso ufficiale
/// genera → controlla → (firma) → esporta → invio manuale via Desktop Telematico → ricevuta.
/// </summary>
public enum StatoPraticaTelematica
{
    Bozza,
    Generata,
    ControllataOk,
    ControllataKo,
    ProntaPerDesktopTelematico,
    InviataManualmente,
    RicevutaAcquisita,
    Scartata,
}

/// <summary>
/// Pratica telematica con la sua macchina a stati. Modella solo il ciclo di vita:
/// la generazione del file, i controlli e l'invio sono delegati ai servizi dedicati.
/// L'invio reale avviene tramite canale ufficiale (Desktop Telematico/Entratel), non dal software.
/// </summary>
public sealed class PraticaTelematica
{
    private static readonly IReadOnlyDictionary<StatoPraticaTelematica, StatoPraticaTelematica[]> Transizioni =
        new Dictionary<StatoPraticaTelematica, StatoPraticaTelematica[]>
        {
            [StatoPraticaTelematica.Bozza] = [StatoPraticaTelematica.Generata],
            [StatoPraticaTelematica.Generata] =
                [StatoPraticaTelematica.ControllataOk, StatoPraticaTelematica.ControllataKo, StatoPraticaTelematica.Bozza],
            [StatoPraticaTelematica.ControllataKo] = [StatoPraticaTelematica.Bozza, StatoPraticaTelematica.Generata],
            [StatoPraticaTelematica.ControllataOk] =
                [StatoPraticaTelematica.ProntaPerDesktopTelematico, StatoPraticaTelematica.Bozza],
            [StatoPraticaTelematica.ProntaPerDesktopTelematico] =
                [StatoPraticaTelematica.InviataManualmente, StatoPraticaTelematica.Bozza],
            [StatoPraticaTelematica.InviataManualmente] =
                [StatoPraticaTelematica.RicevutaAcquisita, StatoPraticaTelematica.Scartata],
            [StatoPraticaTelematica.Scartata] = [StatoPraticaTelematica.Bozza],
            [StatoPraticaTelematica.RicevutaAcquisita] = [],
        };

    public PraticaTelematica(Guid id, Guid tenantId, TipoModelloTelematico tipo, int annoFiscale)
    {
        Id = id;
        TenantId = tenantId;
        Tipo = tipo;
        AnnoFiscale = annoFiscale;
    }

    public Guid Id { get; }
    public Guid TenantId { get; }
    public TipoModelloTelematico Tipo { get; }
    public int AnnoFiscale { get; }
    public StatoPraticaTelematica Stato { get; private set; } = StatoPraticaTelematica.Bozza;

    /// <summary>Vero se dallo stato corrente è ammesso transitare verso <paramref name="nuovo"/>.</summary>
    public bool PuoTransizionareA(StatoPraticaTelematica nuovo) =>
        Transizioni.TryGetValue(Stato, out var ammessi) && Array.IndexOf(ammessi, nuovo) >= 0;

    /// <summary>Applica la transizione, lanciando se non ammessa dallo stato corrente.</summary>
    public void Transiziona(StatoPraticaTelematica nuovo)
    {
        if (!PuoTransizionareA(nuovo))
            throw new InvalidOperationException(
                $"Transizione non ammessa da {Stato} a {nuovo} per la pratica {Tipo}.");
        Stato = nuovo;
    }
}
