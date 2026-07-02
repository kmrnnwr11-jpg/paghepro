# Guida — attivare l'accesso con Google ("Continua con Google")

Il codice è già pronto (frontend + backend). Per accenderlo serve un **ID client Google**
(gratuito), che crei tu in Google Cloud, e poi lo incolli in 2 posti. ~5 minuti.

> L'ID client **non è un segreto** (sta nel frontend, è pubblico). La verifica del token avviene
> lato backend contro Google. Aggiornato al 2026-06-29.

---

## Passo 1 — Crea l'ID client Google
1. Vai su <https://console.cloud.google.com/> e crea (o scegli) un **progetto**.
2. Menu → **API e servizi → Schermata consenso OAuth**:
   - Tipo utente: **Esterno** → Crea.
   - Nome app: *PaghePro* · Email di supporto: **kmrnnwr11@gmail.com** · Email sviluppatore: la tua.
   - Salva (per i test va bene lasciarla in stato "Test").
3. Menu → **API e servizi → Credenziali → Crea credenziali → ID client OAuth**:
   - Tipo di applicazione: **Applicazione web**.
   - **Origini JavaScript autorizzate** → Aggiungi URI:
     - `http://localhost:5173` (sviluppo, interfaccia)
     - (in futuro) l'indirizzo del tuo sito online, es. `https://tuosito.it`
   - Crea → copia l'**ID client** (una stringa tipo `1234-abcd.apps.googleusercontent.com`).

## Passo 2 — Incolla l'ID in 2 posti

**a) Frontend** — crea il file `frontend/.env` con dentro:
```
VITE_GOOGLE_CLIENT_ID=IL-TUO-ID-CLIENT.apps.googleusercontent.com
```

**b) Backend** — come user-secret (consigliato) o variabile d'ambiente:
```bash
cd backend
dotnet user-secrets set "Google:ClientId" "IL-TUO-ID-CLIENT.apps.googleusercontent.com" --project src/Paghe.Api
```
(in alternativa: variabile d'ambiente `Google__ClientId`, oppure il campo `Google:ClientId` in
`appsettings.json`).

## Passo 3 — Riavvia e prova
```bash
cd backend && dotnet run --project src/Paghe.Api -c Release
cd frontend && npm run dev
```
Apri l'interfaccia → premi **"Continua con Google"** → scegli il tuo account Google → entri.
Se l'email è `kmrnnwr11@gmail.com`, entri direttamente come **super-admin**.

---

## Come funziona (tecnico)
- Frontend: carica **Google Identity Services**; al click chiede l'account e ottiene un *ID token*.
- Lo invia a **`POST /api/auth/google`** (`api.googleLogin`).
- Backend: verifica il token via `https://oauth2.googleapis.com/tokeninfo` (controlla che
  `aud` = il tuo Client ID e che l'email sia verificata), poi **accede o crea** l'utente per
  quell'email (`AuthService.AccediConGoogleAsync`) e rilascia il nostro JWT.
- Utenti creati via Google non hanno password (accesso solo via Google).

## Note
- Senza `Google:ClientId` configurato, il bottone mostra solo un avviso e l'endpoint risponde
  "non configurato" (501): è voluto.
- Per andare **online** ricordati di aggiungere l'origine del sito di produzione tra le
  "Origini JavaScript autorizzate" e di impostare le due variabili sull'host.
