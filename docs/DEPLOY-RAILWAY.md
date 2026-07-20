# PaghePro su Railway — ambiente di prova

Questa configurazione pubblica **frontend e API nello stesso servizio Railway**. È pensata per
prove con dati inventati: non è ancora un ambiente certificato per cedolini o dati reali di
dipendenti.

## Architettura

- `Dockerfile.railway` compila React/Vite e .NET 10.
- ASP.NET serve la SPA e le API dallo stesso dominio: non serve configurare CORS o
  `VITE_API_URL`.
- Railway assegna la porta tramite `PORT`; il container ascolta su `0.0.0.0`.
- SQLite usa `/data/paghe.db`: va collegato un volume Railway con mount path `/data`.
- `railway.json` imposta build Docker, healthcheck `/health` e riavvio su errore.

## Creazione del servizio

1. Caricare su GitHub il ramo che contiene questa configurazione.
2. In Railway scegliere **New Project → Deploy from GitHub repo** e selezionare il repository.
3. Lasciare la root directory del servizio su `/`: Railway leggerà `railway.json`.
4. Aggiungere un volume al servizio con mount path esatto `/data`.
5. Inserire nelle **Variables**:

   ```text
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__Default=Data Source=/data/paghe.db
   Sdi__Provider=Simulato
   Ai__Provider=Spento
   Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning
   ```

6. Generare localmente due valori diversi e inserirli come variabili protette:

   ```bash
   openssl rand -base64 48   # copiare in: Jwt__Key
   openssl rand -base64 32   # copiare in: PAGHE_DATA_KEY
   ```

   `PAGHE_DATA_KEY` va conservata anche fuori da Railway in un gestore password: senza quella
   chiave i dati cifrati non sono recuperabili.

7. In **Settings → Networking** premere **Generate Domain**.

## Controllo dopo il deploy

1. Aprire `https://DOMINIO-RAILWAY/health`: deve rispondere `{"stato":"ok"}`.
2. Aprire il dominio senza `/health`: deve comparire PaghePro.
3. Creare un'azienda inventata, ricaricare la pagina e verificare che resti presente.
4. Riavviare il servizio e ricontrollare: se il volume è montato correttamente, i dati restano.

## Limiti dell'ambiente di prova

- Usare soltanto aziende e dipendenti fittizi.
- Non configurare SdI, firma, email, WhatsApp o provider AI con credenziali reali.
- `Sdi__Provider=Simulato` prova il flusso senza effettuare invii reali; gli esiti non hanno
  valore ufficiale.
- Non aumentare a più repliche: SQLite deve restare collegato a una sola istanza.
- Prima di dati reali servono migrazioni stabili, backup/ripristino provato, retention GDPR,
  monitoraggio e validazione professionale. Vedi `docs/PRODUZIONE.md`.

Documentazione Railway corrente: [ASP.NET Core](https://docs.railway.com/guides/aspnet-core),
[Dockerfile](https://docs.railway.com/builds/dockerfiles),
[healthcheck](https://docs.railway.com/deployments/healthchecks),
[volumi](https://docs.railway.com/volumes) e [prezzi](https://railway.com/pricing).
