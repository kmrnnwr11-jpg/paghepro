# Piano: file telematico ufficiale AA7/10 e AA9/12 (apertura/variazione/cessazione P.IVA)

Richiesta del fondatore: oltre al PDF già compilato sul modello ufficiale, produrre il **file
telematico che l'Agenzia delle Entrate accetta davvero**, uguale a quello del software originale.

## Risposta breve: sì, si può — e sappiamo già come

L'Agenzia non richiede che il file sia prodotto dal *suo* software: richiede che il file rispetti
le **specifiche tecniche** (tracciato) e superi il **modulo di controllo ufficiale** (JAR SOGEI) —
lo stesso identico modulo usato dal Desktop Telematico. Un file che passa il controllo con esito
positivo È, a tutti gli effetti, "il file che fa il software originale".

Questa strada è **già stata percorsa con successo nel progetto per l'F24**: tracciato a lunghezza
fissa scritto da `TracciatoF24Ufficiale` e validato con `ControlliF24A0.jar` → **ESITO POSITIVO**,
`.dcm` prodotto, zero errori. Si replica lo stesso metodo.

## Asset già in casa (verificati il 15/07/2026)

Nel kit `~/ade-official-integration-kit/` (fuori repo):

| Asset | Stato |
|---|---|
| Specifiche tracciato AA7/10 inizio/variazione (58 pp.) e cessazione | ✅ presenti |
| Specifiche tracciato AA9/12 inizio/variazione (49 pp.) e cessazione | ✅ presenti |
| `Ctrl_AA7_AA9_AT6VA.jar` (controllo ufficiale SOGEI, firmato) | ✅ presente |
| `TabellaAssociazioniControlli.json` con entrypoint confermati | ✅ presente |
| Modelli PDF + istruzioni AA7/10 2025 e AA9/12 | ✅ presenti |
| Java sulla macchina (OpenJDK 25) | ✅ presente |

Entrypoint confermati dalla tabella ufficiale AdE (stesso JAR per tre codici fornitura:
**AT6VA** = attribuzione/inizio, **VR6VA** = variazione, **CE3VA** = cessazione):

- controllo: `finanze.anag.main.GestoreControlli`
- diagnostico: `finanze.anag.generale.StampaDiagnostici`

Nel repo già pronti e riusabili:

- `ModuloAa710` (dati + validazione) e `Aa710PdfUfficiale` (PDF) + archivio Persone;
- `RichiestaControlloUfficiale` / `ControlliUfficialiRunnerJava` (lancio JAR generico, provato con F24);
- `PraticaTelematica` (macchina a stati Bozza→Generata→ControllataOk→…→InviataManualmente);
- il precedente completo `TracciatoF24Ufficiale` come modello di scrittura record posizionali.

## Il tracciato (dalle specifiche)

Fornitura = sequenza di **record a lunghezza fissa di 3503 caratteri**:

- record **A** (testa fornitura), **B** (dati principali dichiarazione), **C** (altri luoghi
  attività, max 4), **D** (soggetti collegati, unico), **Z** (coda con contatori);
- campi **posizionali** (posizione/lunghezza/formato da tabella), numerici a destra con zeri,
  alfanumerici a sinistra con spazi, tutto MAIUSCOLO; ogni record chiude con 3 caratteri di
  controllo + CR/LF;
- vincolo trasmissione (byte 23 record A): CF del responsabile = CF del
  titolare/rappresentante per l'invio via Fisconline.

Stessa famiglia dell'F24 (che era a 1900 caratteri): nessuna novità concettuale.

## Fasi di lavoro

| Fase | Cosa | Verifica | Effort |
|---|---|---|---|
| 0 | Smoke test del JAR: lancio a vuoto e su file volutamente errato, lettura `.dgn` | il JAR gira e produce diagnostico | ½ g |
| 1 | `ModuloAa912` (persone fisiche): dati + validazione, gemello di `ModuloAa710`; riuso archivio Persone | test xUnit | 1 g |
| 2 | `TracciatoAnagraficoUfficiale` per **AA7/10**: record A/B/C/D/Z da specifiche (prima inizio/variazione, poi cessazione) | test su posizioni/lunghezze campo per campo | 2–3 g |
| 3 | Stesso tracciato per **AA9/12** (condivide A/Z e l'impianto) | test come sopra | 1–2 g |
| 4 | `ControlloAA7AA9` (riusa il runner Java; classi main sopra) e **iterazione fino a diagnostico pulito** su 6 casi di prova: AA7 e AA9 × inizio/variazione/cessazione | **criterio di accettazione: esito positivo del controllo ufficiale, `.dcm` prodotto, zero errori bloccanti** | 2–4 g |
| 5 | Persistenza pratica (stati `PraticaTelematica`) + endpoint `POST /api/moduli/aa7-10/telematico` e `/aa9-12/telematico` + pagina Moduli ufficiali: bottone «Genera file telematico (controllato)» accanto al PDF | end-to-end vivo | 1–2 g |
| 6 | PDF ufficiale AA9/12 compilato (come già fatto per AA7/10, overlay calibrato) — utile per la copia cartacea | resa visiva verificata | 1 g |

Totale stimato: **8–13 giornate** di lavoro agente, incrementali e ognuna verificabile da sola.
L'ordine consigliato è quello in tabella; la fase 6 è staccabile.

## Cosa resta (per onestà) FUORI da questo piano

1. **L'invio automatico non esiste per nessuno**: il file controllato si trasmette con il
   **Desktop Telematico** (utenza Entratel/Fisconline dell'utente) o tramite **intermediario
   abilitato**. Il software arriva fino al file controllato + istruzioni passo-passo. È lo
   stesso confine già documentato per l'F24.
2. **Chi può usare AA7/AA9 da soli**: professionisti/lavoratori autonomi (AA9/12), enti non
   tenuti al Registro Imprese (AA7/10), e in generale variazioni/cessazioni. Per le **nuove
   imprese iscritte al Registro Imprese** l'apertura IVA passa per legge da **ComUnica**
   (Camera di Commercio): quella parte resta sul modulo Pratiche ComUnica esistente
   (distinta pronta; invio via ComunicaStarweb/DIRE o intermediario; il web service
   InfoCamere è tracciato in `TODO-COMUNICA-CAMERA-INAIL-INPS.md` e i suoi XSD/WSDL non sono
   nel repo).
3. **Versionamento**: le specifiche cambiano con i provvedimenti AdE; le tabelle del tracciato
   vanno tenute per versione (stesso principio dei parametri normativi per anno).

## Perché l'Agenzia lo accetterà

Il criterio di accettazione di ogni fase 4 è esattamente il criterio dell'Agenzia: il **suo**
modulo di controllo, eseguito in locale, con esito positivo. Non c'è ambiguità né
interpretazione: o il diagnostico è pulito o non lo è. Con l'F24 questo metodo ha già portato a
un file accettato dal controllo ufficiale al primo colpo dopo le iterazioni locali.
