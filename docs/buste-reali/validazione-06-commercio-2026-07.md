# Prospetto paga di simulazione – non costituisce LUL — luglio 2026

## Revisione prevalente del 25 agosto 2026 — Officina Nord / Giulia Bianchi

> Questa sezione **sostituisce i valori e le conclusioni delle revisioni storiche riportate
> più sotto**. Il documento non è un cedolino validato e non autorizza un netto definitivo.

Il caso corrente usa assunzione 01/09/2022 e un solo scatto, con decorrenza 01/10/2025.
La retribuzione fissa oggetto della prova è:

| Componente | Importo |
|---|---:|
| Paga base livello III | 1.453,94 € |
| Contingenza comprensiva di EDR | 527,90 € |
| EDR separato | 0,00 € |
| Scatto di anzianità | 21,95 € |
| Terzo elemento Milano, solo con fonte territoriale documentata | 11,36 € |
| **Totale fisso di simulazione** | **2.015,15 €** |
| Tariffa oraria effettivamente usata | **11,9949 €** |
| Tariffa giornaliera effettivamente usata | **77,5058 €** |

La quattordicesima non viene sommata al prospetto ordinario: luglio contiene soltanto il
riferimento al prospetto separato e alla data di pagamento del 1° luglio. Con la base sopra,
il TFR calcolato e arrotondato per singolo codice paga è **162,40 €**. Per l'anzianità del caso,
al 31 luglio risultano separatamente **ROL 16,33 ore** ed **ex festività 18,67 ore**, totale
**35,00 ore** fino a 15 dipendenti; oltre 15 dipendenti sono **21,00 + 18,67 = 39,67 ore**.

### Stato dei dodici rilievi

| # | Controllo software | Stato |
|---:|---|---|
| 1 | H011 per autoriparatore non artigiano: ATECO, visura, stato Albo e verifica di applicabilità obbligatori | Implementato, **dati reali mancanti = blocco** |
| 2 | Componenti livello III, scatto, terzo elemento condizionato alla fonte, tariffe a precisione reale | Implementato |
| 3 | Quattordicesima del 1° luglio come prospetto separato documentato | Implementato, **riferimento/data mancanti = blocco** |
| 4 | Calendario completo, ore e progressivi settimanali; sovrapposizioni vietate | Implementato fail-closed |
| 5 | Malattia con protocollo/date, RMG precedente, ratei, lordizzazione e dettaglio giornaliero; INPS recuperabile separata | Implementato; UniEmens rifiuta dati giornalieri incompleti |
| 6 | Infortunio: evento 100%, tre giorni 60%, INAIL dal quinto giorno | Implementato |
| 7 | Aliquote da profilo CSC/CA/organico/FIS/agevolazioni e relativa fonte; H011 non classifica la posizione INPS | Implementato, **profilo reale mancante = blocco** |
| 8 | Addizionali soltanto da conguaglio annuale e domicilio fiscale | Implementato, **domicilio/saldi mancanti = blocco** |
| 9 | Ferie, ROL ed ex festività separati con precedente/maturato/goduto/residuo | Implementato |
| 10 | Fondo Est, ente bilaterale, TFR per codice e costo al netto delle prestazioni recuperabili | Implementato; costi chiamati **parziali stimati** |
| 11 | Checksum di CF e P.IVA | Implementato sia al salvataggio sia nel gate |
| 12 | Tariffe/aliquote mostrate con la precisione realmente utilizzata | Implementato e riconciliato nei test PDF |

**Esito corrente: NON VALIDABILE.** Fino all'acquisizione dei documenti e progressivi reali,
ogni PDF mantiene sempre il titolo «PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL» e
l'avviso «BOZZA / NON CERTIFICATO», anche se lo stato tecnico del workflow è “chiusa”.

---

## Revisione storica del 24 agosto 2026 — non usare per il caso corrente

> **A chi legge**: questa busta è stata calcolata da PaghePro il 24 agosto 2026 e **rivista
> riga per riga il giorno stesso**. La colonna «Consulente» e le risposte alle domande
> riportano il giudizio ricevuto. Le regole confermate diventano test automatici; le
> correzioni richieste sono elencate in fondo con lo stato di implementazione.

