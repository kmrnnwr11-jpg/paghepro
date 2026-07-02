# Prompt per Fable — lavoro in loop fino a completamento

Come usarlo: apri Claude Code, scrivi `/goal ` e incolla tutto il testo qui sotto.
Un solo agente alla volta sul progetto (mai due insieme). Ogni fase finisce con
test verdi + commit, così il lavoro non si perde mai.

---

OBIETTIVO: porta l'app PaghePro a livello "pronta da mostrare": sicurezza verificata e rinforzata, login senza problemi, calcoli paghe/CU/contabilità affidabili, e interfaccia moderna stile glassmorphism (vetro) con animazioni 3D leggere. Lavora in autonomia fase per fase finché tutto è VERIFICATO. Non fermarti a metà.

REGOLE FISSE (mai violarle):
- Dopo OGNI fase: `cd backend && dotnet test Paghe.slnx -c Release` verde e `cd frontend && npm run build` senza errori, poi un commit con messaggio chiaro.
- Importi e aliquote sempre `decimal`. Mai inventare valori normativi (aliquote, minimi CCNL): se un valore non è verificabile, scrivilo in `docs/TODO-DA-VERIFICARE.md` invece di inventarlo.
- Isolamento multi-tenant: ogni query persistita deve filtrare per tenantId.
- Modifiche chirurgiche: non rifattorizzare ciò che funziona, non toccare i valori di Seed.cs.

FASE 0 — Fotografia iniziale
- Esegui test backend e build frontend. Se qualcosa è rosso, sistemalo prima di tutto. Commit checkpoint dello stato attuale (ci sono modifiche non committate: salvale).

FASE 1 — Sicurezza (audit + correzioni)
- CORS: oggi è AllowAnyOrigin. Rendilo configurabile da appsettings (lista di origin per produzione, permissivo solo in sviluppo).
- Aggiungi header di sicurezza HTTP: X-Content-Type-Options, X-Frame-Options, Referrer-Policy, HSTS solo in produzione.
- Rivedi JWT (durata, comportamento a token scaduto), rate limiting, upload file (PDF visura/UNILAV: limite dimensione e tipo file), messaggi di errore che non devono rivelare dettagli interni.
- Verifica l'isolamento tenant su TUTTI gli endpoint del gruppo secured: per ognuno controlla che non si possano leggere/modificare dati di un altro tenant cambiando gli ID nella richiesta. Scrivi test automatici per i casi principali (cedolini, fatture, bilanci, F24, CU).
- Controlla che nessun segreto (chiavi, password) sia nel codice o nei file committati.
- Scrivi i risultati in `docs/AUDIT-SICUREZZA.md`: cosa era debole, cosa hai corretto, cosa resta da decidere al fondatore.

FASE 2 — Login senza problemi
- Prova tutti i flussi: registrazione, login, password sbagliata (messaggio chiaro in italiano), rate limiting, 2FA, ruoli, super-admin. Sistema ogni messaggio confuso.
- Google login: il codice c'è già; verifica che il backend gestisca bene token Google invalidi/scaduti e che il frontend spieghi chiaramente quando non è configurato. Non servono chiavi nuove: la configurazione la fa il fondatore con docs/GUIDA-GOOGLE-LOGIN.md.
- Aggiungi i test mancanti sui casi limite dell'autenticazione.

FASE 3 — Affidabilità calcoli (buste paga, CU, contabilità)
- Esegui tutti i golden test esistenti. Poi crea test di coerenza incrociata: su un anno di cedolini di esempio, cedolino archiviato → Certificazione Unica → 770 → F24 devono raccontare gli stessi numeri.
- Controlla arrotondamenti al centesimo e casi limite: assunzione/cessazione a metà mese, tredicesima, part-time.
- Ogni incongruenza trovata: PRIMA un test che la riproduce, POI la correzione.

FASE 4 — Interfaccia moderna "glass" con animazioni
- Crea un design system coerente in frontend/src/index.css: palette scura elegante + effetto vetro (backdrop-filter blur, bordi luminosi sottili, ombre morbide), tipografia moderna, spaziature coerenti.
- Animazioni: transizioni fluide su hover/focus, ingressi delle card con leggero effetto 3D (transform/perspective sottile in CSS puro, niente librerie pesanti), rispetta prefers-reduced-motion.
- Applica lo stile a TUTTE le pagine (~26 in frontend/src/pages) partendo da: Accesso, Dashboard, BustaPaga, Cedolini, CertificazioneUnica, Scadenzario. Una pagina alla volta, verificando che form e bottoni continuino a funzionare.
- L'app è una PWA usata anche da telefono: tutto deve restare leggibile e usabile su schermo piccolo.
- Nessuna nuova dipendenza npm pesante senza segnalarlo.

FASE 5 — Rapporto finale
- Completa `docs/AUDIT-SICUREZZA.md` e scrivi `docs/RAPPORTO-MIGLIORAMENTI.md` in linguaggio semplice per un fondatore non tecnico: cosa è stato migliorato, cosa resta, e cosa richiede un'azione del fondatore (attivare Google login, scegliere i domini per il CORS di produzione).
- Test finali verdi + commit finale.
