# Piano completo PaghePro per App Store e Google Play

Stato al 17 luglio 2026: **app mobile generata e compilabile; pubblicazione bloccata dai gate esterni e di sicurezza elencati sotto**.

Gli artefatti locali di verifica usano intenzionalmente `it.paghepro.app.dev` e
`https://api.paghepro.invalid`: dimostrano compilazione e resa grafica, ma login e dati non possono
funzionare. Non sono build per TestFlight, Play Internal o utenti reali.

Questo documento distingue ciò che il codice può fare automaticamente da ciò che deve essere
completato dal titolare dell'app negli account Apple e Google. Non è consentito sostituire i dati
reali con valori fittizi nelle dichiarazioni degli store.

## Obiettivo e architettura

PaghePro usa React/Vite nel frontend e ASP.NET Core nel backend. Le applicazioni iOS e Android
sono contenitori nativi Capacitor che incorporano la build web: nessun `server.url` remoto e
nessun sito caricato dentro una WebView generica. L'app chiama soltanto l'API pubblica configurata
in fase di build.

Identificativo locale provvisorio: `it.paghepro.app.dev`. Non va registrato negli store. Quello
definitivo deve appartenere all'organizzazione editrice e, una volta pubblicato, non si cambia.

## Fatto nel codice

- [x] Progetti Xcode e Android Studio generati in `frontend/ios` e `frontend/android`.
- [x] Build mobile separata dalla build web e vincolata a un'API HTTPS reale.
- [x] Blocco della build store con dominio `.invalid`, HTTP o identificativo `.dev`.
- [x] Google Login e account demo esclusi dalla variante mobile; rimane email/password con 2FA.
- [x] Service worker disabilitato nel contenitore nativo.
- [x] Condivisione nativa di PDF/XML/file tramite pannello Condividi, con file temporanei in cache.
- [x] Safe area iPhone e navigazione responsive.
- [x] Android: traffico in chiaro e backup disabilitati, FileProvider limitato alla cache.
- [x] iOS: dichiarazione crittografia standard e manifesto privacy incluso.
- [x] Icone PaghePro incluse; chiavi `.jks`, `.keystore`, `.p12`, `.p8` escluse dal repository.
- [x] Compilazione verificata su iOS Simulator e Android API 36.

## Gate tecnici obbligatori prima dell'invio

- [ ] Pubblicare backend e frontend integrato su un dominio HTTPS stabile, senza URL temporanei.
- [x] Autorizzare sul backend le sole origini native effettive: `capacitor://localhost` per iOS
  e `https://localhost` per Android, con preflight e credenziali verificati da test automatico.
- [x] Sostituire il JWT annuale salvato nel browser con JWT v2 di 15 minuti, refresh opaco
  monouso ruotabile/revocabile, cookie web `HttpOnly` e custodia mobile interna in Keychain/
  Android Keystore. I vecchi JWT vengono rifiutati e il database conserva solo SHA-256.
- [x] Aggiungere logout server-side e blocco immediato delle sessioni quando lo studio è sospeso.
- [ ] Revocare tutte le sessioni dopo il futuro cambio password e durante la cancellazione account.
- [ ] Implementare nell'app l'avvio della cancellazione account/studio, con conferma forte,
  revoca immediata delle sessioni e stato visibile della richiesta.
- [ ] Pubblicare anche una pagina web pubblica per richiedere la cancellazione richiesta da
  Google Play.
- [ ] Definire con il professionista privacy quali dati fiscali, paghe e documenti devono essere
  cancellati, anonimizzati oppure conservati per obbligo di legge; gestire il legal hold.
- [ ] Ospitare Informativa privacy, Assistenza e pagina Cancellazione su URL HTTPS pubblici.
- [ ] Chiudere il gate Apple 4.2 con una release mobile focalizzata su almeno tre attività reali
  provate su dispositivo (per esempio import documento, controllo scadenze e condivisione/approvazione
  di un file), documentate anche nelle note per il revisore; il solo involucro web non basta.