## Dati di partenza

| Dato | Valore |
|---|---|
| Azienda | ALFA COMMERCIO SRL — commercio/servizi, ATECO 47.91.10, Milano |
| CCNL | Terziario Distribuzione e Servizi (Confcommercio) — codice CNEL H011 |
| Fonte tabelle | CNEL CCNL 242377, minimi dal 01/11/2025; contingenza ed EDR sono inclusi nel minimo tabellare |
| Livello | 3 |
| Qualifica | Impiegato, tempo pieno, tempo indeterminato |
| Orario | 40 ore settimanali |
| Mensilità | 14 |
| Assunzione | 01/01/2020 |
| Scatti di anzianità | 0 *(dato errato: vedi sotto)* |
| Superminimo | 0,00 € |
| Regione / Comune | Lombardia / Milano (F205) |
| Periodo | luglio 2026, **mese intero senza assenze, senza straordinari** |

**Errore già nei dati di partenza**: «Scatti di anzianità: 0» è incompatibile con l'assunzione
del 01/01/2020. A luglio 2026 gli scatti maturati sono **2** (decorrenze 01/01/2023 e
01/01/2026): il software deve maturarli da solo dalla data di assunzione, non riceverli come
numero digitato.

## Le righe della busta

| # | Voce | Come è stato calcolato | PaghePro | Consulente |
|---|---|---|---|---|
| 1 | Minimo tabellare | tabella CCNL livello 3 (contingenza ed EDR inclusi) | **1.981,84** | Totale giusto, **esposizione sbagliata**: è paga base 1.453,94 + contingenza 527,90 (EDR incluso nella contingenza). Le voci devono restare distinguibili nel prospetto (Accordo retributivo H011, Testo Unico CCNL 2026, L. 4/1953) |
| 2 | Totale competenze | solo la voce 1 | **1.981,84** | **Incompleto per Milano**: mancano il terzo elemento provinciale 11,36 e 2 scatti × 21,95 = 43,90 → minimo fisso corretto **2.037,10** (col terzo elemento nazionale 2,07 sarebbe 2.027,81). Conta l'**unità operativa**, non la sede legale (tabella territoriale FILCAMS). Manca inoltre la **quattordicesima**: il CCNL la paga il 1° luglio sulla retribuzione al 30/6 (ammesso un cedolino separato) |
| 3 | Imponibile previdenziale | totale competenze | **1.981,84** | Segue il lordo: da rifare sul totale corretto |
| 4 | Contributi c/dipendente | 9,19% di 1.981,84 | **182,13** | Aritmetica giusta, aliquota incompleta: 9,19% è la **sola quota IVS**. Va aggiunta la quota lavoratore FIS: 9,29% (FIS ridotto 0,30), 9,3567% (FIS 0,50), 9,4567% (FIS 0,80), ~9,7567% se applicabile anche la CIGS (regole INPS sul FIS, riduzione dal 2025) |
| 5 | Contributi c/azienda | 27,88% di 1.981,84 | **552,54** | **Non validato e probabilmente sbagliato**: nel profilo ordinario di riferimento del commercio la quota datoriale base è 28,98% prima del FIS (29,18%–29,5133% col FIS, oltre con CIGS). L'aliquota esatta richiede CSC, codici autorizzazione, media dipendenti, agevolazioni: **non cablare né 27,88% né una percentuale unica per CCNL** |
| 6 | Imponibile fiscale | 1.981,84 − 182,13 | **1.799,71** | Segue i contributi: da rifare |
| 7 | IRPEF lorda | scaglioni 2026 sull'annuo ricostruito | **413,93** | Il reddito annuo è proiettato su **12 mensilità: il contratto ne ha 14** |
| 8 | Detrazioni lavoro dipendente | detrazione del mese | **208,01** | Stessa proiezione errata su 12; manca inoltre l'**ulteriore detrazione del cuneo fiscale**: 1.000 €/anno nella fascia oltre 20.000 e fino a 32.000 € (Circ. AdE 4/E/2025) |
| 9 | IRPEF netta | 413,93 − 208,01 | **205,92** | Aritmetica corretta, fiscalmente non validabile: i due errori della riga 8 si muovono in direzioni opposte, serve il **calcolo progressivo gennaio–luglio**, non una cifra mensile fissa |
| 10 | Addizionale regionale | metodo mensile semplificato | **24,06** | Coerente **solo come simulazione**: il metodo vero è il conguaglio sul reddito annuale effettivo, trattenuto l'anno successivo fino a 11 rate (D.Lgs. 360/1998) |
| 11 | Addizionale comunale | Milano | **0,00** | **Non dimostrabile**: Milano applica 0,8% con esenzione solo fino a 23.000 €; servono reddito 2025 e residenza fiscale, più l'acconto del 30% in massimo 9 rate da marzo |
| 12 | Totale trattenute | somma | **412,11** | Da rifare di conseguenza |
| 13 | **Netto in busta** | 1.981,84 − 412,11 | **1.569,73** | Solo somma aritmetica: senza progressivi, quattordicesima, contributi completi e conguagli il netto reale non è determinabile |
| 14 | Quota TFR del mese | retribuzione ÷ 13,5, − 0,50% | **159,71** | **Formula confermata** (base comprensiva delle 14 mensilità, divisore 13,5, contributo 0,50%); la base però è incompleta: sul minimo Milano 2.037,10 la quota diventa ~164,16 (art. 2120 c.c.) |
| 15 | Costo azienda | competenze + c/azienda + TFR | **2.694,09** | Omette ratei di 13ª/14ª, contribuzione sui ratei, FIS, INAIL, Fondo Est, ente bilaterale. Già con le cifre stampate il costo medio è ~3.116,48; col lordo Milano e il 28,98% ~3.229,52, sempre prima di FIS/INAIL/fondi |
| 16 | Ferie maturate | rateo mensile | **15,17** | Non è il rateo del mese: è il **maturato gennaio–luglio** (26 gg × 7/12). Etichetta da correggere; il saldo reale richiede il goduto |
| 17 | Permessi ROL maturati | rateo mensile | **42,00** | Mancano le **32 ore di ex-festività**: il maturato combinato su 7 mesi è 51,33 h (aziende fino a 15 dipendenti) o 60,67 h (oltre) |

