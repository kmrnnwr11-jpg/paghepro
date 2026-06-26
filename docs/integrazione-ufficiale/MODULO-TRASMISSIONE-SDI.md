# Modulo trasmissione Fattura Elettronica → SdI

Data: 2026-06-26.

Questo modulo aggiunge il "modem" che mancava: invio della fattura elettronica al Sistema
di Interscambio (SdI), firma, e tracciamento di ricevute/esiti. È la base per rendere l'app
davvero "connessa". Parte del flusso (accreditamento, certificati, account provider) resta
amministrativa e va completata fuori dal codice.

## Architettura

Il principio è **generare → controllare → firmare → inviare solo via canale ufficiale**.

- **Dominio/astrazioni** (`Paghe.Core/Fatturazione/Trasmissione/TrasmissioneSdi.cs`):
  - `StatoTrasmissioneSdi`: DaInviare, PreparataInvioManuale, Inviata, ConsegnataSdi (RC),
    MancataConsegna (MC), ScartataSdi (NS), AccettataPa/RifiutataPa (NE), DecorrenzaTermini (DT), Errore.
  - `IFatturaSdiGateway`: il canale di trasmissione (provider, SdICoop, manuale).
  - `IFirmaFatturaElettronica`: la firma digitale (CAdES/.p7m) o firma delegata al provider.
  - `NomeFileFatturaPa`: nome file ufficiale `ITxxxxxxxxxxx_<progressivo>.xml`.
  - `MappaturaRicevuteSdi`: mappa i tipi di notifica SdI (RC/MC/NS/NE/DT) negli stati.
- **Implementazioni canale** (`Paghe.Persistence/Trasmissione/`):
  - `SdiGatewayManuale` (default): non invia in rete, prepara il file da firmare e caricare sul
    portale "Fatture e Corrispettivi" o da consegnare all'intermediario. Utilizzabile **subito**, senza credenziali.
  - `SdiGatewayProviderRest`: client REST generico verso un provider accreditato (Aruba, Fatture
    in Cloud, Acube, Openapi, ...). Usa `HttpClient` con auth Bearer; mappa la risposta JSON.
- **Firma**:
  - `FirmaDelegataAlProvider` (default): il provider firma lato server.
  - `FirmaCadesNonConfigurata`: segnaposto che blocca con errore esplicito finché non si
    configura un certificato di firma locale.
- **Orchestrazione/persistenza** (`Paghe.Persistence/`):
  - `TrasmissioneFattureService`: genera l'XML dalla fattura persistita, esegue i `ControlliSdi`,
    firma/delega, invia, salva `TrasmissioneFatturaEntity` + `RicevutaSdiEntity`. Blocca i doppi invii.
  - `DelegheService`: deleghe cliente→intermediario per adempimento (`DelegaEntity`), isolate per tenant.

## Endpoint API

- `POST /api/fatture/{id}/trasmetti` — trasmette la fattura (blocca se non conforme SdI o già inviata).
- `GET  /api/fatture/{id}/trasmissione` — ultimo stato di trasmissione della fattura.
- `GET  /api/fatture/{id}/xml` — XML della fattura persistita (per firma/upload manuale).
- `GET  /api/trasmissioni` / `GET /api/trasmissioni/{id}` — elenco/dettaglio con ricevute.
- `POST /api/trasmissioni/{id}/ricevute` — registra una notifica SdI (RC/MC/NS/NE/DT), via webhook provider o manuale.
- `POST /api/trasmissioni/{id}/aggiorna-esito` — interroga il provider per l'esito aggiornato.
- `POST /api/deleghe` / `GET /api/deleghe` / `POST /api/deleghe/{id}/revoca` — deleghe intermediario.

## Configurazione (appsettings / secret)

Default = canale manuale (nessun invio in rete). Per passare a un provider reale:

```json
{
  "Sdi": {
    "Provider": "Rest",
    "BaseUrl": "https://api.provider.it/v1/",
    "ApiKey": "<segreto-da-vault>",
    "EndpointInvio": "invoices",
    "EndpointEsito": "invoices/{id}",
    "FirmaLocale": false,
    "CampoId": "id",
    "CampoStato": "status"
  }
}
```

Il mapping degli stati del provider è in `SdiGatewayProviderRest.MappaStatoProvider` e va
allineato ai valori reali del provider scelto (la documentazione del provider elenca gli stati).

## Cosa resta da completare (fuori dal codice o fasi successive)

- **Account/contratto** con il provider accreditato e `ApiKey` reale.
- **Firma**: o delegata al provider, oppure `FirmaCadesNonConfigurata` → implementazione reale
  (smart card/HSM/firma remota) se si firma localmente.
- **Webhook** del provider per ricezione automatica delle ricevute (oggi: endpoint pronto, va
  collegato al provider; in alternativa polling con `aggiorna-esito`).
- **Conservazione a norma** dei file e delle ricevute (10 anni).
- Canale **SdICoop proprio** (alternativa al provider) solo a volumi alti: richiede accreditamento.
