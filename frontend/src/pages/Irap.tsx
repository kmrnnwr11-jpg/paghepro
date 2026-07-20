import { useEffect, useState } from "react";
import { api, type Azienda, type IrapBozza } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function Irap() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [b, setB] = useState<IrapBozza | null>(null);
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
    if (!azienda) { setB(null); return; }
    api.irapBozza(azienda.id, anno)
      .then(setB)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno]);

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>IRAP</h1>
            <p>Bozza dal valore della produzione{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 5 dichiarazioni</p>
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

            {b && (
              <>
                <div className="table-wrap">
                  <table className="data-table compact">
                    <tbody>
                      <tr><td>Ricavi (valore della produzione)</td><td className="right num">{euro(b.ricavi)} €</td></tr>
                      <tr><td>− Costi esterni (materie e servizi)</td><td className="right num">{euro(b.costiEsterni)} €</td></tr>
                      <tr><td>− Costo del lavoro deducibile (tempo indet.)</td><td className="right num">{euro(b.costoLavoroDeducibile)} €</td></tr>
                      <tr><td><strong>Base imponibile IRAP</strong></td><td className="right num"><strong>{euro(b.baseImponibile)} €</strong></td></tr>
                      <tr><td>Aliquota</td><td className="right num">{(b.aliquota * 100).toLocaleString("it-IT", { minimumFractionDigits: 1, maximumFractionDigits: 2 })}%</td></tr>
                    </tbody>
                  </table>
                </div>

                <div className="accounting-total">
                  <span>IRAP stimata {b.anno}</span>
                  <strong>{euro(b.imposta)} €</strong>
                  <em>Base {euro(b.baseImponibile)} € × {(b.aliquota * 100).toLocaleString("it-IT", { minimumFractionDigits: 1 })}%</em>
                </div>
              </>
            )}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Cos'è questa schermata</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          Bozza <b>IRAP</b>: valore della produzione (ricavi) meno costi esterni e costo del lavoro deducibile, per aliquota standard 3,9%.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
        <p className="muto" style={{ fontSize: 12, marginTop: 10, fontWeight: 600 }}>
          Calcolo semplificato: l'IRAP varia per tipo di contribuente e per maggiorazioni regionali. Da validare con un Commercialista.
        </p>
      </aside>
    </div>
  );
}
