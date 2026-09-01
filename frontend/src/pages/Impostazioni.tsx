import { useEffect, useState } from "react";
import { ExternalLink, Settings2, Sparkles } from "lucide-react";
import { api } from "../api";
import {
  CHIAVE_RISPOSTE_BREVI_AURORA,
  STILI_BUSTA,
  salvaStileBusta,
  stileBustaPreferito,
  type StileBusta,
} from "../preferenze";

const ETICHETTE_STILE: Record<StileBusta, string> = {
  classico: "Classico (prospetto professionale)",
  colori: "Colori (riquadri colorati)",
  editoriale: "Editoriale (impaginazione estesa con guida)",
};

/** Collegamenti alle pagine di configurazione già esistenti: la SPA rispetta l'hash. */
const COLLEGAMENTI: { hash: string; titolo: string; descrizione: string }[] = [
  { hash: "aziende", titolo: "Scheda azienda", descrizione: "Dati fiscali, INPS/INAIL, CCNL, logo dei documenti" },
  { hash: "collega-sdi", titolo: "Collega Fatturazione", descrizione: "Credenziali del canale SdI" },
  { hash: "utenti", titolo: "Utenti e permessi", descrizione: "Chi può fare cosa nello studio" },
  { hash: "audit", titolo: "Registro attività", descrizione: "Chi ha letto o modificato cosa" },
  { hash: "retention", titolo: "Conservazione dati", descrizione: "Cancellazione e retention GDPR" },
  { hash: "scadenzario", titolo: "Scadenze e promemoria", descrizione: "DURC, F24 e messaggi precompilati" },
];

export function Impostazioni() {
  const [stile, setStile] = useState<StileBusta>(() => stileBustaPreferito());
  const [risposteBrevi, setRisposteBrevi] = useState(() => {
    try { return localStorage.getItem(CHIAVE_RISPOSTE_BREVI_AURORA) === "1"; } catch { return false; }
  });
  const [ai, setAi] = useState<{ provider: string; attivo: boolean; consumoMese: number; limiteMensile: number } | null>(null);

  useEffect(() => {
    let attivo = true;
    api.statoAi().then((stato) => { if (attivo) setAi(stato); }).catch(() => { /* solo informativo */ });
    return () => { attivo = false; };
  }, []);

  function cambiaStile(valore: string) {
    if ((STILI_BUSTA as readonly string[]).includes(valore)) {
      setStile(valore as StileBusta);
      salvaStileBusta(valore);
    }
  }

  function cambiaRisposteBrevi(valore: boolean) {
    setRisposteBrevi(valore);
    try { localStorage.setItem(CHIAVE_RISPOSTE_BREVI_AURORA, valore ? "1" : "0"); } catch { /* facoltativa */ }
  }

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Impostazioni</h1>
          <p>Preferenze d'uso e collegamenti alla configurazione dello studio</p>
        </div>
      </div>

      <div className="form-grid">
        <label>
          <span><Settings2 size={14} aria-hidden="true" /> Stile predefinito della busta paga (PDF)</span>
          <select value={stile} onChange={(e) => cambiaStile(e.target.value)}>
            {STILI_BUSTA.map((valore) => (
              <option key={valore} value={valore}>{ETICHETTE_STILE[valore]}</option>
            ))}
          </select>
          <small style={{ display: "block", marginTop: 4, opacity: .7 }}>
            Vale come proposta iniziale nella pagina Buste paga; i numeri sono identici nei tre
            stili. I cedolini chiusi restano nel PDF congelato alla chiusura.
          </small>
        </label>

        <label>
          <span><Sparkles size={14} aria-hidden="true" /> Aurora · risposte brevi</span>
          <select value={risposteBrevi ? "1" : "0"} onChange={(e) => cambiaRisposteBrevi(e.target.value === "1")}>
            <option value="0">Risposte normali</option>
            <option value="1">Risposte brevi (2-3 frasi)</option>
          </select>
          <small style={{ display: "block", marginTop: 4, opacity: .7 }}>
            Cambia solo lo stile delle risposte, mai permessi o strumenti. Il pannello Aurora la
            applica alla prossima apertura. Il consenso pieno si gestisce dal pannello Aurora.
          </small>
        </label>

        <div className="empty-state wide-field">
          {ai
            ? <>Assistente AI: <strong>{ai.attivo ? ai.provider : "spento"}</strong>
              {ai.attivo && <> · consumo del mese {ai.consumoMese}/{ai.limiteMensile === 0 ? "illimitato" : ai.limiteMensile}</>}.
              Le preferenze di questa pagina restano su questo dispositivo.</>
            : "Le preferenze di questa pagina restano su questo dispositivo."}
        </div>
      </div>

      <div className="card-title-row" style={{ marginTop: 22 }}>
        <div>
          <h2>Configurazione dello studio</h2>
          <p>Ogni voce apre la pagina dedicata dove i dati si modificano e si salvano davvero.</p>
        </div>
      </div>
      <div className="settings-grid">
        {COLLEGAMENTI.map((voce) => (
          <article className="settings-card" key={voce.hash}
            style={{ cursor: "pointer" }}
            onClick={() => { window.location.hash = `#${voce.hash}`; }}>
            <div>
              <strong>{voce.titolo}</strong>
              <span>{voce.descrizione}</span>
            </div>
            <em className="status-pill blue"><ExternalLink size={12} aria-hidden="true" /> Apri</em>
          </article>
        ))}
      </div>
    </section>
  );
}
