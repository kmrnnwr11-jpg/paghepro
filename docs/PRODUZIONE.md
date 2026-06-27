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

## 3. Backup

- 🔧 Script `scripts/backup-db.sh` (SQLite e PostgreSQL), con rotazione (ultimi 14).
- 🧑 Pianifica un backup notturno (cron):
  ```bash
  0 2 * * * /percorso/scripts/backup-db.sh sqlite /dati/paghe.db /backup
  ```
- 🧑 Conserva i backup **fuori** dal server (storage esterno) e prova ogni tanto un ripristino.

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
- [ ] `ConnectionStrings__Default` impostata
- [ ] Migrazioni generate e applicate (a schema stabile)
- [ ] Backup notturno pianificato e ripristino testato
- [ ] HTTPS attivo e CORS ristretto
- [ ] Documenti `docs/commerciale/` (disclaimer, ToS) pubblicati
