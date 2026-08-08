# Istruzioni per un'AI che deve usare PaghePro

Questo file è scritto per un assistente automatico (Claude, GPT o simili) che deve **operare** su
PaghePro tramite le sue API: creare aziende, assumere dipendenti, produrre buste paga, PDF, XML
UniEmens e F24.

Non è un manuale per persone. Il manuale umano è `docs/MANUALE-PAGHEPRO.md`.

Aggiornato al 2026-08-07. Percorsi e strutture verificati sull'istanza in produzione.

---

## 0. Regole non negoziabili

Leggile prima di qualunque chiamata. Violarle produce danni reali su dati di lavoratori veri.

1. **Non inventare dati normativi.** Se manca il CCNL, il livello, la matricola INPS o la P.A.T.
   INAIL, **chiedi all'utente**. Non dedurre il CCNL dal codice ATECO, non inventare un livello
   perché "sembra plausibile": cambiano l'importo della busta paga e le aliquote contributive.
2. **Non calcolare mai tu.** Gli importi li produce il motore del server. Non stimare un netto,
   non arrotondare, non "correggere" un risultato che ti sembra strano: riportalo e segnala il
   dubbio.
3. **Ogni busta paga prodotta è marcata BOZZA DA VALIDARE.** Dillo sempre all'utente quando
   consegni un risultato. Non presentarla come documento definitivo.
4. **Non trasmettere niente a INPS, Agenzia delle Entrate o SdI.** Gli XML e gli F24 generati sono
   bozze. L'invio reale passa da canali ufficiali e non è compito tuo.
5. **Un errore 400 con un messaggio in italiano è una regola di protezione, non un bug.** Leggi il
   messaggio e riferiscilo all'utente invece di aggirarlo (per esempio con `?forza=true`).

---

## 1. Collegamento e autenticazione

**Indirizzo base**: `https://paghepro.onrender.com`

### Intestazione obbligatoria

Ogni chiamata dal web deve portare:

```text
X-PaghePro-Client: web
```

Senza, la registrazione e il login rispondono `400 {"errore":"Client web non riconosciuto."}`.

### Ottenere il token

```http
POST /api/auth/login
Content-Type: application/json
X-PaghePro-Client: web

{"email":"...","password":"...","codice2fa":null}
```

Risposta: `{"token":"eyJ...", ...}`. Da qui in avanti ogni chiamata protetta vuole:

```text
Authorization: Bearer <token>
```

### Il token dura 15 minuti

È la trappola più comune. Un token scaduto fa rispondere **corpo vuoto o 401**, non un errore
parlante. Se una chiamata che prima funzionava smette di rispondere, **rifai il login** prima di
concludere che qualcosa è rotto.

### Isolamento

Il tenant (lo studio) è dentro il token. Non esiste modo di leggere i dati di un altro studio e non
devi cercarlo. Non c'è nessun header di tenant da impostare.

### Primo accesso lento

Il servizio è sul piano gratuito e si sospende dopo 15 minuti di inattività. La prima chiamata dopo
una pausa può impiegare **fino a 60 secondi**. Usa un timeout di almeno 90 secondi e **non
ritentare in parallelo**: sembrerebbe un errore ed è solo il risveglio.

---

## 2. La sequenza per produrre una busta paga

Questo è il percorso completo, nell'ordine obbligato. Ogni passo dipende dal precedente.

```text
azienda → dipendente+rapporto → cedolino → PDF
                                    ↓
                          UniEmens XML · F24 · CU
```

### Passo 1 — Azienda

```http
POST /api/aziende
```

```json
{
  "ragioneSociale": "Rossi Costruzioni Srl",
  "indirizzo": "Via Roma 1",
  "cap": "29017",
  "comune": "Fiorenzuola d'Arda",
  "provincia": "PC",
  "partitaIva": "12345678903",
  "codiceFiscale": null,
  "matricolaInps": null,
  "patInail": null,
  "ccnlDefault": null,
  "livelloDefault": null,
  "codiceCatastale": null
}
```

Obbligatori: `ragioneSociale`, `indirizzo`, `cap`, `comune`, `provincia`. Tutto il resto ha un
valore predefinito o è facoltativo.

Campi che **contano davvero** e che devi chiedere all'utente se mancano:

| Campo | A cosa serve | Se manca |
| --- | --- | --- |
| `matricolaInps` | UniEmens, F24 sezione INPS | Quei documenti sono bloccati dal server |
| `patInail` | Autoliquidazione INAIL | Autoliquidazione non producibile |
| `codiceCatastale` | Addizionali comunali IRPEF | Addizionale comunale a zero: busta paga **sbagliata** |
| `ccnlDefault`, `livelloDefault` | Precompilazione assunzioni | Vanno indicati per ogni dipendente |

