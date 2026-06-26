import { useState } from "react";

// cod, descrizione, quantità, valore unitario, importo
const competenze: [string, string, string, string, string][] = [
  ["1001", "Paga base", "26,00", "82,61538", "2.148,00"],
  ["1002", "Contingenza", "26,00", "10,32692", "268,50"],
  ["1003", "Scatti di anzianità", "2", "8,26000", "16,52"],
  ["1101", "Straordinario 15%", "8,00", "12,67692", "101,42"],
  ["1201", "Ratei 13ª mensilità", "/", "107,40", "107,40"],
  ["1202", "Ratei 14ª mensilità", "/", "107,40", "107,40"],
];

// cod, descrizione, importo
const trattenute: [string, string, string][] = [
  ["2001", "Contributi INPS c/dipendente", "228,09"],
  ["3001", "IRPEF netta", "565,11"],
  ["3002", "Addizionale regionale (Lombardia)", "20,20"],
  ["3003", "Addizionale comunale (Milano)", "13,34"],
  ["4001", "Altre trattenute", "186,00"],
];

const boxes: [string, string, string, boolean][] = [
  ["Imponibile INPS", "2.584,00 €", "Soggetto a contributi", false],
  ["Imponibile IRPEF", "2.348,28 €", "Reddito fiscale", false],
  ["Contributi azienda", "781,64 €", "Oneri a carico azienda", false],
  ["NETTO IN BUSTA", "1.736,50 €", "", true],
];

const inps: [string, string][] = [
  ["Fino a 2.692,00 €", "9,190%"],
  ["Da 2.692,01 a 25.000 €", "9,190%"],
];
const irpef: [string, string][] = [
  ["Fino a 28.000,00 €", "23%"],
  ["28.000,01 — 50.000 €", "35%"],
  ["Oltre 50.000,00 €", "43%"],
];
const addizionali: [string, string][] = [
  ["Regionale (Lombardia)", "1,23%"],
  ["Comunale (Milano)", "0,80%"],
];

