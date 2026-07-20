# Registro attività GDPR

Data verifica tecnica: 15 luglio 2026.

Questo documento descrive il perimetro tecnico del registro accessi e modifiche di PaghePro.
Non sostituisce privacy policy, registro dei trattamenti, DPA o valutazione del consulente privacy.

## Cosa viene registrato

- login riusciti e tentativi riconducibili a un account valido (2FA errato/mancante o studio sospeso);
- richieste API autenticate, incluse letture, operazioni, rifiuti RBAC, rate limit, 404 ed errori;
- inserimenti, modifiche e cancellazioni EF delle entità dotate di `TenantId`;
- utente, tenant, data/ora UTC, risorsa, template della rotta, soli identificativi UUID della rotta,
  esito, codice HTTP e correlation ID;
- per una modifica, esclusivamente i **nomi semantici** dei campi cambiati (per esempio `Iban`),
  mai il valore precedente o nuovo.

Le modifiche ai dati e la relativa riga di audit sono salvate nella stessa transazione. Un mismatch
fra tenant autenticato e `TenantId` del record blocca l'intera operazione. Il registro non può essere
modificato o cancellato tramite EF: è append-only a livello applicativo.

## Cosa non viene registrato

- password, codici 2FA, chiavi provider o PIN;
- body della richiesta o contenuto della risposta;
- query string e parametri testuali potenzialmente personali;
- valori di IBAN, salute, cedolino, corpo/credenziale del modulo Posta (email/PEC) o altri campi
  protetti: solo il nome semantico compare come campo modificato (`CorpoMessaggio`,
  `CredenzialeCasellaPosta`), mai il contenuto;
- indirizzo IP, posizione, user-agent o impronta del dispositivo;
- email nel record persistito: l'email mostrata all'admin è risolta dalla tabella utenti al momento
  della consultazione, con filtro sul medesimo tenant.

Questa scelta applica la minimizzazione: il registro deve poter dimostrare l'operazione senza creare
una seconda copia dei documenti o dei dati del lavoratore.

## Consultazione e autorizzazioni

La schermata **Amministrazione → Registro attività** e `GET /api/audit` sono accessibili soltanto ai
ruoli `admin` e `superadmin`. Il super-admin vede esclusivamente il registro del proprio tenant di
piattaforma, compresi gli eventi espliciti di sospensione/riattivazione studi; non può usare questo
endpoint per leggere il registro di un cliente. `operatore`, `sola_lettura` e ruoli sconosciuti sono
esclusi. Ogni query usa il `tenant_id` del token e non accetta un tenant dalla richiesta. La pagina è
limitata a 200 righe per chiamata (50 nell'interfaccia) e filtra periodo, categoria e azione.

La consultazione del registro viene a sua volta registrata, ma la riga compare dalla richiesta
successiva perché l'evento finale viene scritto dopo aver prodotto la risposta corrente.

## Comportamento in caso di errore

- Se una scrittura dati fallisce o la transazione viene annullata, anche il relativo evento di
  modifica viene annullato; il middleware conserva comunque l'esito HTTP/errore della richiesta.
- L'evento di accesso usa un nuovo scope database, così non riutilizza un `DbContext` guasto.
- Se la sola scrittura dell'access log fallisce, l'operazione già conclusa non viene trasformata in
  un falso errore per l'utente; viene emesso un log infrastrutturale `Scrittura registro attività
  fallita` con correlation ID. In produzione questo messaggio deve generare un alert.
- Le richieste anonime con credenziali sconosciute non vengono inserite nel registro tenant perché
  non sono attribuibili senza conservare l'email tentata. In produzione **devono** essere raccolte
  dal gateway/WAF con accesso ristretto e retention dedicata: è un requisito operativo, non una
  garanzia già fornita da questo repository.

## Limiti residui e gestione operativa

- L'append-only applicativo non protegge da un amministratore del database con privilegi diretti.
  In produzione usare un account runtime senza privilegi DDL, limitare e auditare gli accessi DB e,
  per requisiti di inalterabilità più forti, inoltrare gli eventi a uno storage esterno immutabile.
- La durata di conservazione e la cancellazione selettiva sono il passo GDPR successivo di
  `PIANO.md`; non è corretto fissare qui un periodo unico senza finalità e base giuridica approvate.
- Alcune entità figlie prive di `TenantId` non producono ancora una riga di modifica granulare:
  l'operazione resta coperta dall'evento HTTP e, quando presente, dalla modifica dell'entità padre.
  Per coprire anche scritture dirette sulle figlie servirà un'evoluzione tenant-aware dello schema.
- Sincronizzare gli orologi dei server e monitorare spazio, errori di audit e crescita della tabella.
- Il rollout crea/allinea le colonne sia su SQLite sia su PostgreSQL; prima della produzione restano
  obbligatori backup cifrato, restore provato e migrazioni EF controllate.

## Verifiche automatiche

I test coprono: eventi di inserimento/modifica, nomi semantici dei campi protetti, assenza dei valori,
correlation ID, mismatch tenant, append-only EF, isolamento della consultazione, email tenant-safe,
lettura riuscita, 403, errore 500, richiesta anonima, RBAC admin-only, login e protezione dei quattro
endpoint professionali in precedenza anonimi. Suite completa al 17 luglio 2026: **597/597 verde**;
build frontend di produzione verde.

## Fonti ufficiali consultate

- [Regolamento (UE) 2016/679, artt. 5, 25 e 32 — EUR-Lex](https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=it),
  consultato il 15 luglio 2026: minimizzazione, responsabilizzazione, privacy by design e sicurezza.
- [Codice di condotta per le imprese di sviluppo e produzione di software gestionale — Garante](https://www.garanteprivacy.it/documents/10160/0/Codice%2Bdi%2Bcondotta%2Bper%2Bil%2Btrattamento%2Bdei%2Bdati%2Bpersonali%2Beffettuato%2Bdalle%2Bimprese%2Bdi%2Bsviluppo%2Be%2Bproduzione%2Bdi%2Bsoftware%2Bgestionale.pdf/a89ad70a-ee18-22fa-8e01-578b04f1023e?download=true&version=2.0),
  consultato il 15 luglio 2026: funzionalità di tracciamento degli accessi e delle attività per ruolo.
- [Provvedimento sugli amministratori di sistema — Garante, punto 4.5](https://www.garanteprivacy.it/home/docweb/-/docweb-display/docweb/1577499),
  consultato il 15 luglio 2026: completezza, inalterabilità e verificabilità degli access log come
  riferimento di sicurezza per gli accessi amministrativi.
