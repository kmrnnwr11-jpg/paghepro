import { useState } from "react";

type Tab = "anagrafica" | "rapporto" | "familiari" | "storico";

const tabs: { key: Tab; label: string }[] = [
  { key: "anagrafica", label: "Anagrafica" },
  { key: "rapporto", label: "Rapporto di lavoro" },
  { key: "familiari", label: "Familiari a carico" },
  { key: "storico", label: "Storico cedolini" },
];

const anagrafica: [string, string, boolean?, string?][] = [
  ["Codice fiscale", "BNCMRA88C15F205X"],
  ["Nome", "Mario"],
  ["Cognome", "Bianchi"],
  ["Data di nascita", "15/03/1988"],
  ["Comune di nascita", "Milano (MI)"],
  ["Cittadinanza", "Italiana"],
  ["Residenza", "Via Roma 10, 20121 Milano (MI) — Lombardia", true, "· rileva per addizionali"],
  ["Email", "m.bianchi@email.it"],
  ["IBAN", "IT•• •••• •••• •••• •••• •2 9 1 7", true, "🔒 sensibile"],
];

const rapporto: [string, string][] = [
  ["Azienda", "Rossi S.r.l."],
  ["Sede di lavoro", "Sede di Milano"],
  ["CCNL", "Commercio — Terziario"],
  ["Livello", "4"],
  ["Qualifica", "Impiegato"],
  ["Tipo contratto", "Tempo indeterminato"],
  ["Data assunzione", "01/01/2018"],
  ["Part-time", "No · 100%"],
  ["Retribuzione lorda", "2.749,24 € / mese"],
];

const familiari: [string, string, string, string, string][] = [
  ["Coniuge", "Sara Bianchi", "BNCSRA90M55F205X", "100%", "dal 06/2015"],
  ["Figlio", "Luca Bianchi", "BNCLCU16T10F205Y", "50%", "dal 10/2016"],
  ["Figlio", "Aurora Bianchi", "BNCRRA20P50F205Z", "50%", "dal 09/2020"],
];

type StoricoStato = "Bozza" | "Chiuso";
const storico: [string, StoricoStato, string, string][] = [
  ["Giu 2026", "Bozza", "2.749,24", "—"],
  ["Mag 2026", "Chiuso", "2.749,24", "1.736,50"],
  ["Apr 2026", "Chiuso", "2.812,60", "1.781,20"],
  ["Mar 2026", "Chiuso", "2.749,24", "1.736,50"],
  ["Feb 2026", "Chiuso", "2.749,24", "1.736,50"],
];

export function Dipendente({ onPayslip }: { onPayslip: () => void }) {
  const [tab, setTab] = useState<Tab>("rapporto");

  return (
    <div className="detail-layout">
      <header className="glass-card emp-header">
        <div className="emp-avatar">MB</div>
        <div className="emp-id">
          <div className="emp-id-top">
            <h1>Mario Bianchi</h1>
            <span className="status-pill green">● Attivo</span>
          </div>
          <div className="emp-id-meta">
            Impiegato · liv. 4 CCNL Commercio · Mat. 000123 · CF{" "}
            <span className="num">BNCMRA88C15F205X</span>
          </div>
        </div>
        <button className="btn secondary" type="button" onClick={onPayslip}>
          Ultimo cedolino
        </button>
        <button className="btn primary" type="button">
          Modifica
        </button>
      </header>

      <section className="glass-card emp-card">
        <div className="emp-tabs">
          {tabs.map((t) => (
            <button
              key={t.key}
              type="button"
              className={`emp-tab ${tab === t.key ? "active" : ""}`}
              onClick={() => setTab(t.key)}
            >
              {t.label}
            </button>
          ))}
        </div>

        <div className="emp-body">
          {tab === "anagrafica" && (
            <div className="emp-grid">
              {anagrafica.map(([label, value, wide, hint]) => (
                <div key={label} className={wide ? "emp-span2" : undefined}>
                  <div className="emp-field-label">
                    {label}
                    {hint && (
                      <span className={`hint ${hint.includes("🔒") ? "muted" : ""}`}> {hint}</span>
                    )}
                  </div>
                  <div className={`emp-field-value ${/\d/.test(value) ? "num" : ""}`}>{value}</div>
                </div>
              ))}
            </div>
          )}

          {tab === "rapporto" && (
            <div className="emp-grid">
              {rapporto.map(([label, value]) => (
                <div key={label}>
                  <div className="emp-field-label">{label}</div>
                  <div className={`emp-field-value ${/\d/.test(value) ? "num" : ""}`}>{value}</div>
                </div>
              ))}
            </div>
          )}

          {tab === "familiari" && (
            <div className="table-wrap">
              <table className="data-table spacious">
                <thead>
                  <tr>
                    <th>Rapporto</th>
                    <th>Nominativo</th>
                    <th>Codice fiscale</th>
                    <th>% carico</th>
                    <th>Periodo</th>
                  </tr>
                </thead>
                <tbody>
                  {familiari.map(([tipo, nome, cf, perc, per]) => (
                    <tr key={cf}>
                      <td><strong>{tipo}</strong></td>
                      <td>{nome}</td>
                      <td className="num">{cf}</td>
                      <td className="num">{perc}</td>
                      <td>{per}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {tab === "storico" && (
            <div className="table-wrap">
              <table className="data-table spacious">
                <thead>
                  <tr>
                    <th>Periodo</th>
                    <th>Stato</th>
                    <th className="right">Lordo</th>
                    <th className="right">Netto</th>
                    <th className="right">Azioni</th>
                  </tr>
                </thead>
                <tbody>
                  {storico.map(([per, st, lordo, netto]) => (
                    <tr key={per}>
                      <td><strong>{per}</strong></td>
                      <td>
                        <span className={`status-pill ${st === "Chiuso" ? "green" : "slate"}`}>{st}</span>
                      </td>
                      <td className="right num">{lordo} €</td>
                      <td className="right num">{netto === "—" ? "—" : `${netto} €`}</td>
                      <td className="right">
                        <button className="mini-action" type="button" onClick={onPayslip}>
                          Apri
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </section>
    </div>
  );
}
