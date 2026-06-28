import { useEffect, useState } from "react";
import { api, type Azienda, type IvaAnnualeBozza } from "../api";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function IvaAnnuale() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [iva, setIva] = useState<IvaAnnualeBozza | null>(null);
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
    if (!azienda) { setIva(null); return; }
    api.ivaAnnualeBozza(azienda.id, anno)
      .then(setIva)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno]);

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Dichiarazione IVA annuale</h1>
            <p>Bozza dalle liquidazioni dell'anno{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 3 dichiarazioni</p>
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

            {iva && (
              <div className="payroll-result">
                <div className="result-summary">
                  <div>
                    <span>IVA vendite (anno)</span>
                    <strong className="num">{euro(iva.totaleIvaVendite)} €</strong>
                  </div>
                  <div>
                    <span>IVA acquisti detraibile</span>
                    <strong className="num">{euro(iva.totaleIvaAcquistiDetraibile)} €</strong>
                  </div>
                  <div className="net-result">
                    <span>{iva.saldoAnnuale >= 0 ? "Saldo IVA a debito" : "Saldo IVA a credito"}</span>
                    <strong className="num">{euro(Math.abs(iva.saldoAnnuale))} €</strong>
                  </div>
                </div>

                {iva.periodi.length === 0 ? (
                  <div className="empty-state">
                    Nessuna liquidazione IVA per il {iva.anno}. Crea le liquidazioni periodiche: la dichiarazione annuale si compone da sola.
                  </div>
                ) : (
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
                        {iva.periodi.map((p, i) => (
                          <tr key={i}>
                            <td><strong>{p.periodo}</strong></td>
                            <td className="right num">{euro(p.ivaEsigibile)} €</td>
                            <td className="right num">{euro(p.ivaDetratta)} €</td>
                            <td className="right num">{euro(p.ivaDovuta)} €</td>
                            <td className="right num">{euro(p.ivaCredito)} €</td>
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
          È la <b>bozza</b> della Dichiarazione IVA annuale: riassume tutte le liquidazioni dell'anno.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
      </aside>
    </div>
  );
}
