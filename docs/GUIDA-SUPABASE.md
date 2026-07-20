# Guida — collegare il backend a Supabase (database online)

Supabase è un **PostgreSQL nel cloud** (con piano gratuito). Collegandolo, i dati non stanno più in
un file locale ma in un database online, accessibile da più dispositivi e con backup gestiti.

> Il software **rileva da solo** se la stringa di connessione è SQLite o PostgreSQL/Supabase:
> non serve cambiare codice, basta impostare la **stringa di connessione** come variabile d'ambiente.

Aggiornato al 2026-06-28.

---

## Passo 1 — Crea il progetto Supabase
1. Vai su <https://supabase.com> → **Sign up** → **New project**.
2. Scegli una **Region** europea (es. *Frankfurt / eu-central-1*) — meglio per il GDPR.
3. Imposta una **Database Password** robusta e **salvala** (ti serve dopo).
4. Attendi che il progetto sia pronto (1–2 minuti).

## Passo 2 — Copia la stringa di connessione
1. Nel progetto: **Project Settings** (ingranaggio) → **Database**.
2. Sezione **Connection string** → scegli **Session pooler** (o **Direct connection**).
   - ⚠️ **Non** usare la *Transaction pooler* (porta 6543) per il primo avvio: non crea bene le tabelle.
3. Prendi i dati e componi la stringa in **formato .NET (Npgsql)**:

```
Host=<HOST>;Port=5432;Database=postgres;Username=<USERNAME>;Password=<LA-TUA-PASSWORD>;SSL Mode=Require;Trust Server Certificate=true
```

- `HOST`: tipo `aws-0-eu-central-1.pooler.supabase.com` (pooler) oppure `db.<ref>.supabase.co` (direct).
- `USERNAME`: con il pooler è tipo `postgres.<project-ref>`; con la direct connection è `postgres`.
- `SSL Mode=Require;Trust Server Certificate=true` è **obbligatorio** (Supabase richiede SSL).

## Passo 3 — Avvia il backend puntando a Supabase
Le credenziali **non vanno nel codice**: si passano come **variabili d'ambiente**.

```bash
cd backend

export ConnectionStrings__Default="Host=...;Port=5432;Database=postgres;Username=postgres.xxx;Password=...;SSL Mode=Require;Trust Server Certificate=true"
export PAGHE_DATA_KEY="$(openssl rand -base64 32)"
export Jwt__Key="almeno-32-caratteri-di-chiave-segreta-jwt"

# Solo per un database esistente/legacy: backup prima, poi cutover one-shot.
dotnet run --project src/Paghe.Api -c Release --no-launch-profile -- --migra-dati-sensibili
# Verifica strict (nessuna colonna in chiaro, chiave/sentinella valide).
dotnet run --project src/Paghe.Api -c Release --no-launch-profile -- --verifica-dati-sensibili
# Avvio normale.
dotnet run --project src/Paghe.Api -c Release --no-launch-profile
```

Al primo avvio il software **crea automaticamente tutte le tabelle** su Supabase.

> Se per qualche motivo non rileva Postgres, forzalo: `export Database__Provider=Postgres`.
> Conserva `PAGHE_DATA_KEY` nel secret manager e separata dai backup. Per rotazione e ripristino
> segui `docs/GDPR-CIFRATURA.md`; perderla rende i dati protetti non recuperabili.

### Metodo pulito consigliato in locale: `dotnet user-secrets`
Tiene la stringa (con la password) in un file **fuori dal progetto** (`~/.microsoft/usersecrets/…`),
letto in automatico **in sviluppo** — non finisce mai nel codice né nei commit.

```bash
cd backend
# (una tantum) imposta la connection string come secret locale:
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=aws-0-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<LA-TUA-PASSWORD>;SSL Mode=Require;Trust Server Certificate=true" \
  --project src/Paghe.Api

# avvia (il profilo di sviluppo carica i user-secrets e si collega a Supabase):
dotnet run --project src/Paghe.Api -c Release
```
- Vedi cosa hai salvato: `dotnet user-secrets list --project src/Paghe.Api`
- Rimuovi: `dotnet user-secrets remove "ConnectionStrings:Default" --project src/Paghe.Api`
- ⚠️ I user-secrets valgono **solo in sviluppo**. In **produzione** usa le variabili d'ambiente/secret
  dell'host (vedi Passo 5).

## Passo 4 — Verifica che funzioni
- Su Supabase: **Table editor** → devi vedere le tabelle (`Aziende`, `Cedolini`, `Utenti`, …).
- Oppure apri <http://localhost:5171/swagger> e prova un endpoint.
- Oppure lancia la prova completa: `python3 scripts/prova-buste-paga.py` (vedi
  [GUIDA-BUSTE-PAGA.md](GUIDA-BUSTE-PAGA.md)): i dati finiranno su Supabase.

## Passo 5 — In produzione (quando vai online)
- Imposta le stesse 3 variabili (`ConnectionStrings__Default`, `PAGHE_DATA_KEY`, `Jwt__Key`) come
  **secret** sull'host (Render, ecc.), **mai** nel codice o su Git.
- In ambiente `Production` il software **si rifiuta di partire** con le chiavi di sviluppo: è una
  protezione voluta (i dati sensibili non vengono mai cifrati con una chiave nota).

---

## Note importanti (oneste)
- **Piano gratuito Supabase**: il progetto va in **pausa** dopo un periodo di inattività e ha limiti
  di spazio/connessioni. Per uso reale valuta un piano a pagamento.
- **Backup**: Supabase fa backup automatici (secondo il piano), ma tieni comunque una copia tua
  cifrata con `scripts/backup-db.sh postgres env ...`. Configura la connessione tramite
  `PGSERVICE`/`PGPASSFILE` (non mettere password nella riga di comando) e il recipient pubblico
  `PAGHE_BACKUP_AGE_RECIPIENT`; prova periodicamente decrypt + `pg_restore` su un DB isolato.
- **GDPR**: scegli regione UE; gli IBAN sono già **cifrati a riposo** dal software (vedi
  `PAGHE_DATA_KEY`), e ogni modifica è tracciata nell'**audit log**.
- **Alternativa**: se preferisci la massima semplicità senza account, il software funziona già con
  **SQLite** (un file `paghe.db`) senza impostare nulla — vedi `docs/DEPLOY.md`.

> Tecnicamente: la scelta del provider è in `backend/src/Paghe.Api/Program.cs` (rileva `Data Source=`
> → SQLite, altrimenti `Host=/Server=/postgres` → Npgsql). Pacchetto:
> `Npgsql.EntityFrameworkCore.PostgreSQL`.
