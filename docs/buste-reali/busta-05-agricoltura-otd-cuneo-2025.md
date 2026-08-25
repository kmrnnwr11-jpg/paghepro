# Busta di riferimento 05 — Operaio agricolo OTD (Cuneo), ottobre–dicembre 2025

Tre buste paga **reali** (software INAZ) di un operaio agricolo a tempo determinato, provincia
di **Cuneo**, contratto «3RC3240 Agricoltura OTD», rapporto 30/09/2025 – 31/12/2025. Dati
personali del lavoratore rimossi (GDPR). Fonte: dossier per la conversione del permesso di
soggiorno stagionale, archivio del fondatore, 14/08/2026.

Il dossier contiene anche l'**estratto conto previdenziale INPS** che registra 39 giornate
agricole nel 2025: è la conferma indipendente delle giornate esposte in busta.

## Contesto / input
- **Rapporto:** operaio agricolo **OTD**, tempo determinato, sede INAIL Cuneo
- **Paga oraria:** salario provinciale **6,16000** + terzo elemento **2,40671** = **8,56671 €/h**
- **Datore:** azienda agricola individuale

## I tre mesi
| Mese | Ore | Giornate (GG Lav) | Lordo | Trattenute | Netto |
|---|---|---|---|---|---|
| Ottobre 2025 | 64,00 | 8 | 662,27 (548,27 + una tantum 114,00) | 63,15 | **600,00** |
| Novembre 2025 | 156,00 | 24 | 1.336,41 | 127,45 | **1.209,00** |
| Dicembre 2025 | 45,50 | 7 | 389,79 | 37,21 | **351,66** |
| **Totale** | 265,50 | **39** | 2.388,47 | 227,81 | |

Estratto conto INPS 2025: 39 giornate, retribuzione 2.387,97 (0,50 € sotto la somma delle buste:
l'INPS registra l'imponibile arrotondato all'euro di ogni mese, 390 + 662 + 1.336 = 2.388).

## Trattenute al lavoratore — 9,54% in tutto
Calcolate sull'imponibile **arrotondato all'euro** (389,79 → 390), ogni voce arrotondata al centesimo.

| Codice | Voce | Aliquota |
|---|---|---|
| 32300 | Contributo FAVLA Cuneo | 0,30% |
| 32301 | CAC provinciale Cuneo | 0,20% |
| 40000 | Contributo FAP (IVS) zona normale | 8,84% |
| 45000 | CAC nazionale | 0,20% |
| | **Totale c/lavoratore** | **9,54%** |
| 32301A | CAC provinciale Cuneo **c/azienda** | 0,44% |

Verifica dicembre: 390 × (0,30 + 0,20 + 8,84 + 0,20)% = 1,17 + 0,78 + 34,48 + 0,78 = **37,21** ✔

## Che cosa insegna questa busta (progetto del modulo agricolo)
1. **La paga nasce dalle ore effettive**, non da una retribuzione mensile divisa per 26 o 168:
   il motore ordinario del progetto non è utilizzabile così com'è.
2. **Il terzo elemento** (qui 2,40671 €/h, il 39% del salario provinciale) paga ora per ora
   ferie, festività e mensilità aggiuntive: in busta non compaiono ratei di 13ª e 14ª.
3. **Ore e giornate sono due dati distinti**: 64 ore / 8 giornate a ottobre (8 h al giorno),
   156 ore / 24 giornate a novembre (6,5 h). Le giornate sono ciò che l'INPS registra e da cui
   dipende la disoccupazione agricola: non si ricavano dividendo le ore per un orario fisso.
4. **Imponibile contributivo arrotondato all'euro**, prima di applicare le aliquote.
5. **L'IRPEF è zero** perché il tempo determinato ha una detrazione minima annua di 1.380 €
   (art. 13 c.1 TUIR) che non si rapporta ai giorni. Senza questa regola il motore tratterrebbe
   IRPEF a uno stagionale che non ne deve.
6. **Il reddito annuo presunto è una stima del rapporto** (qui 6.000 €), non il mese × 12:
   annualizzare novembre darebbe 16.000 € a chi ne guadagnerà 2.400.
7. **Il netto si arrotonda all'euro per eccesso** (599,12 → 600,00) e la differenza si trascina:
   l'ultima busta paga il netto esatto meno gli 0,92 già anticipati (352,58 − 0,92 = 351,66).
8. **I contributi a carico dell'azienda non sono in busta**: in agricoltura li calcola l'INPS
   sulla denuncia trimestrale e li chiede con un F24 già compilato.

## Da verificare col Consulente del Lavoro
- **Quota deducibile ai fini fiscali: 7,80%.** L'imponibile fiscale delle tre buste è il lordo
  meno il 7,80% dell'imponibile contributivo — meno del 9,54% trattenuto e meno anche del solo
  FAP (8,84%). Il motore la tiene come **parametro esplicito** (`AliquotaContributiDeducibili`)
  proprio perché non sappiamo ancora derivarla da una regola. Verifiche: ottobre 662,27 − 51,64
  = 610,63 ✔, dicembre 389,79 − 30,42 = 359,37 ✔, novembre 1.336,41 − 104,21 = 1.232,20 contro
  **1.232,21** sulla busta (un centesimo di differenza, arrotondamento da chiarire).
- **Livello di inquadramento**: la busta non lo riporta in chiaro; la tariffa 6,16 €/h è quella
  applicata. Serve la tabella salariale completa del CPL di Cuneo per mappare tutti i livelli.
- Composizione esatta di FAVLA e CAC (nazionale e provinciale) e loro deducibilità.
- Le voci `V91 Imp. zone ex fiscal.nord` e `Contributo FAP Z.NORM.` indicano una **zona
  tariffaria** (normale / montana / svantaggiata): incide sui contributi c/azienda, da modellare
  quando si costruirà la denuncia trimestrale.
