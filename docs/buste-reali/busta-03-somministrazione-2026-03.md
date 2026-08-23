# Busta di riferimento 03 — Somministrazione, Operatore logistico liv. 6, Marzo 2026

> **Confermata dal consulente il 23/08/2026** (estrazione voci e confronto). Resta aperta la
> sola nota sul ragguaglio mensile della detrazione, in fondo alla scheda.

Busta paga **reale** (Zucchetti, agenzia di somministrazione, utilizzatrice logistica del freddo).
Dati personali rimossi (GDPR): si conservano solo inquadramento e numeri.
Archivio di provenienza: cartella di validazione del consulente, 23/08/2026 (file 016).

## Contesto / input
- **Rapporto:** somministrazione a tempo determinato (scadenza 29/08), assunzione 24/03/2025
- **Inquadramento:** Operatore logistico di magazzino, livello 6
- **Paga oraria:** stipendio base 9,61781 + EDR 0,04888 + altri elementi 0,13805 → **9,80474 €/h**
- **Mese:** ore retribuite 170 (lavorate 139), giorni INPS 21, GG detrazione 31

## Voci del mese
| Voce | Ore/GG | Importo |
|---|---|---|
| Retribuzione ordinaria | 139 h × 9,80474 | 1.362,86 |
| Indennità integrativa mensile | 1 | 104,50 |
| Ticket elettronici (esenti, figurativi) | 19 gg × 6,00 | (114,00) |
| Maggiorazione 20% | 8 h | 15,70 |
| Carenza malattia (c/datore) | 31 h | 303,95 |
| Straordinario 30% | 3 h × 12,80728 | 38,42 |
| Straordinario 50% | 6 h × 14,77007 | 88,62 |
| Assenza ingiustificata (figurativa) | 2 h | (19,61) |

## Risultati attesi (i numeri da riprodurre)
| Voce | Valore atteso | Nostro motore | Esito |
|---|---|---|---|
| Totale competenze | 1.914,61 (con arrot.) | — | da modellare |
| Imponibile INPS | 1.914,00 | — | |
| INPS FPLD c/dip 9,19% | 175,90 | **175,90** | ✅ al centesimo |
| FSBS somministrazione TD 0,15% | 2,87 | — | ❌ fondo non modellato (aritmetica ✓) |
| Imponibile fiscale mese | 1.629,94 | **1.629,94** | ✅ la catena quadra: 1.914,05 − 175,90 − 2,87 − 105,34 |
| IRPEF lorda | 374,89 | **374,89** | ✅ al centesimo |
| Detrazione lavoro dip. (mese) | 259,72 | 223,55 | ⚠️ **DIFF 36,17 — da indagare col consulente** |
| IRPEF netta | 115,17 | 151,34 | ⚠️ conseguenza della detrazione |
| Imposta sost. rinnovo CCNL 5% | 5,27 (su 105,34) | — | ❌ istituto non modellato (aritmetica ✓) |
| Add. regionale rata A.P. | 25,19 | — | ⚠️ meccanismo rate |
| Add. comunale rata A.P. + acconto | 10,62 + 3,54 | — | ⚠️ |
| **Netto** | **1.576,00** | — | |

## Nota sulla detrazione (l'unica vera divergenza di calcolo)
La formula di legge annua/12 dà 223,55; Zucchetti espone 259,72. L'analisi dei **sei mesi
consecutivi della stessa lavoratrice** (gen–giu 2026) mostra detrazioni mensili che non sono
funzione del solo imponibile del mese (166,04 compare sia a gennaio con 777,46 sia a maggio
con 1.333,91): Zucchetti applica un **ragguaglio previsionale progressivo** riallineato mese
per mese, legittimo e riassorbito dal conguaglio di dicembre. Il nostro conguaglio annuale
produce lo stesso risultato di legge a fine anno; la differenza è solo nel sentiero mensile.

Nella seconda serie dell'archivio (magazziniere D1, reddito previsionale sopra 20.000) compare
inoltre la colonna **ULTER. DETR. ~84,93/mese ≈ 1.000 €/anno**: è l'ulteriore detrazione del
taglio del cuneo (L.207/2024) per i redditi 20.000–40.000 — **istituto oggi non modellato nel
nostro motore** e necessario per il netto esatto di quei casi.

**Evidenza positiva dalla stessa serie**: giugno (imponibile 3.140,30) conferma al centesimo
il **secondo scaglione 2026 al 33%** già presente nei nostri parametri — primo golden test
reale oltre i 28.000 annualizzati.

## Cosa manca per riprodurla al centesimo
1. **CCNL somministrazione** (paga oraria per livello, indennità integrativa, maggiorazioni).
2. **FSBS** (fondo bilaterale somministrazione, 0,15% TD).
3. **Imposta sostitutiva rinnovi contrattuali** (L.199/25, 5%).
4. **Meccanismo rate addizionali** anno precedente + acconto comunale.
5. Metodo di ragguaglio mensile della detrazione (vedi nota sopra).
