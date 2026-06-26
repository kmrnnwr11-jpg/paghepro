using Paghe.Core.Fatturazione.Trasmissione;

namespace Paghe.Persistence.Trasmissione;

/// <summary>
/// Canale "manuale": non effettua alcun invio di rete. Prepara il file (firmato o meno) e
/// lo marca come pronto per il caricamento sul portale ufficiale "Fatture e Corrispettivi"
/// dell'Agenzia delle Entrate o sul provider. È il canale di default finché non si configura
/// un provider accreditato: onesto e utilizzabile senza credenziali esterne.
/// </summary>
public sealed class SdiGatewayManuale : IFatturaSdiGateway
{
    public string Nome => "Manuale (portale Fatture e Corrispettivi)";

    // L'invio manuale dal portale richiede un file XML; la firma può essere apposta dal portale
    // stesso o da dispositivo dell'utente, quindi qui non imponiamo la firma locale preventiva.
    public bool RichiedeFirmaLocale => false;

    public Task<RisultatoTrasmissioneSdi> TrasmettiAsync(FatturaDaTrasmettere fattura, CancellationToken ct = default) =>
        Task.FromResult(new RisultatoTrasmissioneSdi(
            StatoTrasmissioneSdi.PreparataInvioManuale,
            IdentificativoSdi: null,
            Messaggio: $"File '{fattura.NomeFile}' pronto. Firmarlo (se non firmato) e caricarlo sul portale " +
                       "Fatture e Corrispettivi dell'Agenzia delle Entrate o consegnarlo all'intermediario."));

    // Il canale manuale non interroga esiti: gli esiti SdI vanno registrati manualmente o via webhook.
    public Task<EsitoSdi?> InterrogaEsitoAsync(string identificativoSdi, CancellationToken ct = default) =>
        Task.FromResult<EsitoSdi?>(null);
}
