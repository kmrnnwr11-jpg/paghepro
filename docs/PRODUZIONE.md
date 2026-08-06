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

✅ **Fatto.** Lo schema è gestito dalle **migrazioni EF Core versionate**, applicate all'avvio da
`SchemaDatabase.Applica` (`Paghe.Persistence`). Non esiste più DDL scritto a mano: `EnsureCreated()`,
i blocchi `CREATE TABLE` in `Program.cs` e gli helper di compatibilità sono stati rimossi.

EF Core ammette un solo assembly di migrazioni per contesto, quindi ce n'è **uno per provider**:

| Provider | Progetto | Migrazione iniziale |
|---|---|---|
| SQLite | `src/Paghe.Migrazioni.Sqlite` | `20260801054909_Iniziale` |
| PostgreSQL | `src/Paghe.Migrazioni.Postgres` | `20260801054920_Iniziale` |

`Program.cs` sceglie l'assembly dalla stringa di connessione (`MigrationsAssembly`).

**Cosa succede all'avvio** (registrato nel log come `Schema database: ...`):

1. database inesistente o senza tabelle dell'applicazione → le migrazioni creano tutto;
2. database già gestito dalle migrazioni → applica quelle mancanti;
3. database creato *prima* delle migrazioni → viene **adottato**: colonne, tabelle e indici
   mancanti sono completati dalla migrazione iniziale, che viene poi registrata come applicata.
   I dati non vengono toccati e nulla viene cancellato (eventuali tabelle o indici di vecchie
   versioni del modello restano nel database, inutilizzati).

🔧 **Aggiungere una migrazione** dopo una modifica al modello — va fatto per **entrambi** i provider,
altrimenti l'altro resta indietro:
```bash
cd backend
ConnectionStrings__Default="Data Source=/tmp/paghe-design.db" \
  dotnet ef migrations add NomeModifica -p src/Paghe.Migrazioni.Sqlite -s src/Paghe.Api -o Migrazioni

Database__Provider=Postgres \
ConnectionStrings__Default="Host=localhost;Database=paghe_design;Username=postgres" \
  dotnet ef migrations add NomeModifica -p src/Paghe.Migrazioni.Postgres -s src/Paghe.Api -o Migrazioni
```
La stringa di connessione serve solo a far scegliere il provider: la generazione non apre il database.

🧑 **Applicare le migrazioni fuori dall'avvio** (deploy controllato):
```bash
ConnectionStrings__Default="$STRINGA_REALE" \
  dotnet ef database update -p src/Paghe.Migrazioni.Postgres -s src/Paghe.Api
```

> ⚠️ **Prima di aggiornare un database con dati veri**: backup verificato (sezione 3), una sola
> replica attiva durante l'aggiornamento, e revisione del SQL generato con
> `dotnet ef migrations script`.

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
