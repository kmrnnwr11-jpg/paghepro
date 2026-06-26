import { useState } from "react";

type Flag = "" | "ord" | "mal" | "mat" | "malw";

// nome, matricola, iniziali, avBg, avFg, [9 valori], chiuso, flag
type Row = [string, string, string, string, string, string[], boolean, Flag];

const rows: Row[] = [
  ["Mario Bianchi", "000123", "MB", "#DBEAFE", "#1D4ED8", ["168,00", "12,00", "4,00", "0,00", "4,00", "8,00", "0,00", "0,00", "0,00"], true, ""],
  ["Sara Conti", "000124", "SC", "#FCE7F3", "#BE185D", ["160,00", "8,00", "0,00", "0,00", "2,00", "4,00", "8,00", "0,00", "0,00"], false, "mal"],
  ["Luca Ferri", "000125", "LF", "#DCFCE7", "#15803D", ["176,00", "16,00", "8,00", "0,00", "0,00", "0,00", "0,00", "1,00", "0,00"], false, "ord"],
  ["Giulia Neri", "000126", "GN", "#FEF3C7", "#B45309", ["168,00", "10,00", "0,00", "8,00", "5,00", "4,00", "0,00", "0,00", "0,00"], true, ""],
  ["Ahmed Khan", "000127", "AK", "#E0E7FF", "#4338CA", ["160,00", "6,00", "0,00", "0,00", "0,00", "2,00", "0,00", "0,00", "0,00"], true, ""],
  ["Elena Romano", "000128", "ER", "#FEE2E2", "#B91C1C", ["160,00", "0,00", "0,00", "0,00", "8,00", "4,00", "0,00", "0,00", "16,00"], false, "mat"],
  ["Paolo Costa", "000129", "PC", "#CFFAFE", "#0E7490", ["152,00", "4,00", "0,00", "0,00", "2,00", "2,00", "4,00", "0,00", "0,00"], false, "malw"],
  ["Martina Galli", "000130", "MG", "#F3E8FF", "#7E22CE", ["168,00", "0,00", "0,00", "0,00", "4,00", "0,00", "0,00", "0,00", "0,00"], true, ""],
];

// indice colonna -> chiave di anomalia che la evidenzia
const colFlagIndex: Record<number, Flag> = { 0: "ord", 6: "mal", 8: "mat" };

const kpis: [string, string, string, string, string, string][] = [
  ["🕐", "#EFF6FF", "#2563EB", "Ore ordinarie", "6.720", "Totale mese"],
  ["⏱", "#DCFCE7", "#16A34A", "Straordinario", "84", "Totale mese"],
  ["🏖", "#FEF3C7", "#B45309", "Ferie", "28 gg", "Totale mese"],
  ["👥", "#EDE9FE", "#6D28D9", "Cartellini chiusi", "31/42", "Chiusi / Totali"],
];

const anomalie: [string, string, string][] = [
  ["#DC2626", "2 cartellini incompleti", "Righe con dati mancanti"],
  ["#D97706", "1 superamento orario", "Oltre 48 ore settimanali"],
  ["#16A34A", "0 incoerenze", "Tutti i controlli ok"],
];

const riepilogo: [string, string][] = [
  ["Giorni lavorativi", "21"],
  ["Ore ordinarie", "6.720"],
  ["Straordinario", "84"],
  ["Giorni ferie", "28"],
  ["Permessi/ROL", "24"],
];

const headers = ["Ore ord.", "Straord.", "Nott.", "Fest.", "Ferie", "Perm.", "Malat.", "Inf.", "Matern."];

function cellClass(closed: boolean, colIdx: number, flag: Flag): string {
  if (closed) return "ts-input locked";
  const flagForCol = colFlagIndex[colIdx];
  if (flagForCol && flagForCol === flag) return "ts-input flag-red";
  // malattia "warning" (malw) evidenzia la colonna Malat. (indice 6) in ambra
  if (colIdx === 6 && flag === "malw") return "ts-input flag-amber";
  return "ts-input";
}

export function Presenze() {
  const [allClosed, setAllClosed] = useState(false);

  return (
    <div>
      <div className="page-heading">
        <div>
          <h1>Cartellino mensile</h1>
          <p>Rossi S.r.l. · Giugno 2026</p>
        </div>
        <div className="actions-row">
          <button className="btn secondary" type="button">⇪ Importa presenze</button>
          <button className="btn secondary" type="button">💾 Salva</button>
          <button className="btn success" type="button" onClick={() => setAllClosed(true)}>
            🔒 Chiudi cartellino
          </button>
        </div>
      </div>

      <div className="timesheet-layout">
        <div className="timesheet-main">
          <div className="ts-kpi-grid">
            {kpis.map(([icon, tint, fg, label, value, sub]) => (
              <article key={label} className="glass-card ts-kpi">
                <span className="ts-kpi-icon" style={{ background: tint, color: fg }}>{icon}</span>
                <div>
                  <span>{label}</span>
                  <b className="num">{value}</b>
                  <small>{sub}</small>
                </div>
              </article>
            ))}
          </div>

          <div className="glass-card ts-grid-card">
            <div className="ts-grid-scroll">
              <table className="ts-grid">
                <thead>
                  <tr>
                    <th>Dipendente</th>
                    {headers.map((h) => (
                      <th key={h}>{h}</th>
                    ))}
                    <th>Stato</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map(([nome, mat, ini, avBg, avFg, values, chiuso, flag]) => {
                    const closed = allClosed || chiuso;
                    return (
                      <tr key={mat}>
                        <td>
                          <div className="ts-person">
                            <span className="avatar" style={{ background: avBg, color: avFg }}>{ini}</span>
                            <div>
                              <b>{nome}</b>
                              <small className="num">Mat. {mat}</small>
                            </div>
                          </div>
                        </td>
                        {values.map((v, colIdx) => (
                          <td key={colIdx}>
                            <input
                              className={`num ${cellClass(closed, colIdx, flag)}`}
                              defaultValue={v}
                              readOnly={closed}
                            />
                          </td>
                        ))}
                        <td>
                          <span className={`status-pill ${closed ? "green" : "amber"}`}>
                            {closed ? "🔒 Chiuso" : "Aperto"}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <aside className="timesheet-rail">
          <section className="glass-card side-card">
            <div className="rail-head">
              <strong>Anomalie e validazioni</strong>
              <span className="count red" style={{ marginLeft: "auto" }}>3</span>
            </div>
            {anomalie.map(([color, t, m]) => (
              <div className="anomalia-row" key={t}>
                <span className="anomalia-dot" style={{ background: color }} />
                <div>
                  <strong>{t}</strong>
                  <small>{m}</small>
                </div>
              </div>
            ))}
            <button className="rail-cta" type="button">Apri elenco anomalie ›</button>
          </section>

          <section className="glass-card side-card">
            <div className="rail-head">
              <strong>Riepilogo presenze mese</strong>
            </div>
            {riepilogo.map(([l, v]) => (
              <div className="riepilogo-row" key={l}>
                <span>{l}</span>
                <strong className="num">{v}</strong>
              </div>
            ))}
          </section>

          <section className="glass-card side-card">
            <div className="rail-head">
              <strong>Azioni rapide</strong>
            </div>
            <button className="quick-action" type="button">📋 Copia da mese precedente</button>
            <button className="quick-action" type="button" style={{ marginBottom: 0 }}>↻ Azzera e reinserisci</button>
          </section>
        </aside>
      </div>
    </div>
  );
}