## Le domande che contano — risposte

| # | Domanda | Risposta |
|---|---|---|
| A | Minimo tabellare 1.981,84 e contingenza/EDR dentro il minimo? | Corretto **come somma**. EDR dentro la contingenza: sì. Contingenza fusa nella «paga base» stampata: **no** — voci distinte obbligatorie nel prospetto |
| B | INPS c/dipendente 9,19%? | **Incompleta**: è la sola quota IVS. Si aggiunge la quota lavoratore FIS secondo la dimensione aziendale (0,10 / 0,1667 / 0,2667) ed eventuale CIGS (0,30) |
| C | INPS c/azienda 27,88% (CSC 10101)? | **Sbagliata come valore unico**: riferimento ordinario 28,98% pre-FIS. Serve il profilo contributivo reale (CSC, CA, media dipendenti, agevolazioni); la matricola stampata non basta. Non cablare percentuali |
| D | Addizionali col metodo mensile semplificato? | Accettabile **solo come anteprima**. Il cedolino reale vuole il conguaglio: regionale trattenuta l'anno dopo fino a 11 rate; comunale saldo + acconto 30% in massimo 9 rate da marzo |
| E | Detrazione mensile 208,01 corretta? | **No**: proiezione su 12 mensilità con contratto a 14, e manca l'ulteriore detrazione cuneo (1.000 €/anno, fascia 20.000–32.000). Serve il progressivo |
| F | TFR: divisore 13,5 e 0,50%? | **Sì, formula confermata** sulla base stampata; la base era però incompleta |
| G | Ratei ferie 15,17 e ROL 42? | Ferie: 26 gg/anno giusti, ma 15,17 è il maturato di 7 mesi, non un rateo mensile. ROL: mancano le 32 h di ex-festività; il totale annuo dipende dalla dimensione aziendale |
| H | Costo azienda: manca qualcosa? | **Sì**: ratei 13ª/14ª con relativa contribuzione, FIS, INAIL, Fondo Est, ente bilaterale. E vanno distinti «costo del cedolino» e «costo medio aziendale annualizzato» |
| I | Ratei 13ª/14ª in busta ogni mese o alla corresponsione? | La revisione fissa il punto sostanziale: la **14ª si paga il 1° luglio** sulla retribuzione di fatto al 30/6 (cedolino separato ammesso). Sull'esposizione mensile dei ratei non si è pronunciata |

