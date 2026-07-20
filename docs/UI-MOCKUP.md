# Specifica UI per mockup — Paghe SaaS

Documento di riferimento per disegnare i mockup di ogni schermata.
Per ciascuna schermata trovi: **scopo**, **layout**, **elementi/campi**, **azioni** e **stati**.
Le schermate mappano sul modello concettuale descritto in `docs/MODELLO-DATI.md`; il vecchio
`db/schema.sql` è storico e non va eseguito. Lo schema runtime è il modello EF Core.

---

## 0. Design system (basi per il mockup)

**Palette (proposta sobria/professionale, dominio fiscale):**
- Primario: blu istituzionale `#1E3A5F` (header, bottoni primari)
- Accento: verde `#2E7D5B` (esiti positivi, "calcolato", "trasmesso")
- Allerta: ambra `#B7791F` (bozze, scadenze in arrivo)
- Errore: rosso `#C0392B` (scartato, errori di validazione)
- Neutri: grigi `#F7F8FA` (sfondo), `#E2E5EA` (bordi), `#1A1D21` (testo)

**Tipografia:** font sans leggibile (Inter / IBM Plex Sans). Numeri sempre **tabular/monospace** per allineare gli importi.

**Regole trasversali:**
- Importi sempre allineati a destra, 2 decimali, separatore migliaia (es. `1.234,56 €`).
- Ogni schermata ha **breadcrumb** in alto e titolo.
- Stati mostrati con **badge** colorato (bozza / calcolato / chiuso / trasmesso / scartato).
- Tabelle con: ricerca, filtri, ordinamento colonne, paginazione, azioni a riga.

---

## 1. Shell applicativa (telaio comune a tutte le schermate)

**Top bar (barra superiore, sempre visibile):**
- Logo a sinistra
- **Selettore Azienda** (dropdown — il tenant può gestire più aziende/datori di lavoro)
- Barra di ricerca globale (dipendente, azienda, cedolino)
- Icona notifiche (scadenze, elaborazioni completate)
- Menu utente (nome, ruolo, impostazioni, logout)

**Sidebar (menu laterale a sinistra):**
1. 🏠 Dashboard
2. 🏢 Anagrafiche
3. 🗓️ Presenze
4. 📄 Cedolini
5. 📨 Adempimenti
6. 📒 Contabilità
7. 📁 Documenti
8. ⚙️ Impostazioni

**Area contenuto (centro):** breadcrumb + titolo + contenuto della schermata.

---

## 2. Accesso

### 2.1 Login
- Logo + nome prodotto centrati
- Campi: **Email**, **Password**
- Bottone primario **Accedi**
- Link "Password dimenticata?"
- Step MFA (se attivo): campo **Codice a 6 cifre**
- Stati: errore credenziali (banner rosso), account sospeso

### 2.2 Selezione tenant / azienda (se l'utente ne ha più di una)
- Lista a card delle aziende: ragione sociale, P.IVA, n° dipendenti
- Click su una card → entra nel contesto di quell'azienda

---

## 3. Dashboard

**Scopo:** colpo d'occhio sul mese di lavoro.

**Layout:** griglia di card in alto + due colonne sotto.

- **Card KPI (riga in alto):**
  - Dipendenti attivi (numero)
  - Cedolini del mese: elaborati / totali (es. 8/12)
  - Costo del personale del mese (€)
  - Netto da pagare (€)
- **Colonna sinistra — Scadenze:** lista con data, tipo (F24, UniEmens, CU), stato, conto alla rovescia. Badge ambra se in scadenza.
- **Colonna destra — Da fare:** cartellini non chiusi, cedolini in bozza, adempimenti da generare. Ogni voce è cliccabile.
- **Grafico** (opzionale): andamento costo del personale ultimi 6 mesi.

---

## 4. Anagrafiche

### 4.1 Lista Aziende
- Tabella: Ragione sociale · Codice fiscale · P.IVA · Matricola INPS · N° dipendenti · CCNL default · Stato
- Azioni riga: Apri · Modifica
- Bottone **+ Nuova azienda**

