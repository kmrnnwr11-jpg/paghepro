# Backend — Motore di calcolo cedolino (.NET 10 / C#)

Implementazione del **motore di calcolo della busta paga** (lordo → netto) sul CCNL
Commercio, con parametri normativi 2026 di esempio, test automatici e una demo console.

> ⚠️ I valori in [`Seed.cs`](src/Paghe.Core/Parametri/Seed.cs) (minimi tabellari, aliquote
> INPS, scaglioni IRPEF, addizionali) sono **ESEMPI ILLUSTRATIVI** a scopo di sviluppo.
> Vanno verificati e aggiornati con un Consulente del Lavoro prima di qualsiasi uso reale.

## Struttura

```text
backend/
├── Paghe.slnx
├── src/
│   ├── Paghe.Core/                 # Libreria di dominio + motore di calcolo
│   │   ├── Domain/                 # RapportoLavoro, Cartellino, Cedolino, enums
│   │   ├── Parametri/              # Ccnl, ParametriNormativi, Seed (dati 2026 esempio)
│   │   ├── Calcolo/                # MotoreCalcoloCedolino, IRPEF, contributi, TFR, stampa
│   │   ├── Fatturazione/           # Fattura, IVA, Spese, generatore FatturaPA XML
│   │   └── Contabilita/            # Bilancio SRL: riclassificazione CEE (SP + CE)
│   ├── Paghe.Persistence/          # EF Core: DbContext, entità, servizi (DB)
│   ├── Paghe.Documenti/            # Generazione PDF cedolini/fatture
│   ├── Paghe.Api/                  # API REST (ASP.NET Core) che espone il motore
│   └── Paghe.Demo/                 # Programma console che stampa cedolini di esempio
└── tests/
    └── Paghe.Tests/                # Test xUnit (74 test: calcoli, DB, PDF, validazione XSD)
```

## Comandi

```bash
cd backend

# Compila
dotnet build Paghe.slnx -c Release

# Esegui i test (74 test)
dotnet test Paghe.slnx -c Release

# Esegui la demo (stampa due cedolini)
dotnet run --project src/Paghe.Demo -c Release

# Avvia l'API REST (di default su http://localhost:5171)
dotnet run --project src/Paghe.Api -c Release
```

## API REST

Avviata l'API (`dotnet run --project src/Paghe.Api`) c'è anche la **documentazione
interattiva Swagger** su `http://localhost:5171/swagger` (spec OpenAPI su
`/swagger/v1/swagger.json`), da cui provare tutti gli endpoint dal browser.

- `GET  /` — informazioni sul servizio ed elenco endpoint
- `GET  /health` — health check
- `POST /api/auth/registra` — crea un account (tenant) e restituisce un token JWT
- `POST /api/auth/login` — autenticazione, restituisce un token JWT
- `GET  /api/ccnl/{codice}/livelli` — livelli e minimi tabellari di un CCNL
- `POST /api/cedolini/calcola` — calcola un cedolino (lordo → netto)
- `POST /api/cedolini/pdf` — genera il PDF del cedolino calcolato
- `POST /api/fatture/calcola` — calcola imponibili, IVA e totali di una fattura
- `POST /api/fatture/xml` — genera il tracciato FatturaPA XML (subset FPR12)
- `POST /api/fatture/valida` — valida l'XML FatturaPA contro lo **schema XSD ufficiale**
- `POST /api/fatture/pdf` — genera un PDF di cortesia della fattura
- `POST /api/bilancio/riclassifica` — bilancio CEE (Stato Patrimoniale + Conto Economico)
- `POST /api/contabilita/iva/liquidazione` — liquidazione IVA vendite/acquisti
- `POST /api/aziende` — crea un'anagrafica azienda (persistita)
- `GET  /api/aziende` — elenco aziende del tenant
- `POST /api/aziende/{id}/spese` — registra una fattura passiva/spesa IVA
- `GET  /api/aziende/{id}/spese` — registro IVA acquisti dell'azienda
- `GET  /api/spese/{id}` — dettaglio spesa
- `POST /api/aziende/{id}/liquidazioni-iva` — crea liquidazione IVA da fatture/spese persistite
- `GET  /api/aziende/{id}/liquidazioni-iva` — elenco liquidazioni IVA
- `GET  /api/liquidazioni-iva/{id}` — dettaglio liquidazione IVA con righe registro
- `POST /api/pratiche/apertura-partita-iva/simula` — checklist pratica senza salvarla
- `POST /api/pratiche/apertura-partita-iva` — salva pratica apertura P.IVA / ComUnica
- `GET  /api/pratiche/apertura-partita-iva` — elenco pratiche del tenant
- `GET  /api/pratiche/apertura-partita-iva/{id}` — dettaglio pratica
- `PATCH /api/pratiche/apertura-partita-iva/{praticaId}/adempimenti/{adempimentoId}` — aggiorna stato adempimento
- `POST /api/aziende/{id}/fatture` — salva una fattura (numero **automatico** se omesso)
- `GET  /api/aziende/{id}/fatture` — elenco fatture dell'azienda
- `GET  /api/fatture/{id}` — dettaglio fattura
- `GET  /api/fatture/{id}/pdf` — PDF di cortesia di una fattura persistita
- `POST /api/aziende/{id}/dipendenti` — crea dipendente + rapporto di lavoro
- `GET  /api/aziende/{id}/dipendenti` — elenco rapporti dell'azienda
- `POST /api/rapporti/{id}/cedolini` — calcola e **archivia** un cedolino
- `GET  /api/rapporti/{id}/cedolini` — cedolini archiviati del rapporto
- `GET  /api/cedolini/{id}` — dettaglio cedolino archiviato (con voci)

