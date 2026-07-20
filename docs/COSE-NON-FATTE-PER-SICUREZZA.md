# Cose che NON ho fatto (e non farò) per motivi di sicurezza

Documento richiesto dal fondatore e riesaminato sul codice il **15/07/2026**.

Questo elenco distingue ciò che è già protetto, ciò che è solo parziale e ciò che deve essere
completato prima di usare PaghePro con clienti e dipendenti reali. Non è una promessa generica:
ogni affermazione indica il limite reale e la via corretta.

## Stato in breve

| Stato | Significato |
|---|---|
| ✅ Protetto | La protezione è presente nel codice ed è coperta da verifiche. |
| 🟠 Parziale | Esiste una protezione, ma non copre ancora tutto il flusso. |
| 🔴 Da completare | Non usare la funzione con dati reali finché il lavoro indicato non è concluso. |

## 1. Invio automatico tramite bot sui portali pubblici — ✅ scelta sicura

**Cosa non ho fatto:** automatizzare login, compilazione e clic su “Invia” nei siti di Agenzia
delle Entrate, INPS, INAIL o Camera di Commercio usando le credenziali dell’utente.

**Perché:** un bot del browser non è un’integrazione ufficiale, è fragile rispetto ai cambiamenti
del portale e può violarne le condizioni d’uso. Un invio sbagliato produce effetti reali e la sua
correzione può richiedere una procedura formale.

**Via corretta:** PaghePro prepara file o bozze e applica i controlli interni disponibili. L’output
deve poi superare il controllo ufficiale dell’ente e essere caricato consapevolmente dal
professionista, oppure trasmesso tramite un provider/canale ufficiale autorizzato.

**Limite reale oggi:** il controllo ufficiale F24 è stato provato solo per i casi documentati; il
controllo INPS di UniEmens e i controlli AdE di CU, LIPE e 770 non sono ancora completati. Quegli
output non sono pronti per l’invio reale.

## 2. Segreti, chiavi API e certificati nel repository — ✅ protetto, 🟠 custodia operativa richiesta

**Cosa non ho fatto:** scrivere chiavi JWT, chiavi AI, chiavi SdI, PIN o certificati di firma nel
codice o nei file versionati.

**Perché:** la cronologia del repository conserva i segreti e può renderli leggibili a chi ottiene
accesso al progetto. Una chiave trapelata può esporre dati, firme o costi API.

**Via corretta:** usare secret/variabili d'ambiente. In produzione PaghePro richiede una chiave
casuale Base64 da 32 byte e rifiuta la chiave di sviluppo. Tutti i tre IBAN persistiti, la terna
malattia/infortunio e le copie nelle voci cedolino sono envelope AES-256-GCM versionati, legati con
AAD a tenant, tabella, record e campo. Il runner one-shot migra plaintext/v1/vecchie chiavi in
batch dentro una transazione atomica con fencing degli scrittori, verifica tenant/orfani, controlla
gli envelope e rimuove le colonne legacy; la sentinella viene ruotata per ultima. Lo startup strict
e `--verifica` fanno controlli read-only prima di servire traffico. TOTP e ApiKey SdI restano
cifrati e ruotabili.

**Da completare prima dei clienti reali:** custodire `PAGHE_DATA_KEY` nel secret manager, separata
dal database e dai backup; configurare il recipient pubblico `age`, provare il ripristino in
produzione e approvare il runbook di rotazione. Lo script backup ora fallisce chiuso e produce solo
file cifrati `0600`. I backup creati prima del cutover possono contenere colonne storiche e vanno
protetti fino a scadenza o distrutti secondo retention. Vedi `docs/GDPR-CIFRATURA.md`.

## 3. Super-admin ottenibile registrando un’email — ✅ corretto

**Cosa ho cambiato:** la registrazione crea sempre un account normale. Conoscere o registrare
l’email del fondatore non concede privilegi di piattaforma.

**Perché era pericoloso:** un estraneo avrebbe potuto prendere il controllo degli studi clienti.

**Via corretta:** il primo super-admin si crea una sola volta, da utente autenticato, con un token
segreto `SuperAdmin:BootstrapToken` di almeno 32 caratteri. Dopo il bootstrap il token va rimosso.
Il confronto è protetto e l’operazione è limitata contro i tentativi ripetuti.

## 4. Firma digitale e caricamento di PFX/PIN — ✅ endpoint protetti, 🟠 collaudo reale mancante

**Cosa non ho fatto:** persistere deliberatamente PFX e PIN o registrarli nei log. Il certificato
caricato viene usato in memoria per il tempo necessario alla firma.

