# Concorrenti — chi sono e dove siamo noi

Mappa dei software di contabilità/paghe italiani concorrenti e **confronto onesto** con il nostro
prodotto: per ogni funzione, **ce l'hai già · da fare · non serve (partner)**.

Aggiornato al 2026-06-28. Da leggere con:
- [docs/commerciale/CATALOGO-SERVIZI.md](CATALOGO-SERVIZI.md) — tutto il possibile (14 mondi)
- [docs/PIANO-INTEGRAZIONE.md](../PIANO-INTEGRAZIONE.md) — come si collega ogni cosa
- [docs/PIANO-COMPLETAMENTO.md](../PIANO-COMPLETAMENTO.md) — gap fatto/da fare

Legenda stato nostro: 🟢 **già fatto** · 🟡 **parziale/bozza** · 🔴 **da fare** · 🤝 **via partner** · ⚪ **non serve ora**

---

## 1. I concorrenti per fascia

### A. I 6 colossi (suite complete per studi)
Coprono **tutto** (contabilità + dichiarativi + bilanci + paghe + fatturazione). Cari e complessi.

| Software house | Prodotto | Forza principale |
|---|---|---|
| **TeamSystem** | Studio / Studio AI, Fatture in Cloud, Enterprise, Alyante | Il più diffuso; possiede Fatture in Cloud (580k+ P.IVA) |
| **Zucchetti** | AGO Infinity, Paghe Infinity/Project, Mago4 | Leader assoluto sulle **paghe** |
| **Wolters Kluwer** | B.Point, Genya, Giotto/Osra (paghe), Arca | 8.000+ studi clienti |
| **Sistemi** | Profis, STUDIO, JOB (paghe) | Storico, bilanci XBRL |
| **Passepartout** | Passcom + Mexal | Cloud integrato col gestionale azienda |
| **GBsoftware** | INTEGRATO GB | Tutto via web, buon prezzo |

Altri completi: Datev Koinos · Buffetti (eBridge/Expert Up) · Ranocchi · Dylog · Versya · Blustring.

### B. Specialisti paghe
Zucchetti Paghe · TeamSystem Paghe · Inaz · **Sistemi JOB** · **eLaborHub** (TeleConsul) ·
**SEAC Paghe Web** · **PAGHE GB** · **Fluida** (a consumo) · **Jet HR** (HR, non sostituisce il motore).

### C. Cloud leggeri (micro-imprese / forfettari) — i nostri rivali diretti
Fatture in Cloud · Danea Easyfatt · Aruba · Fattura24 · Software Semplice · Finom ·
Fatturhello · SumUp · Forfè Manager.

### D. Fintech "all-in-one" (conto + fatture)
**Qonto · Tot · Finom** — partono dal conto bancario e aggiungono fatture/spese/F24.

---

## 2. Confronto funzione per funzione (loro vs noi)

> "I colossi" = lo coprono praticamente tutti i 6 grandi. La colonna **Noi** è lo stato reale del progetto.

### Fatturazione
| Funzione | I colossi | I cloud leggeri | Noi |
|---|---|---|---|
| Fattura elettronica attiva + SdI | ✅ | ✅ | 🟢 fatto (FatturaPA + validazione XSD + controlli SdI) |
| Trasmissione SdI (modem) | ✅ | ✅ | 🟢 fatto (gateway A-Cube, serve account) |
| Validazione XSD + controlli semantici SdI | ✅ | parziale | 🟢 fatto (spesso **meglio** dei leggeri) |
| Fatture passive (acquisti) | ✅ | parziale | 🔴 da fare |
| Corrispettivi / RT | ✅ | alcuni | 🔴 da fare |
| Note credito/debito, reverse charge, bollo, ritenute | ✅ | parziale | 🔴 da fare |
| Conservazione a norma 10 anni | ✅ | ✅ | 🤝 via partner (A-Cube/InfoCert) |

### Contabilità & Bilancio
| Funzione | I colossi | Noi |
|---|---|---|
| Liquidazione IVA + registri | ✅ | 🟢 fatto (`LiquidazioneIva` + registri periodo) |
| Prima nota + registrazioni automatiche | ✅ | 🔴 da fare |
| Piano dei conti + partitari | ✅ | 🟡 parziale (piano conti nel bilancio) |
| Cespiti & ammortamenti | ✅ | 🔴 da fare |
| Bilancio CEE (SP+CE) | ✅ | 🟢 fatto e persistito (stati Bozza→Depositato) |
| Deposito XBRL Registro Imprese | ✅ | 🔴 da fare |
| Dichiarazione IVA annuale / esterometro / Intrastat | ✅ | 🔴 da fare (esterometro via TD SdI: 🟡) |

### Dichiarativi fiscali
| Funzione | I colossi | Noi |
|---|---|---|
| F24 (tracciato ufficiale) | ✅ | 🟢 fatto e **validato col JAR ufficiale** (Erario+Regioni+IMU) |
| Certificazione Unica (CU) | ✅ | 🟡 avviata (`CertificazioneUnicaService`) |
| 770 | ✅ | 🔴 da fare (JAR già scaricato) |
| Redditi PF/SP/SC + IRAP | ✅ | 🔴 da fare |
| 730 (precompilata, 730/4) | ✅ | 🔴 da fare |
| ISA + CPB 2026-2027 | ✅ | 🔴 da fare |
| Ravvedimento operoso → F24 | ✅ | 🔴 da fare (riusa modulo F24) |