## Autenticazione

Gli endpoint che gestiscono i dati di un'azienda richiedono un **token JWT** (`Authorization:
Bearer <token>`). Il token si ottiene con `POST /api/auth/registra` (crea l'account) o
`POST /api/auth/login`. Il **tenant** è derivato dal claim `tenant_id` del token firmato — non
da un header — quindi un account non può accedere ai dati di un altro. Le password sono
salvate con PBKDF2 (`PasswordHasher`). La chiave di firma è in configurazione (`Jwt:Key`);
in produzione va impostata come secret. Gli endpoint di sola elaborazione (calcoli, validazione)
e Swagger restano pubblici.

```bash
# registra un account e usa subito il token
TOKEN=$(curl -s -X POST http://localhost:5171/api/auth/registra -H "Content-Type: application/json" \
  -d '{"ragioneSociale":"Studio Rossi","email":"mario@studio.it","password":"password1"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['token'])")
curl -s -X POST http://localhost:5171/api/aziende -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"ragioneSociale":"Alfa Srl","partitaIva":"01234567890","indirizzo":"Via Roma 1","cap":"20100","comune":"Milano","provincia":"MI"}'
```

## Documenti PDF

Il progetto `src/Paghe.Documenti` genera:

- PDF cedolino da un cedolino calcolato.
- PDF di cortesia per fatture, mentre l'originale fiscale resta il file XML FatturaPA.

La generazione usa QuestPDF. Prima dell'uso in produzione va verificata la licenza applicabile
al soggetto che commercializza il SaaS; vedi anche `../docs/GITHUB_REPOS_RIUSO.md`.

### Validazione FatturaPA (XSD ufficiale)

`FatturaPaValidator` valida l'XML contro lo **schema XSD ufficiale dell'Agenzia delle
Entrate** (`Schema_VFPR12`, v1.2.3), incorporato come risorsa insieme allo schema xmldsig
importato: la validazione è completamente **offline**. Restituisce l'elenco degli errori
(vuoto = conforme). Esempio di errore intercettato: provincia non `[A-Z]{2}`, CAP non
`[0-9]{5}`.

Oltre allo schema, `ControlliSdi` (in `Paghe.Core`) replica alcuni **controlli semantici
del Sistema di Interscambio** che fanno scartare fatture pur valide a schema: coerenza
Aliquota IVA / Natura (00400, 00401), identificativo del cessionario (00417), coerenza
dell'imposta nei riepiloghi (00422). L'endpoint `POST /api/fatture/valida` restituisce sia
`erroriXsd` sia `erroriSdi`; `valido` è true solo se entrambe le liste sono vuote.

Resta da aggiungere, per la trasmissione reale, la **firma digitale** CAdES/XAdES del file.

### Esempio: calcolo cedolino

```bash
curl -s -X POST http://localhost:5171/api/cedolini/calcola \
  -H "Content-Type: application/json" \
  -d '{
    "rapporto": {
      "codiceCcnl": "COMMERCIO_TERZIARIO",
      "livello": "3",
      "qualifica": "Impiegato",
      "numeroMensilita": 14,
      "scattiAnzianita": 2,
      "regione": "Lombardia",
      "codiceCatastaleComune": "F205"
    },
    "cartellino": { "anno": 2026, "mese": 6, "oreStraordinario": 8 }
  }'