## Altri difetti segnalati dalla revisione

1. Ore, giorni lavorati e calendario presenze **vuoti**: vanno alimentati con presenze reali.
2. Mancano i **progressivi fiscali e previdenziali** (base del conguaglio).
3. Il CF `VRDLGI90A41F205N` è formalmente valido ma il giorno «41» identifica una **donna**:
   per «Luigi» l'anagrafica è incoerente. Serve un controllo di coerenza CF/sesso al salvataggio.
4. Distinguere nel software il «costo del cedolino» dal «costo medio aziendale annualizzato».

## Esito della validazione

- **Data**: 24 agosto 2026
- **Chi ha verificato**: il fondatore, dichiaratosi consulente in sessione (nessun numero di
  iscrizione all'albo fornito). Per indicazione della stessa revisione, la **validazione formale
  di LUL e UniEmens resta in capo al Consulente del Lavoro incaricato**.
- **Righe che coincidono**: 4, 9 e 14 (aritmetica corretta sulla base stampata), 10 (come sola
  anteprima dichiarata)
- **Righe da correggere**: 1, 2, 3, 5, 7, 8, 11, 13, 15, 16, 17 (dettaglio nelle tabelle sopra)
- **Esito**: ☐ tutto confermato ☑ **da rifare, con le correzioni indicate**

**Verdetto sul software**: l'avvertenza si può rimuovere **solo dalla tabella CCNL H011**, e
solo dopo aver implementato paga base, contingenza/EDR, terzo elemento, scatti e decorrenze.
La **dicitura generale di non certificazione resta** finché aliquote INPS, progressivi,
addizionali e presenze non sono alimentati con dati reali.

## Conseguenze nel software

Correzioni decise a partire da questa revisione. Stato al 24 agosto 2026, sera (tutte coperte
dai test in `RevisioneConsulenteCommercioTests`):

- [x] Tabella H011 scomposta: paga base 1.453,94 + contingenza (EDR incluso) 527,90, esposte
      come **voci separate** in busta e nella testata retributiva del PDF. La scomposizione è
      del solo livello 3 (l'unico fornito): per gli altri livelli servono i valori.
- [x] **Scatti di anzianità calcolati dalla data di assunzione** (trienni art. 209, max 10,
      decorrenza dal compimento: assunto 01/01/2020 → scatti dal 01/01/2023 e 01/01/2026).
      Il numero digitato resta solo per i CCNL senza periodicità dichiarata.
- [x] **Terzo elemento** per provincia dell'unità operativa (Milano 11,36; nazionale 2,07):
      tabella `CcnlTerzoElemento` per anno+provincia, campo `ProvinciaLavoro` sul rapporto
      (default: sede azienda), nessuna eredità da altre province.
- [x] Detrazione e addizionali proiettate sul reddito con le **mensilità contrattuali** (14).
      L'IRPEF lorda del mese resta a scaglioni ragguagliati (×12): è il metodo di legge,
      confermato al centesimo da 26 buste reali di 5 gestionali.
- [x] **Ulteriore detrazione cuneo fiscale** (1.000 €/anno, oltre 20.000 fino a 32.000, poi
      decrescente fino a 40.000) come parametri dell'anno su DB, applicata anche al
      conguaglio di dicembre.
- [x] **Quattordicesima erogata a luglio** (finestra 1/7→30/6) e tredicesima a dicembre,
      maturazione per dodicesimi dall'assunzione; tassate a parte senza detrazioni
      (art. 23 c.2 DPR 600/73).
- [x] **Ex-festività** nel monte permessi: 88 h/anno (≤15 dipendenti) o 104 h/anno (oltre).
      Il valore esposto è il **maturato progressivo**, non un rateo del mese.
- [x] Aliquota INPS c/datore: nessuna percentuale cablata; il **profilo contributivo
      dell'azienda** (`AliquotaInpsDatore`, es. 28,98% + FIS) si imposta nella scheda azienda
      e vince sulla tabella per qualifica, che resta un riferimento non certificato.
- [x] **Costo del mese** e **costo medio annualizzato** distinti in busta (formula del costo
      medio verificata sui due importi della revisione: 3.116,48 e 3.229,52).
- [x] Controllo di coerenza **CF/sesso** al salvataggio (25/08): campo sesso in anagrafica
      (M/F, facoltativo) rifiutato con messaggio chiaro se il giorno di nascita del codice
      fiscale dice il contrario; verificato anche quando cambia il solo CF.
- [x] **Addizionali a rate dal conguaglio** (25/08): caricando sul rapporto gli importi
      annui, la busta trattiene le rate vere (regionale e saldo comunale in 11 rate
      gennaio–novembre, acconto 30% in 9 rate da marzo, ultima rata a recupero); senza i
      dati resta l'anteprima, dichiarata come tale nella voce.
- [x] **Progressivi annui in busta** (25/08): imponibile previdenziale, imponibile IRPEF,
      IRPEF pagata e detrazioni (cuneo compreso) sommati dai mesi elaborati dell'anno
      (chiusi e storici pre-workflow, mai le bozze) e stampati nella fascia dedicata;
      congelati nello snapshot alla chiusura. Senza archivio la fascia resta onesta
      («dato non calcolato»).
- [ ] FIS/CIGS calcolati dalla dimensione aziendale: oggi si impostano come quote esplicite
      (lavoratore su `ContributiFondiSettore`, datore dentro il profilo), non dedotti.

### La busta rivista (stesso caso, valori corretti)

Rigenerata dopo le correzioni: `output/pdf/busta-rivista-commercio-2026-07.pdf`.
Scenario aggiornato: unità operativa Milano, azienda 6–15 dipendenti (FIS 0,50%: lavoratore
+0,1667%, datore 28,98%+0,3333%=29,3133%).

| Voce | Prima | Dopo |
|---|---|---|
| Paga base | (unica voce 1.981,84) | **1.453,94** |
| Contingenza (EDR incluso) | — | **527,90** |
| Terzo elemento (MI) | — | **11,36** |
| Scatti di anzianità (2) | — | **43,90** |
| **Minimo fisso** | 1.981,84 | **2.037,10** |
| Quattordicesima (luglio) | assente | **2.037,10** |
| Imponibile previdenziale | 1.981,84 | 4.074,20 |
| INPS c/dipendente | 182,13 (9,19%) | **381,21 (9,3567%)** |
| INPS c/azienda | 552,54 (27,88%) | **1.194,28 (29,3133%, profilo azienda)** |
| Detrazione lavoro dip. / ulteriore detrazione cuneo | 208,01 (unica casella) | **180,98 + 83,33** (caselle separate) |
| IRPEF netta | 205,92 | **585,08** (14ª tassata a parte) |
| Add. regionale / comunale | 24,06 / 0,00 | 29,66 / 0,00 (**MEF 2026: Milano senza comunale**) |
| Fondo Est (lavoratore / azienda) | — | **2,00 / 13,00** |
| EBiTer 0,05%/0,10% su base+contingenza, 14ª compresa | — | **1,98 / 3,96** |
| **Netto di luglio** | 1.569,73 | **3.074,27** (con la 14ª, al netto dei fondi) |
| TFR medio del mese (ratei inclusi) | 159,71 | **164,16** |
| Contributo solidarietà 10% su fondo sanitario c/azienda | — | **1,30** |
| Costo del mese / medio annualizzato (parziali) | 2.694,09 | **5.450,90 / 3.254,06** |
| Ferie / ROL maturati (gen–lug) | 15,17 / 42,00 | 15,17 / **51,33** |

Nota sull'addizionale comunale: il file ufficiale MEF 2026 non riporta un'addizionale
deliberata per Milano, quindi il software applica zero. La regola indicata dalla revisione
(0,8% con esenzione a soglia 23.000) è comunque implementata e testata: vale dove la
delibera la prevede.

## Seconda revisione (25 agosto 2026) — sulla busta rigenerata

La busta rivista è stata riesaminata. Verdetto: **tabella retributiva H011 Milano verificata**;
il cedolino completo non ancora. La dicitura «BOZZA DA VALIDARE» resta. Rilievi ed esito:

| # | Rilievo | Esito nel software |
|---|---|---|
| 1 | L'aliquota stampata «9,36%» non riproduce il calcolo (9,3567%): 9,36% di 4.074,20 farebbe 381,35, non 381,21 | **Corretto**: le voci INPS stampano l'aliquota con tutti i decimali significativi (9,3567%, 29,3133%) |
| 2 | I 264,31 di detrazioni sono 180,98 (art. 13) + 83,33 (cuneo), ma il PDF li fondeva nella casella «detrazione lavoro dipendente» lasciando vuota «ulteriore detrazione» | **Corretto**: caselle separate in archivio (colonna dedicata) e su tutti e tre gli stili PDF |
| 3 | Addizionale regionale 29,66 ancora simulata: il metodo vero è la rata del conguaglio 2025 (max 11 rate) | **Implementato il metodo a rate**: caricando sul rapporto l'importo annuo, la busta trattiene la rata vera (11 rate gennaio–novembre, ultima a recupero). Senza il dato resta l'anteprima, ora dichiarata nella voce |
| 4 | Addizionale comunale 0 non verificabile senza conguaglio reale (saldo + acconto 30% in 9 rate da marzo) | **Implementato**: saldo (11 rate) e acconto 30% (9 rate da marzo) come campi del rapporto; Milano resta a zero finché non c'è il dato reale |
| 5 | Mancano Fondo Est (2 € lavoratore + 13 € azienda dal 01/04/2025) ed EBiTer (~1,98/3,96 nel mese con 14ª) | **Implementati** come regole del CCNL: Fondo Est 2+13 €; EBiTer 0,05%+0,10% su paga base + contingenza, mensilità aggiuntive comprese (3.963,68 × 0,05% = 1,98 ✓). Aggiunti poi il **contributo di solidarietà 10%** sul fondo sanitario c/azienda (1,30) e il flag «azienda non aderente» che spegne le quote; gli elementi sostitutivi restano da gestire |
| 6 | «12 × 169,76 = 2.037,10» è sbagliato (12 × 169,76 = 2.037,12) | **Corretto**: la mensilità piena si stampa intera, senza base arrotondata; nel pro-rata la frazione sta in descrizione e quantità |
| 7 | Chiarire il metodo TFR: 164,16 è il medio mensile con i ratei, non il TFR sul pagato di luglio (281,42) | **Chiarito in etichetta**: «Quota TFR media del mese (ratei 13ª/14ª inclusi)» — un solo metodo, nessun doppio conteggio |
| 8 | Il costo omette ancora Fondo Est, EBiTer, solidarietà, INAIL: scrivere «parziale» | **Fatto**: i costi includono Fondo Est, EBiTer c/azienda e solidarietà 10% (5.450,90 / 3.254,06) e le etichette dicono «(parziale)»; resta dichiarato mancante l'INAIL |
| 9 | CF con giorno «41» = donna per un «Luigi»; «Cartellino acquisito a totali mensili» contraddittorio con le caselle ore/giorni vuote | **Corretti**: scenario con CF maschile valido (VRDLGI90A01F205J); con variabili tutte a zero il PDF dichiara «Nessuna variabile mensile registrata: ore e giorni lavorati non disponibili» |

Le aliquote contributive della busta restano valide **solo per l'ipotesi dichiarata**
(FIS pieno 0,50%, nessuna riduzione/agevolazione): CSC, codici autorizzazione e media
occupazionale servono a confermarle.

**Dati ancora bloccanti** (vanno caricati, non calcolati): presenze reali (giorni/ore,
calendario), comune di residenza, P.A.T. INAIL, data/prova di pagamento, saldi anni
precedenti di ferie e permessi (i campi di riporto esistono). I progressivi annui e il
campo sesso sono stati implementati il 25/08; i progressivi si popolano man mano che i
mesi vengono elaborati e chiusi.