### Paghe & HR
| Funzione | I colossi | Noi |
|---|---|---|
| Cedolino lordo→netto | ✅ (tutti i CCNL) | 🟢 fatto (1 CCNL Commercio) |
| Multi-CCNL (400+) | ✅ | 🔴 da fare (il gap più grosso) |
| UniEmens | ✅ | 🟡 bozza (da far passare dal controllo INPS) |
| F24 da paghe | ✅ | 🟢 fatto |
| TFR (accantonamento/rivalutazione/tassazione) | ✅ | 🔴 da fare |
| Conguaglio fine anno + 730/4 | ✅ | 🔴 da fare |
| LUL (Libro Unico) | ✅ | 🔴 da fare |
| Malattia/maternità/CIG/ANF | ✅ | 🔴 da fare |
| Autoliquidazione INAIL | ✅ | 🔴 da fare |
| Presenze/timbrature/ferie | ✅ | 🔴 da fare |

### Integrazioni & canali ufficiali
| Funzione | I colossi | Noi |
|---|---|---|
| Cassetto fiscale / Fatture e Corrispettivi | ✅ | 🔴 da fare (via provider) |
| Cassetto previdenziale INPS / DURC | ✅ | 🟡 archivio DURC c'è; lettura automatica 🔴 |
| Open Banking / riconciliazione bancaria | ✅ | 🔴 da fare (via aggregatore PSD2) |
| Flussi CBI (F24/bonifici) | ✅ | 🔴 da fare |
| Login SPID/CIE/CNS | ✅ | 🔴 da fare (🤝 aggregatore) |
| Firma digitale (CAdES/XAdES/CNS) | ✅ | 🟢 fatto (`Paghe.Firma`) |
| Aggiornamento normativo automatico | ✅ | 🟡 parametri su DB c'è; pipeline annuale 🔴 |

### Sicurezza & GDPR
| Funzione | I colossi | Noi |
|---|---|---|
| Auth + multi-tenant | ✅ | 🟢 fatto (JWT + tenant da claim) |
| Cifratura dati sensibili (IBAN/malattia) | ✅ | 🟢 fatta (AES-GCM + cutover verificato) |
| Audit log | ✅ | 🟢 fatto: letture, operazioni, autenticazione e modifiche tenant-safe |
| Retention / diritto all'oblio | ✅ | 🟡 anonimizzazione fatta; policy/job completi da fare |
| 2FA/MFA | ✅ | 🟢 fatto (TOTP) |

### Gestione studio & extra
| Funzione | I colossi | Noi |
|---|---|---|
| Scadenzario / promemoria | ✅ | 🟢 fatto (DURC+F24, multi-canale) |
| Apertura P.IVA / ComUnica | alcuni | 🟢 checklist fatta; invio InfoCamere 🔴 |
| Antiriciclaggio (231) | ✅ | ⚪ dopo |
| CRM / magazzino / commesse | ✅ | ⚪ dopo |
| Conto/carte/credito/assicurazioni | alcuni (TeamSystem/fintech) | 🤝 via partner |
| App mobile | ✅ | 🔴 da fare |

---

## 3. Lettura strategica

**Dove sei già competitivo (anche contro i grandi):**
- 🟢 Fatturazione elettronica con **validazione XSD + controlli SdI** seria — meglio dei tool leggeri
- 🟢 **F24 col tracciato ufficiale validato** dal JAR AdE — roba che i "Fattura24/Aruba" non hanno
- 🟢 **Firma digitale CAdES/XAdES/CNS** nativa
- 🟢 Bilancio CEE persistito, scadenzario, trasmissione SdI

**I 4 gap che ti separano dal vendere a un commercialista (priorità):**
1. 🔴 **Multi-CCNL + paghe avanzate** (TFR, conguaglio, LUL, malattia) — il cuore mancante
2. 🔴 **Dichiarativi** (Redditi/730/770/IVA annuale/ISA-CPB)
3. 🔴 **Integrazioni canali ufficiali** (cassetto, Fatture e Corrispettivi, Open Banking)
4. 🔴 **GDPR residuo** (retention, documenti, audit infrastrutturale e chiavi/backup di produzione)

**Dove NON inseguire i colossi (è una trappola):**
- ⚪ Coprire **tutti i 400+ CCNL** e **tutti** gli adempimenti = lavoro di anni → resta su una **nicchia verticale**
- 🤝 Conto/carte/factoring/assicurazioni = **partner**, non costruire

> **Posizionamento vincente:** non "un altro TeamSystem", ma il **tutto-in-uno semplice e onesto
> per la microimpresa** — fattura + paghe base + F24 + scadenze già integrati, a un prezzo che i
> colossi non fanno e con funzioni serie che i tool leggeri non hanno. Il conto e il credito si
> aggiungono via partner (modello Qonto al contrario). Vedi [CATALOGO-SERVIZI.md](CATALOGO-SERVIZI.md).

---

*Fonti ricerca concorrenti: Qonto, Commercialista Telematico, TeamSystem, Zucchetti, Jet HR,
Fluida, SRLonline, GBsoftware, Punto Informatico, Tot, Finom, Agicap, Zucchetti DigitalCFO (giu 2026).*
