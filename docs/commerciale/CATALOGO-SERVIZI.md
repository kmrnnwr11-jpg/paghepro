# Catalogo servizi — la visione "tutto in un posto"

Tutto il possibile che una piattaforma unica per la contabilità d'impresa può offrire: il
catalogo completo dei servizi, dal conto in banca alle dichiarazioni, dalle paghe all'AI.
L'obiettivo è il **"one-stop shop"**: l'imprenditore (e il suo commercialista) trova **tutto qui**.

Aggiornato al 2026-06-28. Da leggere con:
- [docs/PIANO-INTEGRAZIONE.md](../PIANO-INTEGRAZIONE.md) — come si collega tecnicamente ogni cosa
- [docs/PIANO-COMPLETAMENTO.md](../PIANO-COMPLETAMENTO.md) — gap fatto/da fare
- [docs/commerciale/POSIZIONAMENTO.md](POSIZIONAMENTO.md) — a chi vendiamo

## Come leggere (legenda)

Non si costruisce tutto: molte cose (conto, carte, prestiti, assicurazioni) **richiedono licenze**
e si offrono tramite **partner**. Per ogni servizio:

- 🔨 **Costruisci** — lo facciamo noi nel software (è il "nostro" valore)
- 🤝 **Partner/Provider** — serve un soggetto accreditato/licenziato (banca, fintech, assicuratore)
- 🔗 **Integrazione** — esiste già altrove, ci colleghiamo via API/file
- 🟢 = già presente nel progetto · 🔴 = priorità alta · 🟠 = media · ⚪ = dopo

> **Strategia in una riga:** parti dal **core che costruisci** (fatture, contabilità, paghe,
> dichiarazioni) e aggiungi i **mondi finanziari/accessori via partner** trasformando la piattaforma
> in un **marketplace**. Non serve la licenza bancaria per offrire il conto: basta l'accordo giusto.

---

## 🌍 Mondo 1 — Conto & Pagamenti (fintech)

| Servizio | Tipo | Note |
|---|---|---|
| 🔴 Conto business + **IBAN** | 🤝 | Via banca/EMI partner (modello Qonto/Tot/Finom) o "conto connesso" via Open Banking |
| 🟠 **Carte** aziendali (fisiche/virtuali) | 🤝 | Emesse dal partner, gestite nella tua UI |
| 🔴 **Pagamento F24 / PagoPA** | 🔗 | Da conto collegato (CBI) o tramite partner |
| 🔴 **Bonifici** (stipendi, fornitori) | 🔗 | File SEPA/CBI `pain.001` o disposizione via partner |
| 🟠 **Incassi**: link di pagamento, POS virtuale, PSD2 pay-by-bank | 🤝 | Provider pagamenti (Stripe/Nexi/PSP) |
| 🟠 **Riconciliazione incassi/pagamenti** automatica | 🔨 | Abbina movimenti ↔ fatture (vedi PIANO-INTEGRAZIONE blocco A) |
| ⚪ **Multivaluta** / cambio | 🤝 | Conti FX del partner |

## 🌍 Mondo 2 — Fatturazione & Documenti

| Servizio | Tipo | Note |
|---|---|---|
| 🟢 Fattura elettronica **attiva** + SdI | 🔨 | Già fatto (FatturaPA + validazione + trasmissione) |
| 🔴 Fatture **passive** (ciclo acquisti) | 🔗 | Scarico via provider SdI (A-Cube) |
| 🔴 **Corrispettivi** telematici / RT | 🔗 | Letti dal Portale via provider |
| 🟠 Preventivi, ordini, **DDT**, proforma | 🔨 | Documenti commerciali a monte della fattura |
| 🟠 **Fatturazione ricorrente** / abbonamenti | 🔨 | Subscription billing automatico |
| 🟠 **Note spese** + **OCR scontrini** | 🔨/🔗 | Foto → estrazione dati → registrazione |
| 🔴 **Conservazione a norma** (10 anni) | 🤝 | Provider conservazione (A-Cube/InfoCert/Aruba) |
| 🟠 **Verifica P.IVA/VIES** anagrafiche | 🔗 | Controllo automatico clienti/fornitori |

## 🌍 Mondo 3 — Contabilità & Bilancio

| Servizio | Tipo | Note |
|---|---|---|
| 🔴 **Prima nota** + registrazioni automatiche da fatture/cedolini | 🔨 | Cuore della contabilità |
| 🔴 **Piano dei conti** + partitari clienti/fornitori | 🔨 | |
| 🟢 **Liquidazione IVA** + registri | 🔨 | Già presente (`LiquidazioneIva`) |
| 🟠 **Cespiti & ammortamenti** | 🔨 | |
| 🟢 **Bilancio CEE** (SP + CE) | 🔨 | Già persistito; manca nota integrativa + **XBRL** |
| 🟠 **Deposito XBRL** Registro Imprese | 🔗 | Tassonomia ITCC + InfoCamere |
| 🟠 Contabilità **analitica** / per commessa | 🔨 | |

