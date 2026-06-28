#!/usr/bin/env python3
"""
Prova end-to-end "12 buste paga -> Certificazione Unica -> conguaglio" contro il backend.

USO:
  1) Avvia il backend:   cd backend && dotnet run --project src/Paghe.Api -c Release
  2) In un altro terminale:   python3 scripts/prova-buste-paga.py
     (URL diverso:            python3 scripts/prova-buste-paga.py http://localhost:5080 )

Non serve installare nulla: usa solo la libreria standard di Python 3.
Crea un'azienda e un dipendente di esempio e genera 12 cedolini. I valori sono ILLUSTRATIVI
(parametri di esempio): vanno validati con un Consulente del Lavoro prima dell'uso reale.
"""
import json, sys, time, urllib.request, urllib.error

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5171"

def call(method, path, body=None, token=None, raw=False):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    if body is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            t = r.read().decode()
            if raw:
                return t
            return json.loads(t) if t and "json" in r.headers.get("Content-Type", "") else t
    except urllib.error.HTTPError as e:
        print(f"ERRORE HTTP {e.code} su {method} {path}: {e.read().decode()[:300]}")
        sys.exit(1)
    except urllib.error.URLError:
        print(f"Backend non raggiungibile su {BASE}. Avvia prima: dotnet run --project src/Paghe.Api")
        sys.exit(1)

reg = call("POST", "/api/auth/registra",
           {"RagioneSociale": "Studio Prova", "Email": f"prova+{int(time.time())}@test.it",
            "Password": "password123"})
token = reg["token"]
print("[1] Account creato.")

az = call("POST", "/api/aziende",
          {"RagioneSociale": "Alfa Srl", "Indirizzo": "Via Roma 1", "Cap": "20100",
           "Comune": "Milano", "Provincia": "MI", "PartitaIva": "01234567890"}, token=token)
print(f"[2] Azienda creata: {az['ragioneSociale']}")

dip = call("POST", f"/api/aziende/{az['id']}/dipendenti",
           {"CodiceFiscale": "RSSMRA80A01F205X", "Nome": "Mario", "Cognome": "Rossi",
            "CodiceCcnl": "COMMERCIO_TERZIARIO", "Livello": "3", "NumeroMensilita": 14,
            "ScattiAnzianita": 1, "Regione": "Lombardia", "CodiceCatastaleComune": "F205"}, token=token)
rid = dip["rapportoId"]
print(f"[3] Dipendente: {dip['nome']} {dip['cognome']}")

print("[4] Genero 12 buste paga:", end=" ")
for m in range(1, 13):
    call("POST", f"/api/rapporti/{rid}/cedolini", {"Anno": 2026, "Mese": m}, token=token)
    print(m, end=" ", flush=True)
print("-> fatto")

cu = call("GET", f"/api/rapporti/{rid}/cu/2026", token=token)
cong = call("GET", f"/api/rapporti/{rid}/conguaglio?anno=2026", token=token)
csv = call("GET", f"/api/aziende/{az['id']}/cu/2026/export.csv", token=token, raw=True)

print("\n=== CERTIFICAZIONE UNICA 2026 ===")
print(f"  {cu['nominativo']} ({cu['codiceFiscale']}) — {cu['numeroCedolini']} cedolini")
print(f"  Imponibile IRPEF: {cu['totaleImponibileIrpef']:.2f}   Ritenute: {cu['totaleRitenuteIrpef']:.2f}")
print(f"  Netto annuo:      {cu['totaleNetto']:.2f}")
print("\n=== CONGUAGLIO FINE ANNO ===")
print(f"  Dovuto {cong['irpefNettaAnnua']:.2f} - trattenuto {cong['irpefGiaTrattenuta']:.2f}"
      f" = {cong['conguaglio']:.2f} ({'a DEBITO' if cong['aDebito'] else 'a CREDITO'})")
print("\n=== CSV CertificazioneUnica ===")
print(csv.strip())

ok = cu["numeroCedolini"] == 12 and "RSSMRA80A01F205X" in csv \
    and abs(cu["totaleRitenuteIrpef"] - cong["irpefGiaTrattenuta"]) < 0.01
print("\nESITO:", "OK — prova superata." if ok else "ATTENZIONE — controlla i valori.")
sys.exit(0 if ok else 1)
