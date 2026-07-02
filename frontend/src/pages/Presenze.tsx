import { useEffect, useState } from "react";
import { api, type Azienda, type PresenzeMese } from "../api";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

export function Presenze() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [mese, setMese] = useState(1);
  const [board, setBoard] = useState<PresenzeMese | null>(null);
  const [ore, setOre] = useState<Record<string, number>>({});
  const [caricamento, setCaricamento] = useState(true);
  const [elaborando, setElaborando] = useState<string | null>(null);
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

  async function carica() {
    if (!azienda) return;
    const b = await api.presenzeMese(azienda.id, anno, mese);
    setBoard(b);
    setOre(Object.fromEntries(b.righe.map((r) => [r.rapportoId, r.oreStraordinario])));
  }

  useEffect(() => {
    if (!azienda) { setBoard(null); return; }
    carica().catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno, mese]);

  async function elabora(rapportoId: string) {
    setElaborando(rapportoId);
    setErrore(null);
    try {
      await api.archiviaCedolino(rapportoId, { anno, mese, oreStraordinario: ore[rapportoId] ?? 0 });
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel calcolo");
    } finally {
      setElaborando(null);
    }
  }

  return (
    <div>
      <div className="page-heading">
        <div>
          <h1>Cartellino mensile</h1>
          <p>Presenze reali{azienda ? ` · ${azienda.ragioneSociale}` : ""} · {mesi[mese]} {anno}</p>
        </div>
      </div>

      {caricamento ? (
        <section className="glass-card page-card"><div className="empty-state">Caricamento…</div></section>
      ) : (
        <div className="timesheet-layout">
          <div className="timesheet-main">
            <div className="ts-kpi-grid">
              <article className="glass-card ts-kpi">
                <span className="ts-kpi-icon tone-icon blue">👥</span>
                <div><span>Dipendenti</span><b className="num">{board?.totale ?? 0}</b><small>Totale azienda</small></div>
              </article>
              <article className="glass-card ts-kpi">
                <span className="ts-kpi-icon tone-icon green">✓</span>
                <div><span>Cedolini elaborati</span><b className="num">{board?.elaborati ?? 0}/{board?.totale ?? 0}</b><small>Questo mese</small></div>
              </article>
            </div>

            <div className="glass-card page-card">
              <div className="form-grid" style={{ marginBottom: 8 }}>
                <label>
                  <span>Anno</span>
                  <input type="number" min={2000} max={2100} value={anno} onChange={(e) => setAnno(Number(e.target.value))} />
                </label>
                <label>
                  <span>Mese</span>
                  <select value={mese} onChange={(e) => setMese(Number(e.target.value))}>
                    {mesi.slice(1).map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                  </select>
                </label>
              </div>

              {!board || board.righe.length === 0 ? (
                <div className="empty-state">Nessun dipendente. Aggiungine in “Dipendenti”.</div>
              ) : (
                <div className="table-wrap">
                  <table className="data-table spacious">
                    <thead>
                      <tr>
                        <th>Dipendente</th>
                        <th className="right">Ore straordinario</th>
                        <th>Stato</th>
                        <th className="right">Netto</th>
                        <th className="right">Azioni</th>
                      </tr>
                    </thead>
                    <tbody>
                      {board.righe.map((r) => (
                        <tr key={r.rapportoId}>
                          <td><strong>{r.nominativo || "—"}</strong></td>
                          <td className="right">
                            <input
                              className="num ts-input"
                              type="number"
                              min={0}
                              value={ore[r.rapportoId] ?? 0}
                              onChange={(e) => setOre((o) => ({ ...o, [r.rapportoId]: Number(e.target.value) }))}
                            />
                          </td>
                          <td>
                            <span className={`status-pill ${r.elaborato ? "green" : "amber"}`}>
                              {r.elaborato ? "✓ Elaborato" : "Da elaborare"}
                            </span>
                          </td>
                          <td className="right num">{r.elaborato ? `${euro(r.netto)} €` : "—"}</td>
                          <td className="right">
                            <button className="mini-action" type="button" disabled={elaborando === r.rapportoId} onClick={() => elabora(r.rapportoId)}>
                              {elaborando === r.rapportoId ? "…" : r.elaborato ? "Ricalcola" : "Elabora"}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {errore && <p className="errore">{errore}</p>}
            </div>
          </div>

          <aside className="timesheet-rail">
            <section className="glass-card side-card">
              <div className="rail-head"><strong>Come funziona</strong></div>
              <div className="empty-state" style={{ marginBottom: 10 }}>
                Inserisci le ore di <b>straordinario</b> e premi <b>Elabora</b>: il cedolino del mese viene calcolato e archiviato (e alimenta CU/770).
              </div>
              <p className="muto" style={{ fontSize: 12, fontWeight: 600 }}>
                Nota: ferie, permessi e malattia richiederanno un'estensione del motore di calcolo (oggi incide lo straordinario).
              </p>
            </section>
          </aside>
        </div>
      )}
    </div>
  );
}
