# Fase 1 — Correzioni di sicurezza P0 (15 luglio 2026)

Esecuzione della Fase 1 del `docs/PIANO-MASTER-AI-AUTONOMO.md`, autorizzata dal fondatore
("procedi"). Ramo dedicato: `epic/f1-sicurezza-p0`. Baseline di partenza: tag
`baseline-f1-2026-07-15`.

## Correzioni applicate

| # | Problema (P0) | Correzione | Test |
|---|---|---|---|
| 1 | **Super-admin ottenibile registrandosi** con l'email in `SuperAdmin:Email` (registrabile pubblicamente) | Rimossa la promozione automatica da email in `RegistraAsync`/`AccediConGoogleAsync` (ora sempre `admin`). Nuovo canale unico: `BootstrapSuperAdminAsync` + `POST /api/admin/bootstrap-superadmin`, token segreto `SuperAdmin:BootstrapToken` (≥32 char, confronto a tempo costante), a **uso singolo** (rifiuta se esiste già un super-admin) | `SuperAdminTests`: email non promuove (registrazione e Google), bootstrap valido/errato/non-configurato, uso singolo |
| 2 | **ApiKey del provider SdI in chiaro** nel database | Cifrata a riposo con `ProtettoreDati.ConverterStringa()` (come IBAN e segreto TOTP) | copertura EF esistente |
| 3 | **Endpoint pubblici** di calcolo/validazione/PDF stateless (`/api/cedolini/calcola`, `/api/fatture/calcola`, `/api/fatture/valida`, `/api/cedolini/pdf`, `/api/fatture/pdf`) | Spostati dal gruppo pubblico `app` al gruppo autenticato `secured` (`RequireAuthorization`) | build + suite |
| 4 | **Endpoint di firma** con upload PFX/PIN aperti al pubblico | `RequireAuthorization()` + `RequireRateLimiting("sensibili")` su `/api/firma/cades`, `/api/firma/verifica`, `/api/fatture/firma`; il certificato resta effimero e non viene loggato | build + suite |
| 5 | Rate limit assente su operazioni costose/sensibili | Nuova policy `sensibili` (30/min per utente, poi per IP) su firma; policy `auth` estesa al bootstrap | — |

## Verificato già a posto (nessuna modifica necessaria)

- **Chiave JWT**: `TokenService.ChiaveJwt` rifiuta il default di sviluppo in `Production` e le
  chiavi < 32 byte (fail-fast all'avvio). ✓
- **Chiave di cifratura dati**: `ProtettoreDati` rifiuta la passphrase di sviluppo in `Production`
  (fail-fast). ✓
- **IBAN e segreto TOTP**: già cifrati a riposo. ✓
- **Dati sanitari**: nessuna diagnosi memorizzata; si salvano solo giorni di assenza (necessari al
  calcolo e già presenti sul cedolino) — minimizzazione già soddisfatta, nessuna cifratura di
  contatori numerici che serve poter sommare/interrogare. Vedi
  `docs/COSE-NON-FATTE-PER-SICUREZZA.md` §5.

## Esito

- Build backend: 0 errori.
- Test: **490/490 verdi** (i due test che codificavano la vecchia falla email→superadmin sono
  stati riscritti per verificare il comportamento sicuro).
- Documento per il fondatore: `docs/COSE-NON-FATTE-PER-SICUREZZA.md`.

## Da fare prima del deploy pubblico (a carico del fondatore)

1. Impostare come **variabili d'ambiente** (mai nel repository): `Jwt__Key` (≥32 byte),
   `PAGHE_DATA_KEY`, e — solo per creare il primo super-admin — `SuperAdmin__BootstrapToken`
   (poi rimuoverla).
2. `ASPNETCORE_ENVIRONMENT=Production` effettivo nel deploy.
3. `Cors:Origini` con il dominio reale del frontend.

## Rimandato alla Fase 2 (fondamenta di produzione)

- Row-Level Security PostgreSQL come **secondo** livello di isolamento tenant (oggi c'è il filtro
  applicativo per `tenantId`).
- Migrazioni EF definitive al posto di `EnsureCreated`.
- Secret manager/KMS per le chiavi.