**Cosa è protetto:** `/api/firma/cades`, `/api/firma/verifica` e `/api/fatture/firma` richiedono
autenticazione e hanno un limite di frequenza. Gli upload applicano limiti separati a documento,
PFX e file firmato, riconoscono il formato minimo dal contenuto e accettano solo `cades` o `xades`.
PFX e CNS/PIN non possono essere forniti insieme. Gli errori tecnici sono registrati lato server
con un riferimento, mentre il client riceve un messaggio neutro. Test HTTP verificano che senza
token tutti e tre gli endpoint rispondano `401` e che il superamento della quota produca `429`.
Il PFX temporaneo viene azzerato dalla memoria dopo il caricamento; il parser XAdES vieta DTD ed
entità e limita i caratteri elaborabili.

**Limite reale:** manca ancora il collaudo con certificato qualificato/CNS reale e la verifica
completa di catena e revoca del certificato. La CNS deve rimanere sulla macchina fidata che firma;
non va trasformata in una chiave privata nel backend cloud.

## 5. Dati sanitari non necessari — ✅ minimizzazione applicata

**Cosa non faccio:** salvare diagnosi o motivazioni mediche di malattia e infortunio.

**Perché:** sono dati particolari. Al calcolo servono le assenze e gli elementi amministrativi,
non la diagnosi.

**Via corretta:** PaghePro conserva giorni di assenza e numero progressivo dell’evento, non la
diagnosi. Se in futuro fosse indispensabile un nuovo dato sanitario, dovrà prima avere finalità,
base giuridica, accessi e retention approvati.

## 6. Far decidere paghe o imposte all’AI — ✅ scelta sicura, 🟠 regole da completare

**Cosa non faccio:** permettere a un modello AI di scegliere autonomamente importi, aliquote,
codici tributo o azioni con effetti legali.

**Perché:** un modello può produrre un dato plausibile ma errato.

**Via corretta:** l’AI è spenta per impostazione predefinita e, quando abilitata, legge e propone
campi con una confidenza. Il parser e il calcolo deterministico prevalgono; la conferma resta
umana.

**Limite reale oggi:** l’obiettivo è avere tutte le regole normative nel database versionato, ma
alcune regole di malattia/infortunio e maturazione sono ancora nel codice e devono essere migrate,
versionate e validate da un Consulente del Lavoro.

## 7. Inviare documenti personali a un provider AI senza governance — 🔴 non autorizzato

**Cosa non faccio:** considerare sicuro l’invio di cedolini, visure o altri documenti reali a un
provider AI solo perché è stata inserita una chiave API.

**Perché:** quando un provider esterno è attivo, testo o immagini possono uscire dal sistema. Oggi
non esiste una redazione automatica completa dei dati personali prima dell’invio.

**Via corretta:** prima servono scelta documentata del provider, DPA, verifica di luogo del
trattamento/trasferimenti, retention, informativa e base giuridica. Per documenti sensibili va
preferito un modello locale quando adeguato. Senza queste decisioni, lasciare l’AI spenta.

## 8. Mescolare i dati di studi diversi — 🟠 difesa applicativa presente, RLS mancante

**Cosa non faccio:** progettare servizi che leggano o modifichino dati senza lo studio
(`tenantId`) ricavato dal token autenticato.

**Perché:** la fuga di dati fra studi è uno degli incidenti più gravi e può richiedere una
valutazione di data breach e, in base al rischio, le notifiche previste dal GDPR.

**Via corretta oggi:** i servizi tenant-scoped principali filtrano per `tenantId` e sono presenti
test di isolamento. Non è però corretto promettere che ogni query futura sia automaticamente
sicura.

**Da completare:** introdurre difesa strutturale nel database (RLS/policy equivalenti), mantenere
test obbligatori per ogni nuovo modulo ed eseguire un audit completo prima dei clienti reali.

## 9. Dichiarare “GDPR completato” — 🟠 non ancora consentito

**Cosa non faccio:** presentare cifratura e una tabella audit come conformità GDPR completa.

**Stato reale:** la cifratura dei campi sensibili e il registro applicativo esistono. Il registro
copre richieste autenticate, accessi, esiti e creazione/modifica/cancellazione senza copiare valori
sensibili; resta distinto dall'audit infrastrutturale degli accessi diretti a database, host e WAF.

**Via corretta:** completare retention/cancellazione, audit infrastrutturale, gestione delle richieste
dell’interessato, data breach, registro dei trattamenti, DPA, informative, ruoli e DPIA quando
necessaria. Le scelte legali devono essere approvate da un professionista.

## 10. Fidarsi di estensione o Content-Type degli upload — 🔴 da completare

