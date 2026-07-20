# Mettere Paghe SaaS online — guida passo-passo

Questa guida porta il software **su internet**, accessibile da un indirizzo web, senza
modificare il codice. Usiamo **Render.com** perché ha un'unica dashboard per backend +
frontend ed è adatto a chi non è tecnico.

## Cosa mettiamo online

Il software ha 3 pezzi → su Render diventano **2 servizi**:

| Pezzo | Cos'è | Servizio Render |
|-------|-------|-----------------|
| **Backend** (`backend/`) | la "cucina": calcola cedolini, login, salva dati | Web Service (Docker) + **disco** per il database |
| **Database** | dove restano i dati (file SQLite) | vive sul **disco** del backend |
| **Frontend** (`frontend/`) | la grafica React che hai visto | Static Site |

Tutto è già descritto nel file [`render.yaml`](render.yaml): Render lo legge e crea i servizi da solo.

## Cosa ti serve prima (una volta sola)

1. Un account **GitHub** (gratis) → https://github.com
2. Un account **Render** (gratis l'iscrizione) → https://render.com — accedi con GitHub
3. Una **carta** per il piano `starter` del backend (~**7 $/mese**). Serve perché il *disco
   persistente* (dove restano i dati) è disponibile solo sui piani a pagamento.
   *(Per fare solo una prova gratuita, vedi "Variante gratis" in fondo: i dati però si perdono ai riavvii.)*

---

## Passo 1 — Mettere il codice su GitHub

Il deploy automatico legge il codice da GitHub. Apri il Terminale ed esegui (copia-incolla):

```bash
cd "/Users/kmrnnwr/Downloads/software buste paghe"
git init
git add .
git commit -m "Paghe SaaS: primo deploy"
```

Poi su GitHub crea un repository **vuoto** (pulsante "New", nome es. `paghe-saas`, lascialo
senza README) e collega + carica il codice con i comandi che GitHub ti mostra, tipo:

```bash
git branch -M main
git remote add origin https://github.com/TUO-UTENTE/paghe-saas.git
git push -u origin main
```

> Da ora, ogni volta che vorrai aggiornare il sito online, ti basterà:
> `git add . && git commit -m "modifiche" && git push`

---

## Passo 2 — Creare i servizi su Render (Blueprint)

1. Su Render: **New ▸ Blueprint**.
2. Seleziona il repository `paghe-saas` appena caricato.
3. Render legge `render.yaml` e propone due servizi: **paghe-api** e **paghe-frontend**.
4. Prima di confermare, genera sul tuo computer `openssl rand -base64 32` e incolla l'output nel
   campo segreto **`PAGHE_DATA_KEY`** richiesto dal Blueprint. Conservalo anche in un gestore di
   password/recovery separato dai backup: se lo perdi, i dati non sono recuperabili.
5. Premi **Apply**. Render:
   - costruisce il backend dal `Dockerfile`, crea il disco da 1 GB su `/data`,
     e genera da solo la chiave segreta `Jwt__Key`;
   - costruisce il frontend (`npm run build`).
6. Aspetta che **paghe-api** diventi *Live*. Copia il suo indirizzo, sarà tipo:
   `https://paghe-api.onrender.com`

---

## Passo 3 — Collegare il frontend al backend

Il frontend deve sapere dov'è la cucina:

1. Apri il servizio **paghe-frontend ▸ Environment**.
2. Trova la variabile `VITE_API_URL` e mettici l'indirizzo del backend del Passo 2
   (es. `https://paghe-api.onrender.com`), **senza** la barra finale.
3. Salva → **Manual Deploy ▸ Deploy latest commit** (così il frontend si ricostruisce con l'indirizzo giusto).

---

## Passo 4 — Provare

Apri l'indirizzo di **paghe-frontend** (es. `https://paghe-frontend.onrender.com`).
Dovresti vedere la Dashboard, esattamente come in locale. L'accesso demo è automatico.

✅ Fatto: il software è online.

---

## Note utili

- **Costi**: backend `starter` ~7 $/mese (per il disco). Il frontend statico è gratis.
- **Sicurezza**: la chiave JWT è generata da Render; `PAGHE_DATA_KEY` è fornita come secret e
  cifra i campi sensibili. Nessun segreto è nel codice.
- **Dati al sicuro**: restano sul disco `/data`. Se cambi piano o regione, crea prima un backup
  cifrato con `scripts/backup-db.sh`; non copiare il file SQLite live con un semplice `cp`.
- **Aggiornamenti**: ogni `git push` ricostruisce e ripubblica in automatico.

### Variante gratis (solo per provare)
Nel file `render.yaml`, nel servizio `paghe-api`, cambia `plan: starter` in `plan: free` e
**togli** il blocco `disk:`. Risparmi, ma: il backend si "addormenta" dopo un po' di inattività
(primo accesso lento) e **i dati si azzerano ad ogni riavvio**. Va bene per una demo, non per uso reale.

### Alternativa: Railway
[Railway](https://railway.com) è un'altra opzione semplice (volumi inclusi, niente "sleep").
Per le prove è disponibile una configurazione più semplice a servizio unico: compila frontend e
backend con `Dockerfile.railway`, usa il volume `/data` e non richiede CORS o `VITE_API_URL`.
La procedura completa è in [`docs/DEPLOY-RAILWAY.md`](docs/DEPLOY-RAILWAY.md).

### Domani: database PostgreSQL
Quando il software crescerà, conviene passare da SQLite a **PostgreSQL** (gestito, più robusto):
si cambia una riga in [`backend/src/Paghe.Api/Program.cs`](backend/src/Paghe.Api/Program.cs#L26)
(`UseSqlite` → `UseNpgsql`), si aggiunge il pacchetto `Npgsql.EntityFrameworkCore.PostgreSQL`,
e si imposta `ConnectionStrings__Default` con l'indirizzo del database Postgres. Per ora **non serve**.