## 🌍 Mondo 4 — Tasse & Dichiarazioni

| Servizio | Tipo | Note |
|---|---|---|
| 🟠 **Redditi** (PF/SP/SC/ENC), **IRAP** | 🔨+🔗 | Genera → controllo JAR → invio |
| 🟠 **730** (precompilata, 730/4) | 🔨+🔗 | |
| 🟢 **F24** (tracciato ufficiale) | 🔨 | Già fatto e validato |
| 🟢 **Certificazione Unica** | 🔨 | Già presente; estendere autonomo |
| 🟠 **770** | 🔨+🔗 | JAR già scaricato |
| 🟠 **Dichiarazione IVA annuale**, **Intrastat**, **esterometro** | 🔨 | Esterometro già via SdI (TD17/18/19) |
| 🟠 **ISA + CPB 2026-2027** | 🔨+🔗 | Bozza nostra, valore vincolante dal SW AdE |
| 🔴 **Ravvedimento operoso** (sanzioni+interessi→F24) | 🔨 | |
| 🟠 **Simulatore/pianificazione tasse** + acconti | 🔨 | "Quanto pagherò?" — fortissimo per microimprese |
| 🟠 **Scadenzario fiscale** automatico | 🟢🔨 | Già c'è lo scadenzario; estendere alle imposte |

## 🌍 Mondo 5 — Paghe & Risorse Umane (HR completo)

| Servizio | Tipo | Note |
|---|---|---|
| 🟢 **Cedolino** lordo→netto | 🔨 | Già fatto (1 CCNL); estendere multi-CCNL |
| 🟢 **UniEmens**, F24 da paghe | 🔨 | Già presente |
| 🔴 **TFR**, **conguaglio** fine anno, **13ª/14ª** | 🔨 | |
| 🔴 **LUL** (Libro Unico) | 🔨 | |
| 🟠 **Malattia/maternità/infortunio**, **CIG**, **ANF/Assegno Unico** | 🔨 | |
| 🟠 **Autoliquidazione INAIL** | 🔨+🔗 | |
| 🟠 **Presenze / timbrature / turni** | 🔨 | + app mobile |
| 🟠 **Ferie & permessi** (richiesta/approvazione) | 🔨 | Self-service dipendente |
| 🟠 **Note spese trasferte / rimborsi km** | 🔨 | |
| 🟠 **Welfare aziendale** / fringe benefit / buoni pasto | 🤝 | Partner welfare |
| ⚪ **Onboarding**, **recruiting**, **valutazione performance** | 🔨 | HR "soft" |
| ⚪ **Formazione** / corsi sicurezza | 🤝 | Partner formazione |
| 🟠 **Fascicolo dipendente** (contratti, certificati) | 🔨 | DMS dedicato |

## 🌍 Mondo 6 — Gestione Business (operations)

| Servizio | Tipo | Note |
|---|---|---|
| 🟠 **CRM** clienti/fornitori | 🔨 | Anagrafiche + opportunità |
| 🟠 **Magazzino / inventario** | 🔨 | Per chi vende beni |
| 🟠 **Listini / preventivi / ordini** | 🔨 | |
| 🟠 **Commesse / progetti / timesheet** | 🔨 | Per servizi/professionisti |
| 🟠 **Gestione contratti** + scadenze | 🔨 | |
| 🟠 **Solleciti / recupero crediti** automatici | 🟢🔨 | Già c'è `AvvisiPagamento`; estendere |
| ⚪ **E-commerce / marketplace** sync | 🔗 | Shopify/WooCommerce → fatture |
| ⚪ **Parcellazione** (studi professionali) | 🔨 | Fatturazione dello studio |

## 🌍 Mondo 7 — Finanza & Credito

| Servizio | Tipo | Note |
|---|---|---|
| 🔴 **Riconciliazione bancaria** (Open Banking) | 🔗 | Via aggregatore PSD2 |
| 🟠 **Cash flow / previsione di cassa** | 🔨 | Modello Agicap; molto richiesto |
| 🟠 **Tesoreria** / budget di cassa | 🔨 | |
| ⚪ **Anticipo fatture / factoring / SBF** | 🤝 | Partner credito; smobilizzo crediti |
| ⚪ **Finanziamenti / prestiti / fido** | 🤝 | Marketplace credito |
| ⚪ **Rating / merito creditizio** (EBA LOM) | 🔗 | Calcolo indicatori + dati esterni |

## 🌍 Mondo 8 — Compliance & Legale

| Servizio | Tipo | Note |
|---|---|---|
| 🟠 **Antiriciclaggio** (D.Lgs. 231) | 🔨 | Adeguata verifica clientela per studi |
| 🟠 **Codice della crisi** / **adeguati assetti** + early warning | 🔨 | Obbligo SRL; indicatori da contabilità (modello DigitalCFO) |
| 🟠 **Privacy / GDPR**: registro trattamenti, informative | 🔨 | + generatore documenti |
| ⚪ **Sicurezza sul lavoro** (DVR, D.Lgs. 81/08) | 🤝 | Partner sicurezza |
| 🔗 **Visure camerali / protesti / bilanci** terzi | 🔗 | InfoCamere/cribis per check clienti |
| 🟢 **Conservazione & firma** | 🔨/🤝 | Firma CAdES/CNS già fatta; conservazione via partner |

