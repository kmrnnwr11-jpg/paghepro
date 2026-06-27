# Dichiarazioni fiscali — cosa servirebbe (roadmap onesta)

L'utente vuole che il software faccia **tutte le dichiarazioni** (Certificazione Unica, persona
fisica, ecc.). Qui sta la verità su cosa comporta, cosa il software ha già e cosa manca.
Aggiornato 2026-06-27.

## Come funziona davvero una dichiarazione (3 pezzi, sempre)
Per **ogni** dichiarazione servono tre cose distinte:
1. **Motore di calcolo normativo** — le regole fiscali dell'anno (chi/come/quanto). Alta complessità.
2. **Tracciato telematico ufficiale** — il file nel formato esatto dell'Agenzia Entrate (specifiche annuali + software di controllo ufficiale).
3. **Invio telematico** — 🔑 canale **Entratel/Fisconline** o intermediario abilitato (credenziali, come per le fatture SdI).

> Oggi il software **calcola le paghe e l'IVA** e **prepara dati/bozze**, ma **non genera i file
> ufficiali** delle dichiarazioni né li **trasmette**. I punti 2 e 3 valgono per tutte le voci sotto.

Legenda: ✅ dati già presenti · ⚠️ parziale · ❌ da fare · 🔑 serve credenziale/spec esterna.

## Elenco dichiarazioni (cosa serve per ciascuna)
| Dichiarazione | A cosa serve | Dati nel software | Manca |
|---|---|---|---|
| **CU — Certificazione Unica** | redditi lavoro dip./autonomo certificati | ✅ dai cedolini (imponibili, IRPEF, addizionali, contributi) | tracciato ufficiale + invio 🔑; quadri autonomi |
| **770 — sostituti d'imposta** | riepilogo ritenute versate (F24) | ⚠️ da cedolini + F24 | calcolo riepiloghi + tracciato + invio 🔑 |
| **730 / Redditi PF (persona fisica)** | dichiarazione del singolo contribuente | ❌ (servono oneri, altri redditi, detrazioni) | motore IRPEF dichiarativo + quadri + tracciato + invio 🔑 |
| **Redditi SP / SC / ENC** | società di persone/capitali/enti | ❌ (serve bilancio fiscale, variazioni) | motore reddito d'impresa + tracciato + invio 🔑 |
| **IRAP** | imposta regionale attività produttive | ⚠️ base da costo lavoro/bilancio | calcolo IRAP + tracciato + invio 🔑 |
| **IVA annuale** | riepilogo IVA dell'anno | ✅ da registri/liquidazioni IVA | quadri annuali + tracciato + invio 🔑 |
| **LIPE** | liquidazioni periodiche IVA | ✅ dalle liquidazioni IVA | tracciato ufficiale + invio 🔑 |
| **Esterometro** | operazioni transfrontaliere | ⚠️ da fatture | selezione operazioni + tracciato + invio 🔑 |
| **INTRASTAT** | scambi intra-UE | ❌ | dati dogane + tracciato + invio 🔑 |
| **IMU** | imposta immobili | ❌ (servono immobili) | anagrafica immobili + calcolo + modello |

## Cosa è realistico fare nel software (in ordine di priorità)
1. **CU (bozza dai cedolini)** — il software ha già i dati delle paghe → può comporre la **bozza** dei
   redditi certificati e un **PDF/anteprima**. Manca il file ufficiale + invio 🔑.
2. **LIPE / IVA annuale (bozza)** — dati IVA già presenti → bozza riepiloghi.
3. **770 (bozza)** — da cedolini + F24.
4. Redditi PF/SP/SC, IRAP, IMU, Intrastat → richiedono nuovi motori e dati: progetti grandi a sé.

## In una frase (per il commercialista)
Il software **non sostituisce** il gestionale dichiarativo: può **preparare le bozze** delle
dichiarazioni legate ai dati che già gestisce (paghe→CU/770, IVA→LIPE), ma per i **file ufficiali**
e l'**invio** servono le specifiche annuali dell'Agenzia Entrate e un **canale Entratel** (come per
le fatture SdI). Le altre dichiarazioni (Redditi PF/SP/SC, IRAP, IMU) sono moduli nuovi da costruire.

> Stato infrastruttura: l'area `Paghe.Core/AdeTelematico` sta già crescendo (F24, UniEmens). Le
> dichiarazioni vere vanno aggiunte lì, una per volta, partendo dalla CU.
