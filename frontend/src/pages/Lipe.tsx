import { useEffect, useState } from "react";
import { api, type Azienda, type LipeBozza } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function Lipe() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [trimestre, setTrimestre] = useState(1);
  const [lipe, setLipe] = useState<LipeBozza | null>(null);
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
    if (!azienda) { setLipe(null); return; }
    api.lipeBozza(azienda.id, anno, trimestre)
      .then(setLipe)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno, trimestre]);

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>LIPE — Liquidazioni periodiche IVA</h1>
            <p>Bozza dalle liquidazioni IVA{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 3 dichiarazioni</p>
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
              <label>
                <span>Trimestre</span>
                <select value={trimestre} onChange={(e) => setTrimestre(Number(e.target.value))}>
                  <option value={1}>1° trimestre (gen–mar)</option>
                  <option value={2}>2° trimestre (apr–giu)</option>
                  <option value={3}>3° trimestre (lug–set)</option>
                  <option value={4}>4° trimestre (ott–dic)</option>
                </select>
              </label>
            </div>

            {lipe && (lipe.moduli.length === 0 ? (
              <div className="empty-state">
                Nessuna liquidazione IVA per questo trimestre. Registra spese/fatture e crea le liquidazioni: la LIPE si compone da sola.
              </div>
            ) : (
              <div className="payroll-result">
                <div className="result-summary">
                  <div>
                    <span>IVA a debito (trimestre)</span>
                    <strong className="num">{euro(lipe.totaleDovuta)} €</strong>
                  </div>
                  <div>
                    <span>IVA a credito (trimestre)</span>
                    <strong className="num">{euro(lipe.totaleCredito)} €</strong>
                  </div>
                  <div className="net-result">
                    <span>Periodi</span>
                    <strong className="num">{lipe.moduli.length}</strong>
                  </div>
                </div>

                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Periodo</th>
                        <th className="right">IVA esigibile</th>
                        <th className="right">IVA detratta</th>
                        <th className="right">IVA dovuta</th>
                        <th className="right">IVA a credito</th>
                      </tr>
                    </thead>
                    <tbody>
                      {lipe.moduli.map((m, i) => (
                        <tr key={i}>
                          <td><strong>{m.periodo}</strong></td>
                          <td className="right num">{euro(m.ivaEsigibile)} €</td>
                          <td className="right num">{euro(m.ivaDetratta)} €</td>
                          <td className="right num">{euro(m.ivaDovuta)} €</td>
                          <td className="right num">{euro(m.ivaCredito)} €</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Cos'è questa schermata</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          È la <b>bozza</b> della Comunicazione Liquidazioni Periodiche IVA del trimestre.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
      </aside>
    </div>
  );
}