## 🌍 Mondo 9 — Analisi & Controllo di gestione

| Servizio | Tipo | Note |
|---|---|---|
| 🟠 **Dashboard KPI** / business intelligence | 🔨 | Fatturato, incassi, margini, scadenze |
| 🟠 **Budget & forecast** | 🔨 | |
| 🟠 **Analisi marginalità** per cliente/prodotto/commessa | 🔨 | |
| ⚪ **Benchmark di settore** | 🔗 | Confronto con dati aggregati |
| 🟠 **Report personalizzabili** + export | 🔨 | |

## 🌍 Mondo 10 — AI & Automazione

| Servizio | Tipo | Note |
|---|---|---|
| 🟠 **Assistente AI contabile/fiscale** (chat) | 🔨 | "Posso dedurre questa spesa?" |
| 🟠 **Registrazione automatica fatture** (categorizzazione AI) | 🔨 | Riduce data-entry |
| 🟠 **OCR documenti** (scontrini, fatture cartacee) | 🔗 | |
| ⚪ **Previsioni** (incassi, cash flow) AI | 🔨 | |
| ⚪ **Chatbot clienti** dello studio | 🔨 | |

## 🌍 Mondo 11 — Identità, Firma, Conservazione

| Servizio | Tipo | Note |
|---|---|---|
| 🟠 **Login SPID/CIE/CNS** | 🤝 | Via aggregatore identità |
| 🟢 **Firma digitale** (CAdES/XAdES/CNS) | 🔨 | Già fatto (`Paghe.Firma`) |
| 🤝 **Firma remota** + **marche temporali** | 🤝 | Provider firma |
| 🤝 **PEC / domicilio digitale** | 🤝 | InfoCert/Aruba/Namirial |
| 🔨 **2FA / MFA** | 🔨 | Sopra al JWT esistente |

## 🌍 Mondo 12 — Avvio & Pratiche

| Servizio | Tipo | Note |
|---|---|---|
| 🟢 **Apertura P.IVA** (checklist + dati) | 🔨 | Già presente (`PartiteIva`) |
| 🟠 **ComUnica / Registro Imprese / REA** | 🔗 | Canale InfoCamere |
| ⚪ **SUAP / SCIA** | 🔗 | ComUnica con SCIA contestuale |
| ⚪ **Bandi & finanza agevolata** | 🔗/🤝 | Marketplace incentivi |

## 🌍 Mondo 13 — Marketplace & Servizi accessori

| Servizio | Tipo | Note |
|---|---|---|
| ⚪ **Assicurazioni** business / RC professionale | 🤝 | Insurtech partner |
| ⚪ **Sede legale / domiciliazione** | 🤝 | |
| ⚪ **Sito web / presenza online** | 🤝 | |
| ⚪ **Marketplace di commercialisti/consulenti** | 🔨 | Matching cliente↔professionista (leva di vendita!) |
| ⚪ **Formazione / academy** | 🔨/🤝 | |

## 🌍 Mondo 14 — Verticali di settore (pacchetti)

Lo stesso software, **preconfigurato** per una nicchia (è la via per crescere senza fare tutto):

- Forfettari / freelance · Professionisti / studi · E-commerce · Ristorazione & retail ·
  Edilizia & cantieri · Agricoltura · Associazioni / ETS / no-profit · Artigiani

---

## 🎯 Sintesi strategica (la cosa importante)

1. **Il "tutto in un posto" non significa "costruisci tutto".** Significa che l'utente **trova
   tutto qui**: parte lo costruisci (🔨), parte la colleghi (🔗), parte la rivendi via partner (🤝).
2. **Il tuo cuore difendibile** = contabilità + paghe + dichiarazioni (🔨). Lì sei un prodotto, non
   un rivenditore. È dove i microimprenditori soffrono e i big sono cari/complessi.
3. **I mondi finanziari** (conto, carte, credito, assicurazioni) si aggiungono **via partner**:
   ricavi extra senza licenze bancarie. È il modello Qonto/Tot/Finom al contrario — loro partono
   dal conto e aggiungono le fatture; **tu parti dalle fatture/paghe e aggiungi il conto**.
4. **Ordine consigliato:** Mondi 2-3-4-5 (core) → 7 (banca/cash) → 1 (conto partner) → 8-9-10
   (compliance/BI/AI) → 11-12-13 (accessori) → 14 (verticali per scalare).
5. **Non promettere mai** un servizio che richiede licenza come se fosse tuo: dichiara sempre
   "in collaborazione con \[partner]". Tiene il prodotto onesto e legale.

> Coerente col principio del progetto: **genera → valida → controllo ufficiale → invia via canali
> ufficiali**, mai login simulati. Vale anche qui: i mondi 1/7/11 passano da soggetti accreditati.
