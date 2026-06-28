import { useEffect, useState } from "react";
import { api, type Azienda, type IntrastatBozza } from "../api";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const nomePaese: Record<string, string> = {
  AT: "Austria", BE: "Belgio", DE: "Germania", ES: "Spagna", FR: "Francia", NL: "Paesi Bassi",
  PT: "Portogallo", IE: "Irlanda", GR: "Grecia", PL: "Polonia", SE: "Svezia", FI: "Finlandia",
};

export function Intrastat() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [b, setB] = useState<IntrastatBozza | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        let aziende = await api.aziende();
        if (aziende.length === 0) {
          await api.creaAzienda({
            ragioneSociale: "Rossi S.r.l.", partitaIva: "01567890123",
            indirizzo: "Via Roma 10", cap: "20121", comune: "Milano", provincia: "MI",
          });
          aziende = await api.aziende();
        }
        setAzienda(aziende[0] ?? null);
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!azienda) { setB(null); return; }
    api.intrastatBozza(azienda.id, anno)
      .then(setB)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno]);

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Intrastat — cessioni intra-UE</h1>
            <p>Bozza dalle fatture verso clienti UE{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 6 dichiarazioni</p>
          </div>
          <span className="status-pill amber">Bozza</span>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : (
          <>
            <div className="form-grid">
              <label>
                <span>Anno</span>
                <input type="number" min={2000} max={2100} value={anno} onChange={(e) => setAnno(Number(e.target.value))} />
              </label>
            </div>

            {b && (b.righe.length === 0 ? (
              <div className="empty-state">
                Nessuna cessione intracomunitaria per il {b.anno}. Compaiono qui le fatture verso clienti di paesi UE (diversi dall'Italia).
              </div>
            ) : (
              <>
                <div className="table-wrap">
                  <table className="data-table spacious">
                    <thead>
                      <tr><th>Paese UE</th><th className="right">N. operazioni</th><th className="right">Totale imponibile</th></tr>
                    </thead>
                    <tbody>
                      {b.righe.map((r) => (
                        <tr key={r.paese}>
                          <td><strong>{nomePaese[r.paese] ?? r.paese}</strong> <span className="muto num">({r.paese})</span></td>
                          <td className="right num">{r.numeroOperazioni}</td>
                          <td className="right num">{euro(r.totaleImponibile)} €</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="accounting-total">
                  <span>Totale cessioni intra-UE {b.anno}</span>
                  <strong>{euro(b.totaleCessioni)} €</strong>
                  <em>{b.righe.length} paesi</em>
                </div>
              </>
            ))}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Cos'è questa schermata</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          Bozza <b>Intrastat</b>: riepilogo delle cessioni di beni/servizi verso clienti di altri paesi UE.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale Dogane</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio telematico</span><strong>Da fare 🔑</strong></div>
      </aside>
    </div>
  );
}