- [ ] Completare il manifesto privacy iOS e le schede Apple/Google dopo l'inventario reale: il
  file tecnico corrente non dichiara categorie raccolte e non sostituisce i moduli degli store.
- [ ] Esportare l'icona Apple 1024×1024 definitiva senza trasparenza e validarla nell'archive.
- [ ] Eseguire prova completa su iPhone/iPad e almeno un telefono/tablet Android reali.
- [ ] Preparare un account revisore con dati dimostrativi non personali e 2FA utilizzabile dal
  team di revisione.

## Gate Apple a carico del titolare

- [ ] Iscrivere il soggetto editore all'Apple Developer Program come **Organization**.
- [ ] Completare verifica legale, numero D-U-N-S se richiesto, accordi, dati fiscali e bancari.
- [ ] Confermare identificativo bundle definitivo e creare il record in App Store Connect.
- [ ] Creare certificati/profili con firma gestita da Xcode; le chiavi restano nel Keychain del
  titolare e non nel repository.
- [ ] Compilare App Privacy con la mappa dati verificata dal titolare.
- [ ] Caricare screenshot reali nei formati richiesti, descrizione, categoria, classificazione età,
  copyright, contatti, note revisore e credenziali demo.
- [ ] Archiviare da Xcode, caricare prima su TestFlight, completare un collaudo e poi inviare alla
  revisione App Store.

## Gate Google a carico del titolare

- [ ] Aprire Google Play Console come organizzazione e completare verifica identità/contatti.
- [ ] Confermare package name definitivo e creare l'app in Play Console.
- [ ] Attivare Play App Signing e custodire separatamente la upload key.
- [ ] Compilare Data safety, App access, Ads, Target audience, Content rating, categoria e contatti.
- [ ] Inserire URL privacy e URL pubblico di cancellazione account.
- [ ] Caricare l'Android App Bundle firmato nel canale Internal testing, eseguire il collaudo e
  promuovere progressivamente a Closed/Open/Production secondo i requisiti dell'account.

## Comandi di rilascio

Esempio da eseguire soltanto con valori reali:

```bash
cd frontend
MOBILE_APP_ID="it.azienda.paghepro" npm run mobile:finalize-id

MOBILE_API_URL="https://app.example.it" \
MOBILE_APP_ID="it.azienda.paghepro" \
npm run mobile:sync
```

`mobile:finalize-id` è un passaggio una tantum: aggiorna realmente package Android, namespace,
percorso Java e bundle ID Xcode. Va eseguito solo dopo approvazione del titolare; il comando di
sync verifica che tutti i progetti coincidano e si blocca se trova ancora `.dev`.

Poi:

```bash
npm run mobile:ios
npm run mobile:android
```

`mobile:ios` apre Xcode; `mobile:android` apre Android Studio. La firma e l'upload si fanno dagli
account dell'organizzazione editrice.

## Criterio di via libera

Il verdetto diventa **PRONTO PER TESTFLIGHT/INTERNAL TESTING** solo quando tutti i gate tecnici
sono chiusi e gli URL pubblici rispondono. Diventa **PRONTO PER REVISIONE STORE** solo dopo test
reali, moduli privacy compilati coerentemente e credenziali revisore verificate.

Fonti ufficiali: [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/),
[cancellazione account Apple](https://developer.apple.com/support/offering-account-deletion-in-your-app),
[App Privacy](https://developer.apple.com/help/app-store-connect/reference/app-privacy/),
[requisiti target API Google](https://developer.android.com/google/play/requirements/target-sdk),
[cancellazione account Google](https://support.google.com/googleplay/android-developer/answer/13327111?hl=it),
[Data safety](https://support.google.com/googleplay/android-developer/answer/10787469?hl=it),
[firma Android](https://developer.android.com/studio/publish/app-signing).
