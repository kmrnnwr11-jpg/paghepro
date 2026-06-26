using Paghe.Core.Fatturazione.Trasmissione;

namespace Paghe.Persistence.Trasmissione;

/// <summary>
/// Canale SdI SIMULATO per demo/sviluppo: non invia nulla in rete ma riproduce il ciclo di vita
/// completo (Inviata → con identificativo → Consegnata) così da poter provare end-to-end la
/// trasmissione e gli esiti senza un account provider reale. NON usare in produzione: lo stato
/// "Provider" lo etichetta esplicitamente come demo.
/// </summary>
public sealed class SdiGatewaySimulato : IFatturaSdiGateway
{
    public string Nome => "Simulatore SdI (demo)";

    public bool RichiedeFirmaLocale => false;

    public Task<RisultatoTrasmissioneSdi> TrasmettiAsync(FatturaDaTrasmettere fattura, CancellationToken ct = default)
    {
        // Identificativo fittizio nello stile dello SdI (numerico), per provare il polling esiti.
        string idSdi = "SIM" + DateTime.UtcNow.ToString("yyMMddHHmmss");
        return Task.FromResult(new RisultatoTrasmissioneSdi(
            StatoTrasmissioneSdi.Inviata,
            IdentificativoSdi: idSdi,
            Messaggio: "Trasmissione simulata: nessun invio reale. Usa 'Aggiorna esito' per simulare la consegna."));
    }

    // Simula la consegna al destinatario (ricevuta RC) alla prima interrogazione dell'esito.
    public Task<EsitoSdi?> InterrogaEsitoAsync(string identificativoSdi, CancellationToken ct = default) =>
        Task.FromResult<EsitoSdi?>(new EsitoSdi(
            StatoTrasmissioneSdi.ConsegnataSdi,
            IdentificativoSdi: identificativoSdi,
            CodiceEsito: "RC",
            Descrizione: "Consegna simulata al destinatario.",
            DataOra: DateTimeOffset.UtcNow));
}
