namespace Paghe.Core.Fatturazione.Trasmissione;

/// <summary>
/// Stato del ciclo di vita di una fattura elettronica verso il Sistema di Interscambio (SdI).
/// Riproduce gli esiti ufficiali: ricevuta di consegna (RC), mancata consegna (MC),
/// notifica di scarto (NS), esito committente B2G (NE), decorrenza termini (DT).
/// </summary>
public enum StatoTrasmissioneSdi
{
    /// <summary>File generato ma non ancora inviato a nessun canale.</summary>
    DaInviare,

    /// <summary>File preparato (eventualmente firmato) da caricare manualmente sul portale ufficiale.</summary>
    PreparataInvioManuale,

    /// <summary>Inviata al canale/provider, in attesa dell'esito SdI.</summary>
    Inviata,

    /// <summary>RC — Ricevuta di consegna: lo SdI ha consegnato la fattura al destinatario.</summary>
    ConsegnataSdi,

    /// <summary>MC — Mancata consegna: fattura messa a disposizione nell'area riservata del destinatario.</summary>
    MancataConsegna,

    /// <summary>NS — Notifica di scarto: la fattura è stata respinta dallo SdI.</summary>
    ScartataSdi,

    /// <summary>NE — Esito committente (B2G/PA): accettazione.</summary>
    AccettataPa,

    /// <summary>NE — Esito committente (B2G/PA): rifiuto.</summary>
    RifiutataPa,

    /// <summary>DT — Decorrenza termini: nessun esito del committente nei termini previsti.</summary>
    DecorrenzaTermini,

    /// <summary>Errore tecnico nella trasmissione (rete, autenticazione, provider).</summary>
    Errore,
}

/// <summary>Documento pronto per la trasmissione (XML o pacchetto firmato .p7m).</summary>
public sealed record FatturaDaTrasmettere(
    Guid FatturaId,
    string NomeFile,
    byte[] Contenuto,
    bool Firmato,
    string FormatoTrasmissione = "FPR12");

/// <summary>Esito di una chiamata di trasmissione al canale/provider.</summary>
public sealed record RisultatoTrasmissioneSdi(
    StatoTrasmissioneSdi Stato,
    string? IdentificativoSdi = null,
    string? Messaggio = null,
    string? CodiceErrore = null);

/// <summary>Esito/notifica restituiti dallo SdI per una fattura già inviata.</summary>
public sealed record EsitoSdi(
    StatoTrasmissioneSdi Stato,
    string? IdentificativoSdi = null,
    string? CodiceEsito = null,
    string? Descrizione = null,
    DateTimeOffset? DataOra = null,
    string? NomeFileRicevuta = null,
    string? ContenutoGrezzo = null);

/// <summary>
/// Astrazione del canale di trasmissione verso lo SdI. Le implementazioni possono essere:
/// un provider accreditato (REST), il canale SdICoop proprio, oppure la preparazione del
/// file per l'invio manuale dal portale ufficiale.
/// </summary>
public interface IFatturaSdiGateway
{
    /// <summary>Nome del canale (per audit e diagnostica).</summary>
    string Nome { get; }

    /// <summary>Vero se il canale richiede che la fattura arrivi già firmata localmente (.p7m).</summary>
    bool RichiedeFirmaLocale { get; }

    /// <summary>Trasmette il documento e restituisce lo stato iniziale e l'identificativo SdI/provider.</summary>
    Task<RisultatoTrasmissioneSdi> TrasmettiAsync(FatturaDaTrasmettere fattura, CancellationToken ct = default);

    /// <summary>Interroga il canale per l'esito aggiornato di una fattura già inviata.</summary>
    Task<EsitoSdi?> InterrogaEsitoAsync(string identificativoSdi, CancellationToken ct = default);
}

/// <summary>Documento dopo l'eventuale apposizione della firma digitale.</summary>
public sealed record FatturaFirmata(byte[] Contenuto, string NomeFile, bool Firmato);