**Cosa non faccio:** considerare innocuo un PDF/XML/PFX soltanto perché il browser ne dichiara il
tipo corretto.

**Perché:** nome, estensione e Content-Type possono essere falsificati; file enormi o malevoli
possono consumare risorse o colpire i parser.

**Via corretta:** gli upload di firma hanno ora limiti e riconoscimento minimo dal contenuto. Sugli
altri upload occorre mantenere o aggiungere limiti, usare parser confinati e introdurre scansione
malware/quarantena per documenti reali.

## 11. Consegnare come definitivo un output non certificato — 🟠 etichette presenti, controllo globale mancante

**Cosa non faccio:** dichiarare pronto un cedolino, F24, CU, UniEmens, LIPE, 770 o altro file
telematico senza le verifiche richieste per quello specifico output.

**Perché:** un file formalmente plausibile può essere scartato o contenere calcoli non validati.

**Via corretta:** mantenere la dicitura **BOZZA DA VALIDARE**, eseguire il controllo ufficiale
disponibile e ottenere la validazione professionale dove necessaria. Alcuni output hanno già
l’etichetta; serve ancora un controllo globale per garantire che nessuna schermata o esportazione
la ometta.

## 12. Ricalcoli e cedolini palesemente incoerenti — ✅ barriere presenti, non validazione professionale

**Cosa ho fatto diversamente dalla via più comoda:** il sistema blocca mesi futuri, periodi prima
dell’assunzione e mesi interamente successivi alla cessazione. Evita il doppione del dipendente e
protegge il ricalcolo di un mese con F24 già inviato; la forzatura richiede conferma esplicita.

**Perché:** un ricalcolo silenzioso può disallineare cedolino, F24 e archivi.

**Limite reale:** queste barriere riducono errori evidenti ma non certificano il calcolo paghe. Il
motore e i PDF restano da validare con casi reali da un Consulente del Lavoro.

## 13. Invii automatici di notifiche senza configurazione consapevole — ✅ default manuale

**Cosa non faccio:** attivare automaticamente email, SMS o WhatsApp e contattare clienti/dipendenti
senza configurazione e responsabilità chiare.

**Via corretta:** il canale predefinito prepara il testo senza inviarlo. Gli invii reali richiedono
credenziali del canale, destinatario corretto, base/preferenza applicabile e test. Per WhatsApp
servono anche i template e le regole del provider quando richiesti.

## 14. Portare in produzione uno schema database “da sviluppo” — 🔴 da completare

**Cosa non faccio:** considerare `EnsureCreated` e l’allineamento automatico delle colonne una
strategia definitiva di migrazione per clienti reali.

**Perché:** modifiche automatiche senza migrazioni versionate e rollback possono causare perdita o
incoerenza dei dati.

**Via corretta:** generare e revisionare migrazioni EF Core, provarle su copia del database,
verificare backup e ripristino, quindi usarle nel deploy stabile.

---

## Cosa serve dal fondatore per sbloccare la via corretta

| Voce | Cosa serve | Non fare |
|---|---|---|
| Dati reali | Legale/privacy + Consulente del Lavoro + DPIA/contratti quando necessari | Caricare clienti reali nella demo incompleta |
| Cifratura | `PAGHE_DATA_KEY` custodita, backup e piano di rotazione | Metterla nel repository o insieme al database |
| AI reale | Provider approvato, DPA/governance oppure modello locale | Inviare documenti personali con la sola ApiKey |
| Super-admin | Token bootstrap una tantum | Riutilizzare il token o lasciarlo configurato |
| Firma | Certificato/CNS su macchina fidata o delega formale al provider | Usare certificati reali senza collaudo di catena/revoca e macchina fidata |
| SdI | Account sandbox A-Cube e poi contratto/canale reale | Simulare login ai portali |
| INPS/AdE | Software/kit di controllo ufficiali e credenziali del professionista | Chiamare “definitivi” file non controllati |
| Conservazione | Provider qualificato e contratto | Trattare un normale backup come conservazione a norma |
| Retention | Tempi approvati, eccezioni e legal hold | Cancellare tutto automaticamente senza regole |

## Checklist per la prossima revisione

Usare questa richiesta come checklist, non come garanzia tecnica automatica:

> Rivedi il codice e la documentazione e aggiorna ogni cosa non fatta o fatta diversamente per
> sicurezza, privacy o conformità. Per ogni voce indica: stato verificato, pericolo, via corretta,
> dipendenze esterne e prova eseguita. Correggi le affermazioni assolute non dimostrate e aggiorna
> `docs/COSE-NON-FATTE-PER-SICUREZZA.md`. Non introdurre scorciatoie di sicurezza.
