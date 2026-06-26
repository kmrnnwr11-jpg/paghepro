import { useEffect, useRef, useState } from "react";

type CalcState = "idle" | "calculating" | "done" | "warn";

// id, nome, iniziali, avBg, avFg, cartellino, lordo, netto
const rows: [string, string, string, string, string, "Chiuso" | "Aperto", string, string][] = [
  ["000123", "Mario Bianchi", "MB", "#DBEAFE", "#1D4ED8", "Chiuso", "2.749,24", "1.736,50"],
  ["000124", "Sara Conti", "SC", "#FCE7F3", "#BE185D", "Aperto", "2.380,00", "1.654,80"],
  ["000125", "Luca Ferri", "LF", "#DCFCE7", "#15803D", "Aperto", "2.600,00", "1.802,30"],
  ["000126", "Giulia Neri", "GN", "#FEF3C7", "#B45309", "Chiuso", "2.300,00", "1.612,40"],
  ["000127", "Ahmed Khan", "AK", "#E0E7FF", "#4338CA", "Chiuso", "2.150,00", "1.521,90"],
  ["000128", "Elena Romano", "ER", "#FEE2E2", "#B91C1C", "Aperto", "2.200,00", "1.548,70"],
  ["000129", "Paolo Costa", "PC", "#CFFAFE", "#0E7490", "Chiuso", "2.100,00", "1.498,30"],
  ["000130", "Martina Galli", "MG", "#F3E8FF", "#7E22CE", "Chiuso", "2.420,00", "1.690,80"],
];

const cartChiusoIds = ["000123", "000126", "000127", "000129", "000130"];

const cedMap: Record<CalcState, { label: string; tone: string; esito: string }> = {
  idle: { label: "Bozza", tone: "slate", esito: "" },
  calculating: { label: "Calcolo…", tone: "blue", esito: "↻" },
  done: { label: "Calcolato", tone: "green", esito: "✓" },
  warn: { label: "Avviso", tone: "amber", esito: "⚠" },
};

export function Elaborazione() {
  const [sel, setSel] = useState<Record<string, boolean>>({
    "000124": true,
    "000125": true,
    "000128": true,
  });
  const [calc, setCalc] = useState<Record<string, CalcState>>({
    "000123": "done",
    "000126": "done",
    "000129": "done",
    "000130": "done",
  });
  const [calculating, setCalculating] = useState(false);
  const [progress, setProgress] = useState(0);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => () => { if (timer.current) clearInterval(timer.current); }, []);

  const selCount = rows.filter(([id]) => sel[id]).length;
  const allChecked = rows.every(([id]) => sel[id]);
  const disabled = calculating || selCount === 0;

  function toggleAll() {
    const all = rows.every(([id]) => sel[id]);
    if (all) return setSel({});
    const next: Record<string, boolean> = {};
    rows.forEach(([id]) => { next[id] = true; });
    setSel(next);
  }

  function calculate() {
    if (calculating) return;
    const ids = rows.filter(([id]) => sel[id]).map(([id]) => id);
    if (!ids.length) return;
    setCalc((c) => { const n = { ...c }; ids.forEach((id) => { n[id] = "calculating"; }); return n; });
    setCalculating(true);
    setProgress(0);
    let p = 0;
    timer.current = setInterval(() => {
      p += 6 + Math.random() * 10;
      if (p >= 100) {
        if (timer.current) clearInterval(timer.current);
        setProgress(100);
        setCalc((c) => {
          const n = { ...c };
          ids.forEach((id) => { n[id] = cartChiusoIds.includes(id) ? "done" : "warn"; });
          return n;
        });
        setTimeout(() => { setCalculating(false); setSel({}); setProgress(0); }, 600);
      } else {
        setProgress(Math.round(p));
      }
    }, 170);
  }

  return (
    <div className="detail-layout">
      <div className="page-heading" style={{ alignItems: "center" }}>
        <div>
          <h1>Elaborazione cedolini</h1>
          <p>Rossi S.r.l. · Giugno 2026</p>
        </div>
        <div className="actions-row" style={{ alignItems: "center" }}>
          <span className="sel-count"><strong>{selCount}</strong> selezionati</span>
          <button className="btn primary" type="button" disabled={disabled} onClick={calculate}>
            ▶ Calcola selezionati
          </button>
        </div>
      </div>

      {calculating && (
        <div className="glass-card batch-progress">
          <div className="batch-progress-head">
            <span>↻ Elaborazione in corso… (coda asincrona)</span>
            <span className="num">{progress}%</span>
          </div>
          <div className="progress-track">
            <div className="progress-fill" style={{ width: `${progress}%` }} />
          </div>
        </div>
      )}

      <div className="glass-card batch-card">
        <table className="batch-table">
          <thead>
            <tr>
              <th style={{ width: 40 }}>
                <input type="checkbox" checked={allChecked} onChange={toggleAll} />
              </th>
              <th>Dipendente</th>
              <th>Cartellino</th>
              <th>Cedolino</th>
              <th className="right">Lordo</th>
              <th className="right">Netto</th>
              <th className="center">Esito</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(([id, nome, ini, avBg, avFg, cart, lordo, netto]) => {
              const state = calc[id] ?? "idle";
              const cm = cedMap[state];
              const showNet = state === "done" || state === "warn" ? netto : "—";
              return (
                <tr key={id} className={sel[id] ? "selected" : undefined}>
                  <td>
                    <input
                      type="checkbox"
                      checked={!!sel[id]}
                      onChange={() => setSel((p) => ({ ...p, [id]: !p[id] }))}
                    />
                  </td>
                  <td>
                    <div className="ts-person">
                      <span className="avatar" style={{ background: avBg, color: avFg }}>{ini}</span>
                      <div>
                        <b>{nome}</b>
                        <small className="num">Mat. {id}</small>
                      </div>
                    </div>
                  </td>
                  <td>
                    <span className={`status-pill ${cart === "Chiuso" ? "green" : "amber"}`}>{cart}</span>
                  </td>
                  <td>
                    <span className={`status-pill ${cm.tone}`}>{cm.label}</span>
                  </td>
                  <td className="right num">{lordo}</td>
                  <td className="right num" style={{ fontWeight: 700 }}>{showNet}</td>
                  <td className="center">
                    <span className="batch-esito">{cm.esito}</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <p className="batch-warn">
        ⚠ I cartellini ancora <strong>Aperti</strong> generano un avviso: il cedolino viene calcolato su dati non bloccati.
      </p>
    </div>
  );
}