/// <summary>
/// Astrazione della firma digitale (CAdES, file .p7m) della fattura elettronica.
/// In molti scenari la firma è applicata lato server dal provider accreditato; in quel
/// caso si usa <see cref="FirmaDelegataAlProvider"/>.
/// </summary>
public interface IFirmaFatturaElettronica
{
    /// <summary>Vero se questo componente applica davvero la firma (false = la applica il provider).</summary>
    bool FirmaLocale { get; }

    Task<FatturaFirmata> FirmaAsync(byte[] xml, string nomeFileXml, CancellationToken ct = default);
}

/// <summary>
/// Firma delegata al provider accreditato: il documento viene inviato non firmato e il
/// provider appone la firma lato server prima della trasmissione allo SdI.
/// </summary>
public sealed class FirmaDelegataAlProvider : IFirmaFatturaElettronica
{
    public bool FirmaLocale => false;

    public Task<FatturaFirmata> FirmaAsync(byte[] xml, string nomeFileXml, CancellationToken ct = default) =>
        Task.FromResult(new FatturaFirmata(xml, nomeFileXml, Firmato: false));
}

/// <summary>
/// Segnaposto per la firma CAdES locale non ancora configurata: solleva un errore esplicito
/// quando il canale richiede la firma locale ma non è disponibile un certificato/dispositivo.
/// Sostituire con un'implementazione reale (HSM, smart card, firma remota) prima della messa in produzione.
/// </summary>
public sealed class FirmaCadesNonConfigurata : IFirmaFatturaElettronica
{
    public bool FirmaLocale => true;

    public Task<FatturaFirmata> FirmaAsync(byte[] xml, string nomeFileXml, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Firma digitale CAdES non configurata: serve un certificato di firma (smart card/HSM/firma remota) " +
            "oppure usare un provider che firma lato server (FirmaDelegataAlProvider).");
}

/// <summary>Utilità per il nome file della FatturaPA secondo le specifiche SdI.</summary>
public static class NomeFileFatturaPa
{
    private const string AlfabetoBase36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Nome file nel formato ufficiale: <c>{Paese}{IdTrasmittente}_{progressivo}.xml</c>
    /// (es. <c>IT01234567890_00001.xml</c>). Il progressivo univoca i file dello stesso trasmittente.
    /// </summary>
    public static string Genera(string paese, string idTrasmittente, string progressivo, bool firmato = false)
    {
        string baseNome = $"{paese}{idTrasmittente}_{progressivo}";
        return firmato ? $"{baseNome}.xml.p7m" : $"{baseNome}.xml";
    }

    /// <summary>Progressivo alfanumerico (base36) di 5 caratteri, derivato da un GUID.</summary>
    public static string ProgressivoCasuale()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        ulong valore = BitConverter.ToUInt64(bytes, 0);
        Span<char> buffer = stackalloc char[5];
        for (int i = 4; i >= 0; i--)
        {
            buffer[i] = AlfabetoBase36[(int)(valore % 36)];
            valore /= 36;
        }
        return new string(buffer);
    }
}

/// <summary>Mappa i tipi di ricevuta/notifica SdI nello stato corrispondente.</summary>
public static class MappaturaRicevuteSdi
{
    /// <summary>
    /// Converte il tipo di notifica SdI in stato. Tipi: RC (consegna), MC (mancata consegna),
    /// NS (scarto), NE (esito committente: usa il codice EC01=accettazione / EC02=rifiuto), DT (decorrenza termini).
    /// </summary>
    public static StatoTrasmissioneSdi DaTipo(string tipo, string? codiceEsito = null)
    {
        return (tipo?.Trim().ToUpperInvariant()) switch
        {
            "RC" => StatoTrasmissioneSdi.ConsegnataSdi,
            "MC" => StatoTrasmissioneSdi.MancataConsegna,
            "NS" => StatoTrasmissioneSdi.ScartataSdi,
            "DT" => StatoTrasmissioneSdi.DecorrenzaTermini,
            "NE" => string.Equals(codiceEsito?.Trim(), "EC02", StringComparison.OrdinalIgnoreCase)
                ? StatoTrasmissioneSdi.RifiutataPa
                : StatoTrasmissioneSdi.AccettataPa,
            _ => StatoTrasmissioneSdi.Inviata,
        };
    }
}
