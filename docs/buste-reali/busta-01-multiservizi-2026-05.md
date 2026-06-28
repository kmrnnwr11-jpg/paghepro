# Busta di riferimento 01 — Multiservizi, Operaio liv. 3, Maggio 2026

Busta paga **reale** (TeamSystem) usata come *golden reference* per validare il motore.
Dati personali rimossi (GDPR): si conservano solo CCNL, inquadramento e numeri.

## Contesto / input
- **CCNL applicato:** K511 — MULTISERVIZI (pulizie/multiservizi)
- **Livello / qualifica:** 3° — Operaio (a tempo indeterminato)
- **Retribuzione:** ORARIA — tariffa di fatto 8,81867 €/h (ore CCNL 173, gg 26)
- **Scatti anzianità:** 1
- **Comune/Regione (addizionali):** Cadeo (PC) — Emilia-Romagna
- **Periodo:** Maggio 2026
- Elementi paga orari: paga base 5,34532 · contingenza 2,97931 · EDR 0,05971 · ind. territoriale 0,09803 · scatti 0,33630 → **totale 8,81867 €/h**

## Ore del mese
Lavoro ordinario 114 + festività godute 8 + ROL 6 + ferie 16 + ex festività 24 (≈ 168 h) · buoni pasto 13 · premio produzione 260,00

## Risultati attesi (i numeri da riprodurre)
| Voce | Valore atteso |
|---|---|
| Totale competenze | 1.742,16 |
| Imponibile contributivo (mese) | 1.742,00 |
| Contributi INPS c/dipendente | 170,02 |
| Imponibile IRPEF (mese) | 1.523,24 |
| IRPEF lorda | 350,35 |
| Detrazione lavoro dipendente | 292,22 |
| IRPEF netta | 58,13 |
| Add. regionale (rata A.P.) | 37,17 |
| Add. comunale (rata + acconto) | 13,93 + 6,38 |
| Quota/TFR maturato mese | 101,03 |
| **Netto in busta** | **1.276,00** |

## Elementi "reali" presenti (che un motore demo NON modella)
- Calcolo **ORARIO** (non mensile) su CCNL Multiservizi
- **Cessione del quinto** dello stipendio: −177,00 (finanziamento IBL Banca)
- **Detassazione L.199/25** (5%): imponibile 48,28 → imposta 2,41
- **Trattamento integrativo / ulteriore detrazione**: 84,93
- Buoni pasto elettronici, Fondo ASIM, credito FGTFR-INPS, contributi altri enti
- Premio di produzione variabile (260,00)

## Cosa torna già con la nostra logica fiscale
- IRPEF lorda = imponibile × 23% → 1.523,24 × 0,23 = **350,35** ✓
- IRPEF netta = lorda − detrazione lavoro → 350,35 − 292,22 = **58,13** ✓

## Cosa manca per riprodurla al centesimo
1. **Calcolo orario** + **tabelle CCNL Multiservizi** (minimi orari per livello).
2. **Aliquota contributiva Multiservizi** (qui ~9,76% c/dip., non 9,19% del demo).
3. **Elementi extra**: cessione del quinto, detassazione L.199/25, trattamento integrativo, buoni pasto, fondi (ASIM/FGTFR).
