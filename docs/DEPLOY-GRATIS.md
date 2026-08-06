# Pubblicare PaghePro online a costo zero

Obiettivo: PaghePro raggiungibile da un indirizzo web pubblico, **senza pagare nulla**, con i dati
che restano anche quando il servizio si spegne.

Aggiornato al 2026-08-06.

## Come funziona questa configurazione

| Pezzo | Dove sta | Costo |
| ----- | -------- | ----- |
| Sito + API (un solo servizio) | Render, piano **Free**, costruito da `Dockerfile.railway` | 0 € |
| Database (i dati veri) | Supabase, piano **Free**, PostgreSQL in Europa | 0 € |
| Indirizzo web | `https://NOME.onrender.com`, fornito da Render | 0 € |

Il database sta **fuori** dal servizio web: è questo che rende possibile il piano gratuito. Il
piano Free di Render non ha disco persistente, quindi un database SQLite dentro il container
verrebbe cancellato a ogni riavvio. Con Supabase i dati sopravvivono.

Sito e API vivono sullo stesso indirizzo: non serve configurare CORS né `VITE_API_URL`.

## Cosa devi accettare del piano gratuito

Sono limiti reali, non opinioni. Vanno letti prima, non dopo.

- **Il sito si addormenta** dopo 15 minuti senza visite. Il primo accesso successivo impiega
  30–60 secondi. Poi torna veloce.
- **Nessuno fa i backup al posto tuo.** Né Render né Supabase, sui piani gratuiti. Il backup è
  responsabilità tua: procedura obbligatoria in fondo a questa guida.
- **Supabase mette in pausa il progetto** dopo 7 giorni senza nessuna richiesta. I dati non si
  perdono, ma devi riattivarlo a mano dal pannello. Se lo usi ogni settimana non succede.
- **Spazio database: 500 MB.** Migliaia di cedolini ci stanno; i PDF pesanti no.
- **Memoria del servizio: 512 MB.** Sufficiente per l'applicazione, ma senza margine per carichi
  grossi.

Quando questi limiti diventano stretti, la stessa configurazione si sposta su un piano a pagamento
cambiando solo il piano: nessuna modifica al codice.

## Passo 1 — Il database su Supabase

Segui [`GUIDA-SUPABASE.md`](GUIDA-SUPABASE.md), passi 1 e 2. In sintesi:

1. <https://supabase.com> → **New project**.
2. **Region: Frankfurt (eu-central-1)** — i dati dei dipendenti restano in UE.
3. Scegli una **Database Password** robusta e **salvala** in un gestore di password.
4. **Project Settings → Database → Connection string → Session pooler** (porta 5432).
   Non usare la Transaction pooler (6543): non crea correttamente le tabelle. Non usare la
   Direct connection: risolve solo su IPv6, che Render non instrada.
5. Componi la stringa in formato .NET e tienila da parte:

   ```text
   Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<LA-TUA-PASSWORD>;SSL Mode=Require;Trust Server Certificate=true
   ```

   Il prefisso `aws-N` e la regione vanno letti dal pannello, non indovinati: cambiano da progetto
   a progetto. Nel formato .NET la password va scritta **così com'è**: il `percent-encode` che
   Supabase suggerisce vale solo per il formato `postgresql://`. Se contiene `;`, racchiudila tra
   virgolette singole.

Al primo avvio l'applicazione crea da sola tutte le tabelle tramite le migrazioni EF.

## Passo 2 — Genera le due chiavi segrete

Sul tuo computer, nel Terminale:

```bash
openssl rand -base64 48   # → Jwt__Key
openssl rand -base64 32   # → PAGHE_DATA_KEY
```

Devono essere **due valori diversi**. Salvali nel gestore di password **prima** di incollarli su
Render.

`PAGHE_DATA_KEY` cifra IBAN, dati sanitari e altri campi sensibili. **Se la perdi, quei dati non
sono più recuperabili da nessuno** — nemmeno con il backup del database.

## Passo 3 — Il servizio web su Render

1. <https://render.com> → accedi con GitHub.
2. **New ▸ Web Service** → seleziona il repository `paghe-saas`.
3. Compila così:

   | Campo | Valore |
   | ----- | ------ |
   | Name | `paghepro` (diventa `https://paghepro.onrender.com`) |
   | Region | **Frankfurt** |
   | Branch | `main` |
   | Language / Runtime | **Docker** |
   | Dockerfile Path | `./Dockerfile.railway` |
   | Instance Type | **Free** |

   Il file si chiama `Dockerfile.railway` per motivi storici, ma non è legato a Railway: compila
   React e .NET e li serve insieme. Va bene anche su Render.

