import { useEffect, useState } from "react";
import { api, type CedolinoArchiviato, type RapportoDettaglio } from "../api";

type Tab = "anagrafica" | "rapporto" | "storico";

const tabs: { key: Tab; label: string }[] = [
  { key: "anagrafica", label: "Anagrafica" },
  { key: "rapporto", label: "Rapporto di lavoro" },
  { key: "storico", label: "Storico cedolini" },
];

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic"];

function iniziali(nome: string, cognome: string): string {
  return `${(cognome[0] ?? "").toUpperCase()}${(nome[0] ?? "").toUpperCase()}` || "—";
}

export function Dipendente({ rapportoId, onPayslip }: { rapportoId: string | null; onPayslip: () => void }) {
  const [tab, setTab] = useState<Tab>("anagrafica");
  const [d, setD] = useState<RapportoDettaglio | null>(null);
  const [cedolini, setCedolini] = useState<CedolinoArchiviato[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [caricamento, setCaricamento] = useState(true);

  useEffect(() => {
    if (!rapportoId) { setCaricamento(false); return; }
    setCaricamento(true);
    Promise.all([api.rapportoDettaglio(rapportoId), api.cedoliniArchiviati(rapportoId)])
      .then(([det, ced]) => { setD(det); setCedolini(ced); })
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"))
      .finally(() => setCaricamento(false));
  }, [rapportoId]);

  if (!rapportoId) {
    return (
      <section className="glass-card page-card">
        <div className="empty-state">Seleziona un dipendente dall'elenco “Dipendenti” per aprirne la scheda.</div>
      </section>
    );
  }
  if (caricamento) {
    return <section className="glass-card page-card"><div className="empty-state">Caricamento…</div></section>;
  }
  if (!d) {
    return <section className="glass-card page-card"><p className="errore">{errore ?? "Dipendente non trovato."}</p></section>;
  }

  const anagrafica: [string, string][] = [
    ["Codice fiscale", d.codiceFiscale || "—"],
    ["Nome", d.nome || "—"],
    ["Cognome", d.cognome || "—"],
    ["IBAN", d.iban || "— (non inserito)"],
  ];
  const rapporto: [string, string][] = [
    ["CCNL", d.codiceCcnl === "COMMERCIO_TERZIARIO" ? "Commercio — Terziario" : d.codiceCcnl],
    ["Livello", d.livello],
    ["Qualifica", d.qualifica],
    ["Mensilità", String(d.numeroMensilita)],
    ["Part-time", d.partTime ? `Sì · ${d.percentualePartTime}%` : "No · 100%"],
    ["Scatti anzianità", String(d.scattiAnzianita)],
    ["Superminimo", `${euro(d.superminimo)} €`],
    ["Regione", d.regione || "—"],
    ["Data assunzione", d.dataAssunzione],
  ];

  return (
    <div className="detail-layout">
      <header className="glass-card emp-header">
        <div className="emp-avatar">{iniziali(d.nome, d.cognome)}</div>
        <div className="emp-id">
          <div className="emp-id-top">
            <h1>{d.cognome} {d.nome}</h1>
            <span className={`status-pill ${d.anonimizzato ? "slate" : "green"}`}>
              {d.anonimizzato ? "Anonimizzato" : "● Attivo"}
            </span>
          </div>
          <div className="emp-id-meta">
            {d.qualifica} · liv. {d.livello} {d.codiceCcnl === "COMMERCIO_TERZIARIO" ? "CCNL Commercio" : d.codiceCcnl}
            {" · CF "}<span className="num">{d.codiceFiscale || "—"}</span>
          </div>
        </div>
        <button className="btn secondary" type="button" onClick={onPayslip}>Cedolini</button>
      </header>

      <section className="glass-card emp-card">
        <div className="emp-tabs">
          {tabs.map((t) => (
            <button key={t.key} type="button" className={`emp-tab ${tab === t.key ? "active" : ""}`} onClick={() => setTab(t.key)}>
              {t.label}
            </button>
          ))}
        </div>

        <div className="emp-body">
          {tab === "anagrafica" && (
            <div className="emp-grid">
              {anagrafica.map(([label, value]) => (
                <div key={label}>
                  <div className="emp-field-label">{label}</div>
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

          {tab === "storico" && (
            cedolini.length === 0 ? (
              <div className="empty-state">Nessun cedolino archiviato. Generane in “Cedolini”.</div>
            ) : (
              <div className="table-wrap">
                <table className="data-table spacious">
                  <thead>
                    <tr>
                      <th>Periodo</th>
                      <th className="right">Imponibile IRPEF</th>
                      <th className="right">Ritenute IRPEF</th>
                      <th className="right">Netto</th>
                      <th className="right">Azioni</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...cedolini].sort((a, b) => b.anno - a.anno || b.mese - a.mese).map((c) => (
                      <tr key={c.id}>
                        <td><strong>{mesi[c.mese]} {c.anno}</strong></td>
                        <td className="right num">{c.imponibileIrpef != null ? `${euro(c.imponibileIrpef)} €` : "—"}</td>
                        <td className="right num">{c.irpefNetta != null ? `${euro(c.irpefNetta)} €` : "—"}</td>
                        <td className="right num"><strong>{euro(c.nettoInBusta)} €</strong></td>
                        <td className="right">
                          <button className="mini-action" type="button" onClick={onPayslip}>Apri</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          )}
        </div>
      </section>
    </div>
  );
}