export function BustaPaga({ onRicalcola }: { onRicalcola: () => void }) {
  const [detail, setDetail] = useState(true);

  return (
    <div>
      <div className="page-heading" style={{ alignItems: "center" }}>
        <div className="emp-id-top">
          <h1>Anteprima busta paga</h1>
          <span className="status-pill green">✓ Calcolato</span>
        </div>
      </div>

      <div className="payslip-layout">
        <section className="glass-card payslip-sheet">
          <div className="payslip-head">
            <div className="payslip-azienda">
              <div className="payslip-logo">R</div>
              <div>
                <strong>ROSSI S.R.L.</strong>
                <small className="num">
                  P.IVA 01567890123
                  <br />
                  Via Roma 10 — 20121 Milano (MI)
                </small>
              </div>
            </div>
            <div className="payslip-head-grid">
              <div>
                <div className="lbl">Dipendente</div>
                <div className="val strong">MARIO BIANCHI</div>
                <div className="sub num">BNCMRA88C15F205X</div>
              </div>
              <div>
                <div className="lbl">Qualifica</div>
                <div className="val">Impiegato</div>
              </div>
              <div>
                <div className="lbl">Livello</div>
                <div className="val">4 CCNL Commercio</div>
              </div>
              <div>
                <div className="lbl">Periodo</div>
                <div className="val">Giugno 2026</div>
                <div className="sub num">Mat. 000123</div>
              </div>
            </div>
          </div>

          <div className="payslip-cols">
            <div>
              <div className="payslip-section-title">COMPETENZE</div>
              <table className="payslip-table">
                <thead>
                  <tr>
                    <th>Cod.</th>
                    <th>Descrizione</th>
                    <th className="right">Quantità</th>
                    <th className="right">Valore unit.</th>
                    <th className="right">Importo</th>
                  </tr>
                </thead>
                <tbody>
                  {competenze.map(([cod, desc, qta, vu, imp]) => (
                    <tr key={cod}>
                      <td className="num muted">{cod}</td>
                      <td>{desc}</td>
                      <td className="right num">{qta}</td>
                      <td className="right num">{vu}</td>
                      <td className="right num" style={{ fontWeight: 600 }}>{imp}</td>
                    </tr>
                  ))}
                  <tr className="total">
                    <td colSpan={4}>TOTALE COMPETENZE</td>
                    <td className="right num">2.749,24 €</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div>
              <div className="payslip-section-title">TRATTENUTE</div>
              <table className="payslip-table">
                <thead>
                  <tr>
                    <th>Cod.</th>
                    <th>Descrizione</th>
                    <th className="right">Importo</th>
                  </tr>
                </thead>
                <tbody>
                  {trattenute.map(([cod, desc, imp]) => (
                    <tr key={cod}>
                      <td className="num muted">{cod}</td>
                      <td>{desc}</td>
                      <td className="right num" style={{ fontWeight: 600 }}>{imp}</td>
                    </tr>
                  ))}
                  <tr className="total">
                    <td colSpan={2}>TOTALE TRATTENUTE</td>
                    <td className="right num">1.012,74 €</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div className="payslip-boxes">
            {boxes.map(([label, value, sub, net]) => (
              <div key={label} className={`payslip-box ${net ? "net" : ""}`}>
                <span>{label}</span>
                <b className="num">{value}</b>
                {sub && <small>{sub}</small>}
              </div>
            ))}
          </div>

          <div className="tfr-strip">
            <div className="tfr-title">TFR</div>
            <div className="tfr-cell">
              <span>Quota mese</span>
              <b className="num">107,40 €</b>
            </div>
            <div className="tfr-op">+</div>
            <div className="tfr-cell">
              <span>Rivalutazione</span>
              <b className="num">8,47 €</b>
            </div>
            <div className="tfr-op">=</div>
            <div className="tfr-cell">
              <span>Accantonamento mese</span>
              <b className="num">115,87 €</b>
            </div>
            <div className="tfr-cell right">
              <span>Saldo progressivo TFR</span>
              <b className="num">9.842,15 €</b>
              <small className="num">al 30/06/2026</small>
            </div>
          </div>

          <div className="payslip-note">
            🛡 Il presente prospetto è puramente indicativo. Fa fede il cedolino ufficiale.
          </div>
        </section>

        <aside className="payslip-rail">
          <section className="glass-card side-card">
            <div className="rail-head">
              <strong>Azioni</strong>
            </div>
            <button className="rail-action" type="button">⤓ Scarica PDF</button>
            <button className="rail-action" type="button">➤ Invia al dipendente</button>
            <button className="rail-action" type="button" onClick={onRicalcola}>↻ Ricalcola</button>
            <button className="rail-action success" type="button" style={{ marginBottom: 0 }}>🔒 Chiudi cedolino</button>
          </section>

          <section className="glass-card calc-detail">
            <button className="calc-detail-head" type="button" onClick={() => setDetail((d) => !d)}>
              <span>Dettaglio calcolo</span>
              <span className="muto">{detail ? "▾" : "▸"}</span>
            </button>
            {detail && (
              <div className="calc-detail-body">
                <div className="group">Aliquote INPS 2026</div>
                {inps.map(([l, v]) => (
                  <div className="calc-detail-row" key={l}>
                    <span>{l}</span>
                    <strong className="num">{v}</strong>
                  </div>
                ))}
                <div className="group spaced">Scaglioni IRPEF 2026</div>
                {irpef.map(([l, v]) => (
                  <div className="calc-detail-row" key={l}>
                    <span>{l}</span>
                    <strong className="num">{v}</strong>
                  </div>
                ))}
                <div className="group spaced">Addizionali 2026</div>
                {addizionali.map(([l, v]) => (
                  <div className="calc-detail-row" key={l}>
                    <span>{l}</span>
                    <strong className="num">{v}</strong>
                  </div>
                ))}
              </div>
            )}
          </section>
        </aside>
      </div>
    </div>
  );
}
