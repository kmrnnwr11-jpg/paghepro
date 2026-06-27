# Guida: collegare A-Cube per l'invio reale delle fatture allo SdI

Questa guida spiega, passo per passo, come attivare l'invio reale delle fatture elettroniche
tramite il provider accreditato **A-Cube**. Il software è già pronto: serve solo aprire l'account
A-Cube e inserire le credenziali.

> In breve: il software genera l'XML FatturaPA → A-Cube lo **firma e lo trasmette allo SdI** →
> il software riceve l'esito (consegnata/scartata).

---

## Cosa fa A-Cube (e cosa fa il nostro software)

| Fase | Chi la fa |
|---|---|
| Crea la fattura, calcola IVA, genera l'XML FatturaPA | Il nostro software |
| Controlli SdI prima dell'invio | Il nostro software |
| Firma digitale + invio allo SdI | **A-Cube** (lato server) |
| Ricezione ricevute/notifiche SdI | A-Cube → lette dal nostro software |

Con A-Cube **non serve** una smart card o un certificato di firma tuo: firma il provider.

---

## Passo 1 — Apri l'account A-Cube

1. Vai su <https://acubeapi.com> e registra un account (o fallo fare al commercialista).
2. Chiedi l'accesso all'**API di e-invoicing Italia** e all'ambiente **sandbox** (prove).
3. Riceverai delle **credenziali API**: una **email** e una **password** dedicate alle API
   (sono diverse dalla password del sito; verifica nella tua dashboard A-Cube).

## Passo 2 — Registra l'azienda emittente su A-Cube

Prima di poter inviare, A-Cube deve conoscere la **P.IVA dell'azienda** che emette le fatture
(business registry). Aggiungi l'azienda nella dashboard A-Cube (o tramite la loro API) e, se
operi come intermediario per più clienti, registra ogni P.IVA. Questo abilita l'invio per quel
soggetto.

## Passo 3 — Inserisci le credenziali nel software (in modo sicuro)

Le credenziali **non vanno scritte nel codice né committate**. Usa i *user secrets* in sviluppo o
le **variabili d'ambiente** in produzione.

In sviluppo (consigliato):

```bash
cd backend/src/Paghe.Api
dotnet user-secrets init
dotnet user-secrets set "Sdi:Provider" "Acube"
dotnet user-secrets set "Acube:Email" "TUA-EMAIL-API-ACUBE"
dotnet user-secrets set "Acube:Password" "TUA-PASSWORD-API-ACUBE"
dotnet user-secrets set "Acube:Environment" "sandbox"
```

In produzione, le stesse chiavi come variabili d'ambiente:

```bash
export Sdi__Provider=Acube
export Acube__Email=...
export Acube__Password=...
export Acube__Environment=production
```

(In alternativa, per una prova rapida, puoi compilare la sezione `Acube` in
`appsettings.json` e mettere `Sdi.Provider` = `Acube` — ma NON committare le credenziali.)

## Passo 4 — Prova in sandbox

1. Avvia il backend: `cd backend && dotnet run --project src/Paghe.Api -c Release`
2. Avvia il frontend: `cd frontend && npm run dev`
3. Vai su **🧾 Fatturazione**, crea una fattura, premi **Trasmetti**.
   - Stato atteso: **Inviata** con un identificativo (UUID di A-Cube).
4. Premi **Aggiorna esito** per leggere lo stato da A-Cube (es. **Consegnata**).

In sandbox lo SdI è simulato da A-Cube: nessun documento reale viene emesso. È il posto giusto
per verificare l'intero flusso.

## Passo 5 — Vai in produzione (go-live)

Quando la sandbox funziona:

1. Cambia `Acube:Environment` in `production`.
2. Assicurati che la P.IVA emittente sia registrata anche nell'ambiente di produzione di A-Cube.
3. Emetti una prima fattura reale di prova e controlla l'esito.

---

## Dettagli tecnici (per chi sviluppa)

- **Login**: `POST https://common.api.acubeapi.com/login` con `{ email, password, environment }`
  → risposta `{ token }` (JWT valido 24h). Il software lo richiede una volta e lo riusa.
- **Invio**: `POST {base}/invoices` con l'XML FatturaPA nel body → risposta con `uuid`, HTTP 202.
  Base: `https://api-sandbox.acubeapi.com` (sandbox) o `https://api.acubeapi.com` (produzione).
- **Esito**: `GET {base}/invoices/{uuid}` → notifiche SdI (RC/MC/NS/NE/DT), mappate negli stati interni.
- Implementazione: `backend/src/Paghe.Persistence/Trasmissione/SdiGatewayAcube.cs`
  (opzioni in `AcubeOptions`, sezione config `Acube`).

> Nota: i nomi esatti dei campi della risposta (es. `uuid`, struttura `notifications`) sono
> resi configurabili in `AcubeOptions`; verificali nella sandbox A-Cube e, se differiscono,
> aggiorna `CampoUuid`/`EndpointInvio`/`EndpointEsito` o il mapping in `LeggiStato`.
> Documentazione ufficiale: <https://docs.acubeapi.com/>

## Sicurezza e adempimenti

- Non committare email/password A-Cube: usa user-secrets o variabili d'ambiente.
- Conservazione a norma (10 anni): A-Cube offre il servizio di conservazione — valuta di attivarlo.
- Deleghe: se invii per conto di clienti, registra la delega del cliente (sezione Deleghe del software).