### 4.2 Scheda Azienda (con tab)
- **Tab Dati generali:** ragione sociale, CF, P.IVA, matricola INPS, PAT INAIL, CCNL di default, indirizzo, comune, provincia, CAP, email, PEC
- **Tab Sedi di lavoro:** tabella sedi (descrizione, indirizzo, comune, codice catastale) + **+ Nuova sede**
- **Tab Dipendenti:** lista rapporti di lavoro dell'azienda

### 4.3 Lista Dipendenti
- Tabella: Cognome Nome · Codice fiscale · Qualifica · Livello CCNL · Tipo contratto · Data assunzione · Stato (attivo/cessato)
- Filtri: azienda, stato, qualifica
- Bottone **+ Nuovo dipendente**

### 4.4 Scheda Dipendente (con tab) — la più importante delle anagrafiche
- **Tab Anagrafica (persona):** CF, nome, cognome, sesso, data e luogo di nascita, cittadinanza, residenza completa (rileva per addizionali), **IBAN** (campo sensibile, mascherato), email
- **Tab Rapporto di lavoro:** azienda, sede, CCNL, livello, qualifica, tipo contratto, data assunzione/cessazione, part-time (sì/no + %), ore settimanali, retribuzione lorda mensile, n° mensilità (12/13/14), flag detrazioni lavoro dipendente
- **Tab Familiari a carico:** tabella (tipo: coniuge/figlio/altro, CF, % carico, periodo) + **+ Aggiungi familiare**
- **Tab Storico cedolini:** lista cedolini del dipendente con link

---

## 5. CCNL & Parametri normativi (area tecnica / sola lettura per l'operatore)

### 5.1 Lista CCNL
- Tabella: Codice · Descrizione · Settore
- Apri → dettaglio

### 5.2 Dettaglio CCNL (con tab)
- **Tab Livelli:** livello, descrizione, minimo tabellare, contingenza, EDR, validità (dal/al)
- **Tab Scatti anzianità:** importo scatto, mesi maturazione, n° massimo
- **Tab Maggiorazioni:** tipo (straordinario/notturno/festivo), percentuale, validità

### 5.3 Parametri nazionali (versionati per anno) — schermate di consultazione
Selettore **Anno** in alto, poi tab:
- Aliquote INPS (settore, qualifica, % dipendente, % datore, massimale)
- Tassi INAIL (voce tariffa, tasso ‰)
- Scaglioni IRPEF (da/a importo, aliquota)
- Detrazioni IRPEF (tipo, fasce reddito, importi)
- Addizionali regionali e comunali
- Coefficienti rivalutazione TFR (per mese)

> Nota mockup: mostrare un **banner** "Parametri normativi — aggiornati per inserimento, mai modificati: i cedolini storici restano ricalcolabili."

---

## 6. Presenze

### 6.1 Cartellino mensile (vista azienda)
- Selettore **Anno / Mese** + Azienda
- Tabella per dipendente con colonne editabili:
  - Ore ordinarie · Straordinario · Notturno · Festivo · Giorni ferie · Ore permessi/ROL · Giorni malattia · Infortunio · Maternità
- Colonna stato cartellino: **Aperto / Chiuso** (lucchetto)
- Azioni: salva, **Chiudi cartellino** (blocca i dati prima del calcolo)
- Bottone **Importa presenze** (da file/timbrature) — opzionale

### 6.2 Dettaglio cartellino singolo dipendente
- Stessi campi in form verticale + note
- Riepilogo ore totali

---

## 7. Cedolini (cuore operativo)

### 7.1 Elaborazione massiva del mese
- Selettore Anno/Mese + Azienda
- Tabella dipendenti con: Nome · Stato cartellino · Stato cedolino (bozza/calcolato/chiuso) · Lordo · Netto
- Checkbox di selezione multipla
- Bottone primario **▶ Calcola selezionati**
- Barra di avanzamento durante l'elaborazione (coda asincrona)
- Esito a riga: ✅ calcolato / ⚠️ avviso / ❌ errore

