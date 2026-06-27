# Piano a fasi — tutte le dichiarazioni fiscali

Obiettivo: portare il software a gestire **tutti i tipi di dichiarazione**, una fase per volta.
Dettaglio del dominio in [DICHIARAZIONI.md](DICHIARAZIONI.md). Aggiornato 2026-06-27.

## Principi (valgono per ogni dichiarazione)
Ogni dichiarazione si costruisce in **3 livelli successivi**:
- **A) Bozza** — il software calcola/aggrega i dati e mostra anteprima/PDF. *(Fattibile da noi.)*
- **B) File ufficiale** — tracciato telematico esatto dell'Agenzia Entrate (specifiche annuali + software di controllo). *(Serve la spec ufficiale.)*
- **C) Invio** — trasmissione via **Entratel/intermediario** 🔑. *(Serve credenziale esterna.)*

Regola: partiamo sempre dalla **bozza** (valore subito), poi file, poi invio.
⚠️ Ogni valore normativo va **validato con un Consulente del Lavoro/Commercialista**.

---

## FASE 0 — Fondamenta dati (prerequisito) 🟡 in parte
Serve avere i dati reali da cui nascono le dichiarazioni.
- ✅ Dipendenti reali (fatto).
- ⏳ **Cedolini archiviati** reali per dipendente/anno (base per CU e 770).
- ✅ Registri/liquidazioni IVA reali (base per LIPE/IVA annuale).

## FASE 1 — CU (Certificazione Unica) 🎯 prima dichiarazione
Redditi di lavoro dipendente certificati al lavoratore e all'AE.
- 1A **Bozza CU** dai cedolini dell'anno (imponibile IRPEF, ritenute, addizionali, contributi) + PDF. ← **inizio qui**
- 1B File ufficiale CU (tracciato AE annuale).
- 1C Invio telematico CU 🔑.

## FASE 2 — 770 (sostituti d'imposta)
Riepilogo delle ritenute operate e versate (collegato a CU + F24).
- 2A Bozza riepiloghi da cedolini + F24 · 2B file ufficiale · 2C invio 🔑.

## FASE 3 — IVA: LIPE + Dichiarazione IVA annuale
Dati già presenti (liquidazioni IVA).
- 3A Bozza LIPE e riepiloghi annuali · 3B file ufficiali · 3C invio 🔑.

## FASE 4 — 730 / Redditi PF (persona fisica)
Dichiarazione del singolo contribuente.
- 4A Motore dichiarativo IRPEF (oneri, detrazioni, altri redditi) + bozza quadri · 4B file · 4C invio 🔑.
- Nota: modulo nuovo e grande (servono dati che oggi il software non ha).

## FASE 5 — Redditi società + IRAP
Redditi SP/SC/ENC e IRAP (collegati al bilancio fiscale).
- 5A Bozza da bilancio + variazioni fiscali · 5B file · 5C invio 🔑.

## FASE 6 — Minori / specifiche
IMU, INTRASTAT, Esterometro/dati transfrontalieri.
- 6A Anagrafiche/dati mancanti + bozza · 6B file · 6C invio 🔑.

## TRASVERSALE — Canale Entratel (riusabile da tutte)
Come il provider per le fatture SdI: un unico modulo **firma + invio + ricevute** verso
Entratel/Fisconline o intermediario abilitato, usato da tutte le fasi B/C.
- ⚠️ 🔑 Richiede accreditamento/credenziali Entratel o un intermediario: **lo fornisci tu/il commercialista**.
- Riusa l'idea già fatta per SdI (gateway + deleghe + stati ricevute).

---

## Riepilogo fasi
| Fase | Dichiarazione | Bozza (noi) | File ufficiale | Invio 🔑 | Dipende da |
|---|---|---|---|---|---|
| 0 | Fondamenta dati | 🟡 | — | — | — |
| 1 | **CU** | 🎯 ora | ❌ | ❌ | Fase 0 cedolini |
| 2 | 770 | ❌ | ❌ | ❌ | CU + F24 |
| 3 | LIPE / IVA annuale | ❌ | ❌ | ❌ | registri IVA ✅ |
| 4 | 730 / Redditi PF | ❌ | ❌ | ❌ | nuovo motore |
| 5 | Redditi SP/SC + IRAP | ❌ | ❌ | ❌ | bilancio fiscale |
| 6 | IMU / Intrastat / Esterometro | ❌ | ❌ | ❌ | nuovi dati |
| T | Canale Entratel | — | — | 🔑 | accreditamento |

## Come procediamo
Una fase alla volta, ognuna **costruita, testata e committata** prima di passare alla successiva.
Si parte da **Fase 1A — bozza CU dai cedolini**. Le parti 🔑 (file ufficiali + invio) si sbloccano
quando ci sono le specifiche AE e un canale Entratel.