4. **Advanced ▸ Add Environment Variable** — inserisci queste voci:

   ```text
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__Default=<la stringa Supabase del Passo 1>
   Jwt__Key=<il valore da 48 byte del Passo 2>
   PAGHE_DATA_KEY=<il valore da 32 byte del Passo 2>
   Sdi__Provider=Simulato
   Ai__Provider=Spento
   DOTNET_gcServer=0
   Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning
   ```

   `DOTNET_gcServer=0` riduce il consumo di memoria: serve per stare nei 512 MB del piano Free.

5. **Health Check Path**: `/health`.
6. **Create Web Service**. La prima costruzione richiede 5–10 minuti.

## Passo 4 — Verifica che sia davvero online

Nell'ordine:

1. `https://TUO-NOME.onrender.com/health` → deve rispondere `{"stato":"ok"}`.
2. `https://TUO-NOME.onrender.com` → deve comparire PaghePro.
3. Su Supabase, **Table editor**: devono esserci le tabelle (`Aziende`, `Cedolini`, `Utenti`, …).
4. Crea un account, inserisci un'azienda di prova.
5. Su Render, **Manual Deploy ▸ Restart service**. Riapri il sito: l'azienda deve essere ancora lì.
   Se sparisce, il database non è collegato a Supabase — controlla `ConnectionStrings__Default`.

Il passo 5 è quello che conta: dimostra che i dati vivono fuori dal container.

## Passo 5 — Il backup (obbligatorio con dati reali)

Sui piani gratuiti nessuno fa backup al posto tuo. Con buste paga vere di clienti veri, questo
passo non è facoltativo.

> Sul piano **Supabase Pro** i backup giornalieri sono inclusi e automatici (il pannello mostra
> `LAST BACKUP`). Restano comunque utili una copia propria cifrata, perché vive fuori dal
> fornitore, e soprattutto la **prova di ripristino**: quella non la fa nessuno al posto tuo.

Una volta sola, prepara la chiave di cifratura del backup:

```bash
brew install age
age-keygen -o ~/paghe-backup-chiave.txt      # conserva questo file al sicuro e fuori dai backup
```

Poi, con la cadenza che decidi (**almeno una volta a settimana**):

```bash
cd "/Users/kmrnnwr/Downloads/software buste paghe"
export PAGHE_BACKUP_AGE_RECIPIENT="<la riga public key stampata da age-keygen>"
./scripts/backup-db.sh postgres env
```

Configura la connessione con `PGSERVICE`/`PGPASSFILE`, non con la password sulla riga di comando.
Dettagli in [`GDPR-CIFRATURA.md`](GDPR-CIFRATURA.md).

**Un backup mai ripristinato non è un backup.** Ogni tanto prova davvero il ripristino su un
database Supabase separato e vuoto.

## Passo 6 — Aggiornare il sito

Ogni modifica pubblicata è un `git push`:

```bash
cd "/Users/kmrnnwr/Downloads/software buste paghe"
git add .
git commit -m "descrizione della modifica"
git push
```

Render ricostruisce e ripubblica da solo in pochi minuti.

## Cosa non funziona in questa configurazione

Non sono guasti: sono funzioni che richiedono programmi installati sulla macchina, assenti nel
container.

- **OCR delle foto** (visura fotografata): richiede Tesseract. I PDF di testo funzionano.
- **Controlli ufficiali F24/AA7-AA9**: richiedono i JAR dell'Agenzia delle Entrate e Java.
- **Firma CNS/smart card**: richiede lettore e middleware PKCS#11 sulla macchina che firma.
- **Invio reale a SdI**: `Sdi__Provider=Simulato` prova il flusso senza inviare nulla. Gli esiti
  non hanno valore ufficiale.
- **AI**: spenta. Il prodotto è completo anche senza.

## Prima di usarlo con clienti veri

Vedi [`PRODUZIONE.md`](PRODUZIONE.md). I punti che restano aperti, in ordine di importanza:

1. **Validazione professionale.** Il motore del cedolino è marcato BOZZA DA VALIDARE: i valori
   vanno confrontati con un Consulente del Lavoro su buste reali prima di consegnarle a un cliente.
2. **GDPR.** Cifratura e registro attività sono fatti; restano retention/cancellazione, informativa
   privacy e nomina a responsabile del trattamento verso Render e Supabase.
3. **Backup provato.** Non basta farlo: va ripristinato almeno una volta per davvero.
