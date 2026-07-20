# Cosa manca (lavori futuri) — Paghe SaaS

Registro onesto di ciò che **non è ancora completo**, per non perderlo. Aggiornato al 2026-06-27.
Legenda: ✅ fatto · ⚠️ parziale/da rifinire · ❌ da fare · 🔑 richiede credenziali/accreditamenti esterni (non solo codice).

> Nota: il software **calcola e prepara** bene (il "motore"). La parte che manca è soprattutto
> l'**invio reale verso gli enti** (il "modem") e il collegamento delle schermate ai dati veri.

---

## 1) Fattura elettronica → SdI (priorità ALTA)
- ✅ Generazione XML FatturaPA + validazione XSD + controlli SdI + PDF.
- ✅ Infrastruttura di trasmissione: gateway astratto, canale Manuale/Simulato/Rest, ricevute/stati, deleghe.
- ✅ Schermata self-service "Collega Fatturazione" (provider + API key salvati per tenant).
- ⚠️ Canale `Rest` **generico**: va mappato sulle API **reali** del provider scelto
  (Aruba / Fatture in Cloud / Acube): endpoint esatti, formato payload, nomi campi risposta.
  Codice: `Paghe.Persistence/Trasmissione/SdiGatewayProviderRest.cs` (+ eventuale `SdiGatewayAcube.cs`).
- ❌ 🔑 **Account provider + API key reale** (la prende l'utente/commercialista). Vedi `docs/GUIDA-COLLEGA-FATTURAZIONE.md`.
- ❌ 🔑 **Webhook ricevute** dal provider (oggi le ricevute si registrano via endpoint manuale `/api/trasmissioni/{id}/ricevute`).
- ⚠️ 🔑 **Firma CAdES/XAdES reale**: il codice firma c'è (`Paghe.Firma`), ma serve un **certificato vero**
  (PFX o smart card/CNS) — oppure delegare la firma al provider.
- ❌ **Conservazione a norma** dei documenti inviati (10 anni): non implementata.

## 2) Apertura P.IVA / ComUnica (priorità MEDIA)
- ✅ Checklist/piano adempimenti + scelta modello (AA9/12 vs AA7/10) + persistenza pratica.
- ❌ Generazione dei **file ufficiali** AA9/12, AA7/10, ComUnica.
- ❌ 🔑 Invio via **InfoCamere/Telemaco** (serve account Telemaco + plafond + accreditamento).
  Dettagli: `docs/integrazione-ufficiale/TODO-COMUNICA-CAMERA-INAIL-INPS.md`.

## 3) Adempimenti Agenzia Entrate (priorità MEDIA)
- ⚠️ **F24**: costruzione importi OK; tracciato ufficiale a lunghezza fissa **in corso/da rifinire**.
- ❌ **LIPE** (liquidazioni periodiche IVA): file ufficiale da generare.
- ❌ **CU/CUD**: generazione file ufficiale + invio.
- ❌ 🔑 Trasmissione via **Entratel/Desktop Telematico** (credenziali Entratel/Fisconline).
  Dettagli: `docs/integrazione-ufficiale/KIT-INTEGRAZIONE-AGENZIA-ENTRATE.md`.

## 4) Frontend — collegare le schermate ai dati veri (priorità ALTA, è codice nostro)
Oggi diverse schermate mostrano **dati demo di esempio**, non quelli reali dal backend:
- ❌ `Dipendente` (scheda) — usare lista/anagrafiche reali.
- ❌ `Presenze` (cartellino) — cartellini reali, salvataggio.
- ❌ `BustaPaga` (anteprima) — collegare al calcolo/cedolini archiviati reali.
- ❌ `Elaborazione` (batch) — elaborazione reale via API.
- ✅ Già collegate all'API reale: `Aziende`, `Cedolino` (calcolo), `Fatture`, `CollegaProvider`.

## 5) Notifiche / promemoria (priorità MEDIA)
- ✅ Scadenzario (DURC, F24) + Avvisi pagamento clienti.
- ❌ 🔑 **Invio email reale (SMTP)** dei promemoria: non ancora cablato (manca config SMTP/provider email).

## 6) Infrastruttura e produzione (priorità ALTA prima del go-live)
- ⚠️ **Migrazioni EF Core**: oggi `EnsureCreated` + SQL di compatibilità manuale in `PagheDbContext.EnsureSchemaSviluppoCompatibile`. Passare a migrazioni vere.
- ⚠️ **Database**: SQLite OK per ora; per più utenti passare a **PostgreSQL** (`UseSqlite`→`UseNpgsql`, una riga in `Program.cs`). Vedi `DEPLOY.md`.
- ✅ **JWT key da secret**: `TokenService` in ambiente Production **rifiuta** il default di sviluppo e le chiavi < 32 byte (avvio bloccato). Resta da **fornire** il secret reale in produzione (`Jwt__Key`).
- ⚠️ **CORS**: oggi `AllowAnyOrigin`; in produzione restringere agli origin reali.
- ⚠️ **GDPR**: cifratura dati sensibili e registro accessi/modifiche completati e verificati;
  restano **retention/cancellazione** e i documenti/azioni legali previsti nel piano.
- ✅ **Multi-utente reale**: login ripristinato — senza token si apre la schermata di accesso (Crea account / Accedi / 2FA / Google), ogni account è un tenant isolato. La **demo** ora è **isolata per browser** (un account demo per dispositivo), non più condivisa.

## 7) Dato normativo (sempre)
- ⚠️ I valori in `Seed.cs` e i parametri sono **esempi**: vanno **validati con un Consulente del Lavoro**
  prima dell'uso reale (aliquote, scaglioni, addizionali, minimi CCNL).

---

### Riepilogo "per essere usabile davvero"
1. 🔑 Account provider SdI + API key → invio fatture reale (resto già pronto).
2. Collegare le 4 schermate demo ai dati veri (codice nostro).
3. Migrazioni EF + JWT secret + CORS + (eventuale) PostgreSQL prima del go-live.
4. Completare la retention GDPR; configurare chiavi, monitoraggio audit e backup reali prima di
   trattare dati di persone (cifratura e registro attività applicativi sono fatti).
5. Validazione dei parametri normativi con un consulente.
