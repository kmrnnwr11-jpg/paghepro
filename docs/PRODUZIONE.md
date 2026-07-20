# Produzione — checklist e procedure (Fase 2)

> Cosa serve perché il software regga clienti veri. Vedi `PIANO.md` Fase 2.
> Legenda: 🔧 fatto nel codice · 🧑 azione tua/operativa.

## 1. Segreti (NON nel codice)

In produzione i segreti vanno passati come **variabili d'ambiente**, mai scritti nei file.

- 🔧 **Chiave JWT**: in ambiente `Production` il software **rifiuta** la chiave di sviluppo e
  le chiavi < 32 byte (errore esplicito all'avvio). Vedi `TokenService.ChiaveJwt`.
- 🧑 Imposta una chiave robusta:
  ```bash
  export ASPNETCORE_ENVIRONMENT=Production
  export Jwt__Key="$(openssl rand -base64 48)"   # >= 32 byte
  ```
- 🔧 **Chiave dati GDPR**: Production, Staging, QA e ogni ambiente non esplicitamente locale/test
  rifiutano la chiave demo. Il secret deve essere Base64 di esattamente 32 byte.
- 🧑 Genera e salva `PAGHE_DATA_KEY` nel secret manager, separato dai backup:
  ```bash
  export PAGHE_DATA_KEY="$(openssl rand -base64 32)"
  ```
- 🧑 **Stringa di connessione** del database via variabile d'ambiente:
  ```bash
  export ConnectionStrings__Default="Data Source=/dati/paghe.db"   # SQLite
  # oppure PostgreSQL:
  # export ConnectionStrings__Default="Host=...;Database=paghe;Username=...;Password=..."
  ```
  (la doppia underscore `__` mappa la sezione di configurazione `:`)

## 2. Database e migrazioni EF

Oggi in sviluppo lo schema si crea con `EnsureCreated()` (+ patch SQL per SQLite). In produzione
si usano le **migrazioni EF Core**, già predisposte:

- 🔧 Factory di design-time pronta: `PagheDbContextFactory` (`Paghe.Persistence`).
- 🔧 Pacchetti EF di tooling aggiunti; `dotnet ef` 10 verificato funzionante.

🧑 **Generare la prima migrazione** (da fare **quando lo schema è stabile**, vedi nota):
```bash
cd backend
dotnet ef migrations add InitialCreate \
  --project src/Paghe.Persistence \
  --startup-project src/Paghe.Api \
  -o Migrations
```
🧑 **Applicare le migrazioni** (in deploy):
```bash
dotnet ef database update --project src/Paghe.Persistence --startup-project src/Paghe.Api
```
Poi sostituire in `Program.cs`, in ambiente Production, `EnsureCreated()` con `db.Database.Migrate()`
(in sviluppo si può mantenere `EnsureCreated()`).

> ⚠️ **Perché non è già generata:** il modello dati è in evoluzione attiva (più moduli in
> sviluppo in parallelo). Generare lo snapshot ora produrrebbe migrazioni subito superate e
> conflitti. Va fatto **una volta** quando l'insieme delle entità è stabile: è un solo comando.

## 3. Backup cifrati

- 🔧 Script `scripts/backup-db.sh` (SQLite e PostgreSQL), cifratura `age`, permessi `0600`,
  output atomico e rotazione degli ultimi 14 file. Se chiave pubblica o `age` mancano, non crea
  alcuna copia in chiaro.
- 🧑 Una sola volta, su una macchina amministrativa protetta, genera la coppia backup:
  ```bash
  age-keygen -o /percorso-sicuro/paghe-backup-identity.txt
  export PAGHE_BACKUP_AGE_RECIPIENT="$(age-keygen -y /percorso-sicuro/paghe-backup-identity.txt)"
  ```
  La identity privata va custodita separatamente dal server e provata; sul server si configura
  soltanto il recipient pubblico.
- 🧑 Pianifica un backup notturno (cron):
  ```bash
  0 2 * * * PAGHE_BACKUP_AGE_RECIPIENT=age1... /percorso/scripts/backup-db.sh sqlite /dati/paghe.db /backup
  ```
- 🧑 Conserva i backup **fuori** dal server. Prova il ripristino SQLite su una cartella isolata:
  ```bash
  set -euo pipefail
  umask 077
  RESTORE_DIR="$(mktemp -d /tmp/paghe-restore-test.XXXXXX)"
  trap 'rm -rf "$RESTORE_DIR"' EXIT
  age --decrypt -i /percorso-sicuro/paghe-backup-identity.txt \
    /backup/paghe-DATA.db.gz.age | gzip -dc > "$RESTORE_DIR/paghe.db"
  chmod 600 "$RESTORE_DIR/paghe.db"
  test -s "$RESTORE_DIR/paghe.db"
  test "$(sqlite3 "$RESTORE_DIR/paghe.db" 'PRAGMA integrity_check;')" = "ok"
  test "$(sqlite3 "$RESTORE_DIR/paghe.db" \
    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='MetadatiProtezioneDati';')" = "1"
  cd /percorso/backend
  ConnectionStrings__Default="Data Source=$RESTORE_DIR/paghe.db" \
    Database__Provider=Sqlite \
    dotnet run --project src/Paghe.Api -c Release -- --verifica-dati-sensibili
  ```
  Il comando finale è il gate applicativo read-only: usa `PAGHE_DATA_KEY` recuperata dal secret
  manager e deve terminare con codice `0`. Un file SQLite vuoto può rispondere `ok` al solo
  `PRAGMA integrity_check`, quindi i tre controlli non vanno separati o ignorati.
  Per PostgreSQL: `age --decrypt ... | pg_restore --exit-on-error --dbname DB_DI_PROVA`;
  il relativo `PGPASSFILE` deve avere permessi `0600`.

## 4. Deploy

- 🧑 Esegui dietro **HTTPS** (reverse proxy o piattaforma con TLS).
- 🧑 Restringi il CORS agli origin reali del frontend (oggi è aperto per lo sviluppo).
- 🧑 Imposta `ASPNETCORE_ENVIRONMENT=Production`.
- Il repo include già `Dockerfile`, `render.yaml` e `DEPLOY.md` come base.

## 5. Osservabilità

- 🔧 Endpoint `GET /health` (stato di base) già presente.
- 🧑 Raccogli i log dell'applicazione (file o servizio) e tieni d'occhio gli errori.

## 6. Checklist rapida prima del primo cliente

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `Jwt__Key` robusta impostata (l'avvio fallisce se manca: è voluto)
- [ ] `PAGHE_DATA_KEY` casuale impostata e copia di recovery custodita separatamente
- [ ] `ConnectionStrings__Default` impostata
- [ ] Migrazioni generate e applicate (a schema stabile)
- [ ] Recipient `age` configurato; backup cifrato notturno e ripristino testato
- [ ] HTTPS attivo e CORS ristretto
- [ ] Documenti `docs/commerciale/` (disclaimer, ToS) pubblicati