```

Risposta (estratto): `"nettoInBusta": 1249.44`, `"costoAzienda": 1987.85`, con l'elenco
completo delle voci. Errori gestiti: livello CCNL inesistente → `400`, CCNL non
disponibile → `404`.

> La porta è definita in `src/Paghe.Api/Properties/launchSettings.json`.

## Persistenza (database)

Il layer dati è in `Paghe.Persistence` (EF Core, **provider-agnostico**):

- **Sviluppo / test**: SQLite (file `paghe.db` creato automaticamente con `EnsureCreated`).
  I test di integrazione girano su SQLite in-memory, quindi verificano la persistenza
  reale senza bisogno di un server.
- **Produzione**: PostgreSQL. Basta cambiare il provider nell'host:

  ```csharp
  // src/Paghe.Api/Program.cs
  builder.Services.AddDbContext<PagheDbContext>(o => o.UseNpgsql(connectionString));
  ```

  e impostare `ConnectionStrings:Default` (in produzione si usano le **migrazioni** EF
  al posto di `EnsureCreated`).

### Esempio: salvataggio fattura con numerazione automatica

```bash
T="11111111-1111-1111-1111-111111111111"
# crea l'azienda
AZ=$(curl -s -X POST http://localhost:5171/api/aziende -H "Content-Type: application/json" \
  -H "X-Tenant-Id: $T" \
  -d '{"ragioneSociale":"Alfa Srl","partitaIva":"01234567890","indirizzo":"Via Roma 1","cap":"20100","comune":"Milano","provincia":"MI"}')
AZID=$(echo "$AZ" | python3 -c "import sys,json;print(json.load(sys.stdin)['id'])")
# salva una fattura SENZA numero -> assegnato in automatico (es. "1/2026")
curl -s -X POST http://localhost:5171/api/aziende/$AZID/fatture -H "Content-Type: application/json" \
  -H "X-Tenant-Id: $T" \
  -d '{"data":"2026-06-26","cessionario":{"denominazione":"Beta Spa","partitaIva":"09876543210","indirizzo":"Via Po 2","cap":"10100","comune":"Torino","provincia":"TO"},"righe":[{"descrizione":"Consulenza","prezzoUnitario":100,"aliquotaIva":22}]}'
```

## Come funziona il motore

Flusso in [`MotoreCalcoloCedolino.Calcola`](src/Paghe.Core/Calcolo/MotoreCalcoloCedolino.cs):

1. **Retribuzione di base** dal livello CCNL: minimo tabellare + EDR + scatti + superminimo
   (con coefficiente part-time).
2. **Straordinari** dal cartellino, con maggiorazione (paga oraria = retribuzione / divisore).
3. **Contributi INPS** c/dipendente e c/datore sull'imponibile previdenziale.
4. **Imponibile fiscale** = imponibile previdenziale − contributi c/dipendente.
5. **IRPEF** con metodo presuntivo su base 12: imponibile annualizzato → scaglioni →
   detrazioni da lavoro dipendente → IRPEF netta del mese.
6. **Addizionali** regionale e comunale (rateazione semplificata su base 12).
7. **Netto in busta** = competenze − trattenute.
8. **TFR** (figurativo): quota mensile = retribuzione utile annua / 13,5, al netto dello 0,50%.

### Esempio d'uso da codice

```csharp
var parametri = Seed.Parametri2026();
var ccnl      = Seed.CcnlCommercio2026();
var motore    = new MotoreCalcoloCedolino(parametri, ccnl);

var rapporto = new RapportoLavoro
{
    CodiceCcnl = "COMMERCIO_TERZIARIO",
    Livello = "3",
    Qualifica = Qualifica.Impiegato,
    NumeroMensilita = 14,
    ScattiAnzianita = 2,
    Regione = "Lombardia",
    CodiceCatastaleComune = "F205", // Milano
};
var cartellino = new Cartellino { Anno = 2026, Mese = 6, OreStraordinario = 8m };

Cedolino cedolino = motore.Calcola(rapporto, cartellino);
Console.WriteLine(StampaCedolino.Testo(cedolino, "Mario Rossi"));
```

## Limiti noti (fuori dallo scope MVP)

- Conguaglio fiscale di fine anno e gestione separata delle mensilità aggiuntive (13ª/14ª).
- Rateazione effettiva delle addizionali (11 rate sull'anno successivo).
- Malattia/maternità con indennità INPS, infortunio INAIL.
- Cessazione del rapporto e liquidazione del TFR con tassazione separata.
- Multi-CCNL, apprendistato con agevolazioni, contratti a termine.

Questi punti sono il naturale lavoro delle fasi successive, sopra alle fondamenta qui poste.