### 7.2 Anteprima cedolino (singolo) — schermata chiave del mockup
Layout simile a un cedolino reale:
- **Intestazione:** azienda, dipendente, periodo, qualifica, livello, n° matricola
- **Sezione Competenze** (righe + a destra): paga base, contingenza, scatti, straordinari, ratei 13ª/14ª…
- **Sezione Trattenute** (righe −): contributi INPS c/dipendente, IRPEF netta, addizionali, altre trattenute
- **Riquadri riepilogo:** Imponibile INPS · Imponibile IRPEF · Contributi c/datore · **NETTO IN BUSTA** (grande, evidenziato)
- **Riquadro TFR:** quota del mese, rivalutazione, saldo progressivo
- Badge stato + pulsanti: **Scarica PDF**, **Ricalcola**, **Chiudi cedolino**, **Annulla**

### 7.3 Dettaglio del calcolo (lo "snapshot")
- Pannello espandibile che mostra *come* è stato calcolato: parametri usati (anno aliquote, scaglioni), formule, valori intermedi
- Importante per fiducia/verifiche fiscali

---

## 8. Adempimenti

Vista a **schede/tab**: UniEmens · F24 · Certificazione Unica. Ognuna con flusso di stato: `bozza → generato → trasmesso → ricevuta/scartato`.

### 8.1 UniEmens (mensile)
- Selettore Anno/Mese + Azienda
- Riepilogo: n° dipendenti inclusi, totale imponibile, totale contributi
- Bottone **Genera flusso XML** → stato "generato"
- Protocollo INPS, data trasmissione, badge stato
- Download file XML

### 8.2 F24 (delega di pagamento)
- Testata: anno/mese, data scadenza, totale a debito
- Tabella righe tributo: Sezione (erario/INPS/regioni) · Codice tributo (es. 1001) · Periodo rif. · Debito · Credito
- Bottone **Genera F24** + Download

### 8.3 Certificazione Unica (annuale)
- Selettore Anno
- Tabella per dipendente: imponibile, ritenute, stato
- Bottoni **Genera CU** (per dipendente / massiva) + Download

---

## 9. Contabilità del personale

### 9.1 Piano dei conti
- Tabella: Codice · Descrizione · Tipo (attivo/passivo/costo/ricavo)
- **+ Nuovo conto**

### 9.2 Registrazioni contabili
- Lista: data · causale (es. "Costo del personale 06/2026") · origine (cedolino) · totale
- Dettaglio registrazione: righe **Dare / Avere** per conto
- Indicatore di **quadratura** (totale dare = totale avere) — verde se quadra, rosso se no
- Le registrazioni nascono in automatico dai cedolini chiusi

---

## 10. Documenti

- Archivio file: PDF cedolini, LUL, XML UniEmens, CU, F24
- Tabella: Tipo · Nome file · Periodo · Data · Conservato (sì/no) · Hash
- Filtri per tipo/periodo/dipendente
- Azioni: anteprima, download
- Badge "Conservato a norma" dove applicabile

---

## 11. Impostazioni

- **Utenti e ruoli:** lista utenti (email, ruolo, MFA, attivo) + invito nuovo utente + gestione ruoli (admin/operatore/consulente/sola lettura)
- **Profilo tenant:** ragione sociale, piano (base/pro/studio), stato abbonamento
- **Sicurezza:** attiva/disattiva MFA, sessioni
- **Audit log:** tabella azioni (utente, entità, azione, data) — sola lettura, immutabile

---

## 12. Stati ed elementi ricorrenti (utili nel mockup)

- **Badge di stato:** Bozza (grigio) · Calcolato (blu) · Chiuso (verde) · Trasmesso (verde) · Scartato (rosso) · In scadenza (ambra)
- **Empty state:** illustrazione + "Nessun dipendente ancora. + Aggiungi il primo."
- **Conferme:** modale per azioni irreversibili ("Chiudere il cedolino? Non sarà più modificabile.")
- **Toast:** "Cedolino calcolato", "Flusso generato", "Salvato".

---

## Priorità per i mockup (se vuoi partire da poche schermate)

1. **Dashboard** (sez. 3) — dà l'idea del prodotto
2. **Scheda Dipendente** (sez. 4.4)
3. **Cartellino mensile** (sez. 6.1)
4. **Anteprima cedolino** (sez. 7.2) — la più "wow"
5. **Elaborazione massiva** (sez. 7.1)

Queste 5 raccontano il flusso completo: anagrafica → presenze → calcolo → cedolino.
