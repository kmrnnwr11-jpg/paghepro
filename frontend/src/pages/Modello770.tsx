import { useEffect, useState } from "react";
import { api, type Azienda, type Mod770Bozza } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function Modello770() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [m770, setM770] = useState<Mod770Bozza | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const aziende = await api.aziende();
        setAzienda(scegliAziendaPreferita(aziende));
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!azienda) { setM770(null); return; }
    api.mod770Bozza(azienda.id, anno)
      .then(setM770)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno]);

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Modello 770 — sostituto d'imposta</h1>
            <p>Bozza ritenute operate dai cedolini{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 2 dichiarazioni</p>
          </div>
          <span className="status-pill amber">Bozza</span>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : (
          <>
            <div className="form-grid">
              <label>
                <span>Anno d'imposta</span>
                <input type="number" min={2000} max={2100} value={anno} onChange={(e) => setAnno(Number(e.target.value))} />
              </label>
            </div>

            {m770 && (
              <div className="payroll-result">
                <div className="result-summary">
                  <div>
                    <span>Ritenute IRPEF (anno)</span>
                    <strong className="num">{euro(m770.totaleRitenuteIrpef)} €</strong>
                  </div>
                  <div>
                    <span>Addizionali (reg.+com.)</span>
                    <strong className="num">{euro(m770.totaleAddizionaleRegionale + m770.totaleAddizionaleComunale)} €</strong>
                  </div>
                  <div className="net-result">
                    <span>Percipienti</span>
                    <strong className="num">{m770.numeroPercipienti}</strong>
                  </div>
                </div>

                {m770.percipienti.length === 0 ? (
                  <div className="empty-state">
                    Nessun cedolino archiviato per il {m770.anno}. Archivia i cedolini dei dipendenti: il 770 si compone da solo.
                  </div>
                ) : (
                  <div className="table-wrap">
                    <table className="data-table spacious">
                      <thead>
                        <tr>
                          <th>Percipiente</th>
                          <th>Codice fiscale</th>
                          <th className="right">Ritenute IRPEF</th>
                          <th className="right">Add. regionale</th>
                          <th className="right">Add. comunale</th>
                        </tr>
                      </thead>
                      <tbody>
                        {m770.percipienti.map((p, i) => (
                          <tr key={i}>
                            <td><strong>{p.nominativo || "—"}</strong></td>
                            <td className="num">{p.codiceFiscale || "—"}</td>
                            <td className="right num">{euro(p.ritenuteIrpef)} €</td>
                            <td className="right num">{euro(p.addizionaleRegionale)} €</td>
                            <td className="right num">{euro(p.addizionaleComunale)} €</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Cos'è questa schermata</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          È la <b>bozza</b> del 770: riepiloga le ritenute operate dal datore (sostituto d'imposta) nell'anno.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
      </aside>
    </div>
  );
}