I codici contributivi composti solo da zeri vengono rifiutati. È voluto.

Risposta: l'azienda creata, con `id` (GUID). **Conservalo**: serve a tutti i passi successivi.

Altri percorsi utili:

- `GET /api/aziende` — elenco
- `GET /api/aziende/{id}` — scheda
- `PATCH /api/aziende/{id}` — correzione parziale: manda **solo** i campi da cambiare
- `POST /api/aziende/da-visura` — crea da visura camerale (PDF o foto)

### Passo 2 — Dipendente e rapporto di lavoro

```http
POST /api/aziende/{aziendaId}/dipendenti
```

```json
{
  "codiceFiscale": "RSSMRA85M01H501Z",
  "nome": "Mario",
  "cognome": "Rossi",
  "codiceCcnl": "EDILIZIA_INDUSTRIA",
  "livello": "3",
  "qualifica": "Operaio",
  "dataAssunzione": "2025-01-15",
  "numeroMensilita": 14,
  "oreSettimanali": 40,
  "tipoContratto": "Tempo indeterminato",
  "partTime": false,
  "percentualePartTime": 100,
  "regione": "Emilia-Romagna",
  "codiceCatastaleComune": "D611",
  "detrazioniLavoroDipendente": true,
  "iban": null
}
```

Obbligatori: `codiceFiscale`, `nome`, `cognome`, `codiceCcnl`, `livello`.

Attenzione a questi:

- **`codiceCcnl` e `livello` devono esistere davvero.** Chiedili con `GET /api/ccnl` e
  `GET /api/ccnl/{codice}/livelli`. Non inventarli.
- **`percentualePartTime`**: se il rapporto è part-time e lasci 100, il lavoratore viene pagato
  come se fosse a tempo pieno. È l'errore più costoso di tutta l'anagrafica.
- **`regione` e `codiceCatastaleComune`** determinano le addizionali IRPEF. Vuoti = addizionali
  a zero.
- **`dataAssunzione`** in formato `AAAA-MM-GG`. Se manca, non potrai produrre buste paga coerenti.

Risposta: contiene `rapportoId` e `dipendenteId`. **Il `rapportoId` è la chiave della busta paga**,
non il `dipendenteId`.

Alternativa automatica dalla Comunicazione Obbligatoria:

- `POST /api/aziende/{aziendaId}/dipendenti/da-unilav` — XML UNILAV
- `POST /api/unilav/pdf` — PDF o foto del modulo
- `POST /api/unilav/testo` — testo incollato

