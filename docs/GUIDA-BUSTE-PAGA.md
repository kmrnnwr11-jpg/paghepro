# Guida — fare le buste paga (per le prove)

Tre modi per provare a generare buste paga, dal più semplice. **Avvertenza:** i valori usano
**parametri di esempio** (aliquote, CCNL Commercio) → prima dell'uso reale vanno **validati da un
Consulente del Lavoro**. Le prove servono a vedere che il meccanismo funziona.

Aggiornato al 2026-06-28.

---

## Modo 1 — Prova lampo (1 comando, niente database) ⚡
Mostra a schermo **12 buste paga + Certificazione Unica + conguaglio** per un dipendente di esempio.

```bash
cd backend
dotnet run --project src/Paghe.Demo -c Release
```
Scorri fino a **"ANNO COMPLETO 2026"**. Per provare casi diversi, modifica il dipendente in
[backend/src/Paghe.Demo/Program.cs](../backend/src/Paghe.Demo/Program.cs) (livello, regione,
mensilità, scatti) e rilancia.

---

## Modo 2 — Prova completa col database (come una vera elaborazione) 🧪
Crea davvero azienda + dipendente + 12 cedolini nel database e legge la CU.

**Terminale 1 — avvia il backend:**
```bash
cd backend
dotnet run --project src/Paghe.Api -c Release
```
(Resta in ascolto su <http://localhost:5171>. Usa SQLite di default, oppure Supabase se hai seguito
[GUIDA-SUPABASE.md](GUIDA-SUPABASE.md).)

**Terminale 2 — lancia la prova:**
```bash
python3 scripts/prova-buste-paga.py
```
Vedrai i 12 cedolini generati, la Certificazione Unica, il conguaglio e il CSV. Lo script controlla
da solo che i numeri siano coerenti e stampa **"OK — prova superata"**.

---

## Modo 3 — Dall'interfaccia web (clic, senza comandi tecnici) 🖱️
Per usarlo come lo userebbe un cliente.

**Terminale 1 — backend:**
```bash
cd backend && dotnet run --project src/Paghe.Api -c Release
```
**Terminale 2 — interfaccia web:**
```bash
cd frontend && npm install && npm run dev
```
Apri il link che compare (di solito <http://localhost:5173>) e segui le pagine:
1. **Aziende** → crea l'azienda.
2. **Dipendenti** → aggiungi un dipendente (codice fiscale, CCNL, livello, regione).
3. **Cedolini** → calcola la busta per ogni mese.
4. **Scadenzario / documenti** → consulta CU e promemoria.

---

## Scaricare il PDF della busta paga 📄
Con il backend avviato, l'endpoint `POST /api/cedolini/pdf` restituisce il PDF. Esempio (salva il
file `busta.pdf`):

```bash
curl -s -X POST http://localhost:5171/api/cedolini/pdf \
  -H "Content-Type: application/json" \
  -d '{
        "Rapporto": { "CodiceCcnl": "COMMERCIO_TERZIARIO", "Livello": "3",
                       "Qualifica": "Impiegato", "Regione": "Lombardia",
                       "CodiceCatastaleComune": "F205" },
        "Cartellino": { "Anno": 2026, "Mese": 6 },
        "Azienda": "Alfa Srl", "Dipendente": "Mario Rossi"
      }' -o busta.pdf
echo "creato busta.pdf"
```

(Apri `busta.pdf` con il visualizzatore PDF.)

---

## Cosa puoi provare oggi vs cosa no (onesto)
| Funzione | Stato |
|---|---|
| 12 buste paga + Certificazione Unica (CU) | ✅ funziona, provato end-to-end |
| Conguaglio fiscale di fine anno | ✅ funziona |
| PDF della busta paga e del LUL | ✅ disponibile |
| Redditi PF / "Unico" persona fisica | 🛠️ modulo in costruzione |
| **Presentare/inviare** all'Agenzia Entrate | ❌ non dal software (serve Entratel / Desktop Telematico) |

> Promemoria: i numeri sono **di esempio**. Per buste reali servono parametri/CCNL **validati da un
> Consulente del Lavoro**.
