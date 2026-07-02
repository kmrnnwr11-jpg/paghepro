# Audit di sicurezza — PaghePro

Data: 2 luglio 2026. Ambito: backend API (.NET), frontend SPA, configurazione di deploy.
Ogni punto dice: **com'era**, **cosa è stato fatto**, oppure **cosa resta da decidere**.

## Correzioni applicate

### 1. CORS (chi può chiamare l'API dal browser)
- **Com'era**: `AllowAnyOrigin` — qualunque sito web poteva chiamare l'API dal browser
  di un utente, anche in produzione.
- **Ora**: permissivo **solo in sviluppo**. In produzione gli origin ammessi si elencano
  in `Cors:Origini` (appsettings o variabili `Cors__Origini__0`, `Cors__Origini__1`, …).
  Senza lista, il browser blocca ogni chiamata cross-origin e all'avvio compare un
  avviso chiaro nei log. In `render.yaml` è predisposta la variabile da compilare.
- **Azione del fondatore**: impostare `Cors__Origini__0` = URL pubblico del frontend
  (es. `https://paghe-frontend.onrender.com`) sul servizio backend.

### 2. Header di sicurezza HTTP
- **Com'era**: nessun header di sicurezza.
- **Ora**: su ogni risposta `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer`; in produzione anche HSTS
  (`Strict-Transport-Security`, 1 anno, impostato direttamente perché il TLS termina
  al proxy del provider).

### 3. Import CCNL riservato al super-admin
- **Com'era**: `POST /api/ccnl/importa` scriveva le tabelle CCNL (minimi tabellari)
  **condivise tra tutti i tenant** con un token qualsiasi: uno studio malevolo poteva
  alterare i dati usati nei calcoli paghe di tutti gli altri.
- **Ora**: riservato al ruolo `superadmin` (HTTP 403 per gli altri).

### 4. Rate limiting sulla registrazione
- **Com'era**: login e 2FA erano protetti (10 tentativi/minuto per IP), ma
  `POST /api/auth/registra` no: creazione account illimitata.
- **Ora**: anche la registrazione è sotto la policy `auth` (10/minuto per IP).

### 5. Upload file (visura camerale, UNILAV)
- **Com'era**: nessun limite esplicito di dimensione o tipo (valeva solo il limite
  implicito del server, ~28 MB).
- **Ora**: massimo **10 MB**; visura accetta solo PDF o immagine (JPG/PNG/WEBP/TIFF),
  UNILAV solo PDF. Errori in italiano, senza dettagli interni.

### 6. Swagger non esposto in produzione
- **Com'era**: la documentazione interattiva dell'API (la "mappa" di tutti gli endpoint)
  era pubblica anche in produzione, e il health check di Render la usava.
- **Ora**: attiva solo in sviluppo (riattivabile con `Swagger:Abilitato=true`);
  il health check di Render punta a `/health`.

### 7. Test automatici di isolamento multi-tenant
- **Com'era**: isolamento verificato nei service (ogni query filtra per `TenantId`) ma
  con test solo su fatture, avvisi di pagamento e GDPR.
- **Ora**: nuovo `IsolamentoTenantTests.cs` — un tenant "intruso" che conosce gli ID di
  un altro tenant non può leggere né modificare **cedolini, fatture, bilanci, pratiche
  F24, Certificazioni Uniche** (5 test). L'audit degli endpoint ha confermato che tutti
  i 73 endpoint `secured` derivano il tenant dal claim `tenant_id` del token JWT, mai
  da header o parametri della richiesta; i soli endpoint senza tenant sono calcolatori
  puri senza persistenza (`/simula`, `/ravvedimento/calcola`) o dati condivisi in sola
  lettura (elenco CCNL).

## Verificato e giudicato adeguato (nessuna modifica)

- **JWT**: firma HMAC-SHA256, scadenza 8 ore, lifetime validato. In produzione l'avvio
  è **bloccato** se la chiave manca, è quella di sviluppo o è più corta di 32 byte
  (su Render la genera `Jwt__Key: generateValue`). A token scaduto l'API risponde 401 e
  il frontend pulisce il token e riporta all'accesso.
- **Password**: hash PBKDF2 con salt (`PasswordHasher`), mai in chiaro.
- **Ruoli (RBAC)**: middleware `RuoloPolicy` — `sola_lettura` non può scrivere,
  azioni distruttive (anonimizzazione GDPR) solo `admin`, gestione studi solo `superadmin`.
- **Segreti**: nessuna chiave o password reale nel codice o nei file committati.
  Le sezioni di appsettings (SMTP, Twilio, A-Cube…) sono vuote con nota "tenere come
  secret". Il `Google:ClientId` è un identificativo pubblico, non un segreto.
- **Messaggi di errore**: in produzione niente stack trace (nessuna developer exception
  page). I messaggi verso l'utente sono in italiano e non rivelano l'esistenza di dati
  altrui ("non trovato per il tenant indicato").

## Punti accettati consapevolmente / da decidere al fondatore

1. **Account demo condiviso** (`demo@paghe.local`, password nel codice frontend):
   è una funzione voluta ("Prova la demo"). Chiunque vede i dati inseriti dagli altri
   visitatori in quello spazio. Va bene per la vetrina; da rimuovere o azzerare
   periodicamente quando arrivano clienti veri.
2. **CORS produzione**: impostare l'URL del frontend (vedi punto 1 sopra) — senza,
   l'app pubblicata su dominio separato non funziona.
3. **Messaggi d'errore della firma digitale** (`Firma non riuscita: …`) riportano il
   messaggio tecnico della libreria crittografica: utile per diagnosi, rischio basso.
   Se si vuole massima opacità, sostituire con un messaggio generico.
4. **`ValidateIssuer/ValidateAudience` disattivi nel JWT**: accettabile finché il token
   è emesso e consumato dalla stessa applicazione; da attivare se in futuro più servizi
   condivideranno la stessa chiave.
5. **Database**: in modalità "prova gratis" su Render i dati si azzerano ai riavvii
   (già documentato in `render.yaml`/`DEPLOY.md`); per dati veri servono disco
   persistente e backup (`scripts/backup-db.sh`).
6. **GDPR**: cifratura dei campi sensibili e retention sono nel piano
   (`docs/PIANO-COMPLETAMENTO.md`), non ancora complete: da pianificare prima di
   trattare dati reali di dipendenti.