Queste tre restituiscono anche `daCompletare[]`: l'elenco dei campi che il modulo non contiene
(qualifica contrattuale e residenza non sono deducibili dall'UNILAV). **Riporta quell'elenco
all'utente**, non ignorarlo.

Elenco dipendenti: `GET /api/aziende/{aziendaId}/dipendenti`.

### Passo 3 — Busta paga

```http
POST /api/rapporti/{rapportoId}/cedolini
```

Il corpo è il **cartellino** del mese:

```json
{
  "anno": 2026,
  "mese": 7,
  "oreStraordinario": 8,
  "oreStraordinarioNotturno": 0,
  "oreStraordinarioFestivo": 0,
  "giorniFerie": 2,
  "orePermessi": 0,
  "oreExFestivita": 0,
  "giorniMalattia": 0,
  "numeroEventoMalattiaAnno": 1,
  "giorniInfortunio": 0,
  "oreAssenzaNonRetribuita": 0
}
```

Obbligatori: `anno`, `mese`. Tutto il resto vale zero se omesso.

`numeroEventoMalattiaAnno` è il **progressivo dell'evento nell'anno solare** (1° episodio, 2°, 3°):
cambia le percentuali di indennità. Se c'è malattia, chiedilo.

Risposta:

```json
{
  "id": "30f31f1d-...",
  "anno": 2026, "mese": 7,
  "nettoInBusta": 1359.73,
  "costoAzienda": 2220.92,
  "ferieResidue": 13.17,
  "permessiResidui": 42.00
}
```

**Se `ferieResidue` o `permessiResidui` sono negativi**, non è un errore: il lavoratore ha goduto
ferie non ancora maturate. Segnalalo all'utente, non nasconderlo.

Altri percorsi:

- `GET /api/rapporti/{rapportoId}/cedolini` — buste archiviate
- `GET /api/cedolini/{id}` — dettaglio voci
- `GET /api/cedolini/{id}/pdf` — **PDF della busta** (`application/pdf`)
- `POST /api/cedolini/calcola` — calcolo senza archiviare (simulazione)

### Elementi retributivi agevolati (facoltativi, nel cartellino)

Somme esenti da imposte e contributi entro i limiti di legge. **Vanno indicate solo se realmente
spettanti.** Il software applica le soglie; non attesta che il presupposto esista.

```json
{
  "anno": 2026, "mese": 7,

  "giorniTrasfertaItalia": 5,
  "giorniTrasfertaEstero": 0,
  "indennitaTrasfertaGiornaliera": 46.48,
  "rimborsoTrasferta": "Forfettario",
  "rimborsiSpeseDocumentate": 0,

  "fringeBenefit": 0,
  "fringeBenefitGiaErogatoAnno": 0,
  "figliACarico": false,

  "giorniBuonoPasto": 20,
  "valoreBuonoPasto": 10,
  "buonoPastoElettronico": true,

  "premioRisultato": 0,
  "redditoAnnoPrecedente": 0
}
```

Limiti 2026 applicati dal motore:

| Elemento | Limite | Regola sull'eccedenza |
| --- | --- | --- |
| Trasferta Italia | 46,48 €/giorno | Solo l'eccedenza è imponibile |
| Trasferta estero | 77,47 €/giorno | Solo l'eccedenza è imponibile |
| Trasferta con rimborso misto | Soglia ridotta di 1/3 o 2/3 | `rimborsoTrasferta`: `MistoVittoOAlloggio` / `MistoVittoEAlloggio` |
| Fringe benefit | 1.000 €/anno (2.000 con figli a carico) | **Tutto o niente**: superata la soglia è imponibile l'intero importo |
| Buoni pasto elettronici | 10 €/giorno | Solo l'eccedenza |
| Buoni pasto cartacei | 4 €/giorno | Solo l'eccedenza |
| Premio di risultato | 5.000 €/anno, imposta sostitutiva 1% | Oltre il limite, o con reddito precedente > 80.000 €, tassazione ordinaria |

Il premio di risultato **resta soggetto a contributi INPS**: l'agevolazione sostituisce la sola
IRPEF. Richiede un contratto collettivo di secondo livello: se non c'è, non è agevolabile.

Nella risposta trovi `totaleEsenti`, `premioRisultatoAgevolato` e `impostaSostitutivaPremio`.

**Cosa devi rifiutare di fare.** Se l'utente ti chiede di inserire una trasferta non avvenuta, o
giorni di assenza non reali, per far uscire un certo importo o per ridurre le tasse: **non farlo e
spiega perché**. Una trasferta fittizia viene riqualificata come retribuzione, con recupero di
contributi e imposte, sanzioni e responsabilità penale. Le assenze inventate sono falsificazione
del cartellino. Le vie legittime per aumentare il netto sono quelle della tabella qui sopra, più
il welfare aziendale, e presuppongono tutte che il fatto sia reale e documentato.

### Calcolo inverso: netto voluto → lordo necessario

```http
POST /api/cedolini/lordo-per-netto
```

```json
{"rapporto": { ... }, "cartellino": {"anno": 2026, "mese": 7}, "nettoDesiderato": 1500}
```

Risposta:

```json
{
  "raggiungibile": true,
  "nettoDesiderato": 1500, "nettoOttenuto": 1500.00,
  "superminimoNecessario": 245.67,
  "lordoMensile": 1586.66, "costoAzienda": 2130.12,
  "nota": "Netto 1.500,00 € con un superminimo di 245,67 € al mese."
}
```

È una **simulazione**: non archivia nulla. Cerca il superminimo individuale che produce il netto
voluto, lasciando invariati CCNL, livello e cartellino.

Se `raggiungibile` è `false`, il netto richiesto è **sotto il minimo tabellare del CCNL**: la `nota`
spiega qual è il netto minimo contrattuale. Non è un limite tecnico da aggirare. Per un importo
inferiore le uniche strade legittime sono meno ore contrattuali (part-time) o assenze realmente
avvenute. Riporta la nota all'utente così com'è.

### Passo 4 — Adempimenti del mese

Tutti richiedono che le buste del periodo siano **già archiviate**.

```http
POST /api/aziende/{aziendaId}/uniemens/mese
POST /api/aziende/{aziendaId}/f24/mese
```

Stesso corpo per entrambi:

```json
{"anno": 2026, "mese": 7, "matricolaInps": "89000000103", "codiceSedeInps": null}
```

UniEmens risponde con `xml` più una sintesi. L'XML porta scritto
`versione="bozza-non-validata"`: **non è validato dal software di controllo INPS**. Riferiscilo.

F24 risponde con le sezioni (`erario`, `inps`, `regioni`, `tributiLocali`), i totali, una `bozza`
testuale e soprattutto:

```json
{"valido": false, "errori": [{"campo": "...", "messaggio": "..."}]}
```

**Se `valido` è `false`, non presentare l'F24 come pronto.** Elenca gli errori.

Per archiviare la pratica F24 con stato: `POST /api/aziende/{aziendaId}/f24/mese/salva`.

Certificazione Unica: `GET /api/rapporti/{rapportoId}/cu/{anno}/pdf` — stampata sul modello
ufficiale dell'Agenzia delle Entrate.

---

## 3. I blocchi del server e cosa significano

Sono protezioni volute dal proprietario del software. Quando le incontri, **spiegale**, non
aggirarle.

| Messaggio | Significato | Cosa fare |
| --- | --- | --- |
| Periodo futuro | Stai chiedendo una busta di un mese non ancora concluso | Usa un mese passato |
| Precedente all'assunzione | Il periodo è prima di `dataAssunzione` | Verifica la data con l'utente |
| Successivo alla cessazione | Il rapporto è chiuso | Controlla `dataCessazione` |
| Codice fiscale già assunto | Stesso CF già presente **nella stessa azienda** | Non ricrearlo: produrrebbe una doppia busta |
| F24 già inviato | Il mese ha una pratica F24 trasmessa | Ricalcolare falsa un versamento già fatto |
| Anagrafica contributiva provvisoria | Manca matricola INPS o P.A.T. INAIL | Chiedi i codici all'utente |

Sul quinto: esiste `?forza=true` per il ricalcolo. **Non usarlo di tua iniziativa.** Serve al
professionista che sa di dover poi rigenerare l'F24. Se pensi serva, chiedi.

Nota sul terzo: se lo stesso codice fiscale esiste in un'**altra** azienda dello stesso studio, il
sistema riusa l'anagrafica e crea solo il nuovo rapporto. È corretto: la stessa persona può avere
due datori di lavoro.

---

## 4. Convenzioni tecniche

- **Importi**: numeri JSON con decimali, mai stringhe. Il server usa `decimal`: non arrotondare né
  convertire in virgola mobile prima di rimandarli.
- **Date**: `AAAA-MM-GG`.
- **Enumerazioni**: stringhe leggibili (`"Impiegato"`, `"Tempo indeterminato"`), non numeri.
- **Identificatori**: GUID.
- **PATCH**: manda solo i campi da cambiare. Un campo assente resta com'è; un campo a `null` in
  genere **non** cancella (per le rimozioni esistono flag dedicati, es. `rimuoviDataCessazione`).

---

## 5. Cosa non funziona su questa istanza

Non sono guasti: richiedono programmi installati sulla macchina, assenti nel contenitore online.

| Funzione | Stato |
| --- | --- |
| OCR di foto (visura fotografata) | Non disponibile. I PDF di testo funzionano |
| Controlli ufficiali F24 / AA7-AA9 (JAR Agenzia Entrate) | Non disponibili |
| Firma con smart card / CNS | Richiede lettore e middleware locale |
| Invio reale a SdI | `Sdi__Provider=Simulato`: prova il flusso, non invia |
| Lettura documenti con AI | `Ai__Provider=Spento` |

Se l'utente chiede una di queste, **dillo subito** invece di provare e riferire un errore generico.

---

## 6. Esempio completo verificato

Sequenza eseguita realmente il 2026-08-06 sull'istanza in produzione, con i valori ottenuti.

```text
POST /api/auth/login                              → token
POST /api/demo/dataset                            → 5 aziende, 5 dipendenti
GET  /api/aziende                                 → azienda "DEMO Edil Casa Srl"
GET  /api/aziende/{id}/dipendenti                 → rapportoId di "Romano Paolo"
POST /api/rapporti/{rapportoId}/cedolini
     {"anno":2026,"mese":7,"oreStraordinario":8,"giorniFerie":2}
                                                  → netto 1359.73, costo azienda 2220.92
GET  /api/cedolini/{id}/pdf                       → PDF A4, 61 KB
POST /api/aziende/{id}/uniemens/mese              → XML bozza-non-validata
POST /api/aziende/{id}/f24/mese                   → totale 744.45 (IRPEF 107.69 +
                                                    INPS 609.97 + add. regionale 26.79)
```

Coerenza da verificare sempre: l'IRPEF e l'addizionale regionale dell'F24 devono coincidere al
centesimo con quelle della busta paga dello stesso periodo. Nell'esempio coincidono.

`POST /api/demo/dataset` funziona **solo** per account con email che finisce in `@paghe.local` ed è
pensato per le prove. Non usarlo su uno studio con dati reali.

---

## 7. Come riferire un risultato all'utente

Quando consegni una busta paga:

1. Netto in busta e costo azienda.
2. Il periodo e il nominativo, per evitare scambi di persona.
3. Ferie e permessi residui, **soprattutto se negativi**.
4. La frase: *"documento BOZZA DA VALIDARE, da sottoporre al Consulente del Lavoro"*.

Quando consegni un XML o un F24:

1. Se è una bozza non validata, dillo nella prima riga.
2. Se `valido` è `false`, elenca gli errori prima dei totali.
3. Non dire mai "pronto da inviare".
