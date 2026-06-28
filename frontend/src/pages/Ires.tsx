import { useEffect, useState } from "react";
import { api, type Azienda, type IresBozza } from "../api";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function Ires() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [variazioni, setVariazioni] = useState(0);
  const [b, setB] = useState<IresBozza | null>(null);
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
    api.iresBozza(azienda.id, anno, variazioni)
      .then(setB)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno, variazioni]);

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>IRES — Redditi società</h1>
            <p>Bozza dall'utile di bilancio{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 5 dichiarazioni</p>
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
              <label>
                <span>Variazioni fiscali nette (+/−)</span>
                <input type="number" step="0.01" value={variazioni} onChange={(e) => setVariazioni(Number(e.target.value))} />
              </label>
            </div>

            {b && (!b.haBilancio ? (
              <div className="empty-state">
                Nessun bilancio per il {b.anno}. Crea prima il bilancio d'esercizio in “Bilancio”: l'IRES si calcola sull'utile.
              </div>
            ) : (
              <>
                <div className="table-wrap">
                  <table className="data-table compact">
                    <tbody>
                      <tr><td>Utile (perdita) d'esercizio</td><td className="right num">{euro(b.utileEsercizio)} €</td></tr>
                      <tr><td>+ Imposte già in bilancio</td><td className="right num">{euro(b.imposteBilancio)} €</td></tr>
                      <tr><td><strong>Reddito ante imposte</strong></td><td className="right num"><strong>{euro(b.redditoAnteImposte)} €</strong></td></tr>
                      <tr><td>± Variazioni fiscali nette</td><td className="right num">{euro(b.variazioniNette)} €</td></tr>
                      <tr><td><strong>Base imponibile IRES</strong></td><td className="right num"><strong>{euro(b.baseImponibile)} €</strong></td></tr>
                      <tr><td>Aliquota</td><td className="right num">{(b.aliquota * 100).toLocaleString("it-IT")}%</td></tr>
                    </tbody>
                  </table>
                </div>

                <div className="accounting-total">
                  <span>IRES stimata {b.anno}</span>
                  <strong>{euro(b.imposta)} €</strong>
                  <em>Base {euro(b.baseImponibile)} € × {(b.aliquota * 100).toLocaleString("it-IT")}%</em>
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
          Bozza <b>IRES</b> (società di capitali): parte dall'utile ante imposte del bilancio, applica le variazioni fiscali e l'aliquota 24%.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
        <p className="muto" style={{ fontSize: 12, marginTop: 10, fontWeight: 600 }}>
          Semplificato: le variazioni in aumento/diminuzione reali sono molte. Da validare con un Commercialista.
        </p>
      </aside>
    </div>
  );
}
