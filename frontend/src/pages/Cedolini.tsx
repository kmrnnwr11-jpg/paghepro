import { useEffect, useState } from "react";
import { ArrowLeft, Calculator } from "lucide-react";
import { api, scaricaBlob, type Azienda, type CedolinoArchiviato, type DipendenteRapporto } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

export function Cedolini({ onTornaPresenze }: { onTornaPresenze: () => void }) {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [lista, setLista] = useState<CedolinoArchiviato[]>([]);
  const [caricamento, setCaricamento] = useState(true);
  const [scaricandoId, setScaricandoId] = useState<string | null>(null);
  const [errore, setErrore] = useState<string | null>(null);
  const [annoCu, setAnnoCu] = useState<number | null>(null);
  const [scaricandoCu, setScaricandoCu] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const aziende = await api.aziende();
        const a = scegliAziendaPreferita(aziende);
        setAzienda(a);
        if (a) {
          const d = await api.dipendenti(a.id);
          setDipendenti(d);
          if (d.length > 0) setRapportoId(d[0].rapportoId);
        }
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!rapportoId) { setLista([]); return; }
    api.cedoliniArchiviati(rapportoId)
      .then(setLista)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [rapportoId]);

  async function scaricaPdf(id: string) {
    setErrore(null);
    setScaricandoId(id);
    try {
      const { blob, filename } = await api.cedolinoPdf(id);
      await scaricaBlob(blob, filename);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Impossibile scaricare il PDF.");
    } finally {
      setScaricandoId(null);
    }
  }

  // Anni con almeno un cedolino archiviato: alimentano la Certificazione Unica.
  const anniCu = [...new Set(lista.map((c) => c.anno))].sort((a, b) => b - a);
  const annoCuScelto = annoCu && anniCu.includes(annoCu) ? annoCu : anniCu[0];

  async function scaricaCu() {
    if (!rapportoId || !annoCuScelto) return;
    setErrore(null);
    setScaricandoCu(true);
    try {
      const { blob, filename } = await api.certificazioneUnicaPdf(rapportoId, annoCuScelto);
      await scaricaBlob(blob, filename);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Impossibile scaricare la CU.");
    } finally {
      setScaricandoCu(false);
    }
  }

  return (
    <div className="module-layout wide">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div style={{ display: "flex", alignItems: "flex-start", gap: 12 }}>
            <button
              className="icon-button"
              type="button"
              aria-label="Torna a Presenze"
              title="Torna al passaggio precedente: Presenze"
              onClick={onTornaPresenze}
            >
              <ArrowLeft size={19} aria-hidden="true" />
            </button>
            <div>
              <h1>Cedolini</h1>
              <p>Archivio bozze cedolino{azienda ? ` · ${azienda.ragioneSociale}` : ""} - alimentano CU e 770</p>
            </div>
          </div>
          <div className="actions-row">
            <button className="btn primary" type="button" onClick={onTornaPresenze}>
              <Calculator size={16} aria-hidden="true" />
              Genera busta paga
            </button>
            <span className="status-pill amber">Bozza da validare</span>
          </div>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : dipendenti.length === 0 ? (
          <div className="empty-state">Nessun dipendente. Aggiungine uno in “Dipendenti”, poi torna qui a generare i cedolini.</div>
        ) : (
          <>
            <div className="form-grid">
              <label>
                <span>Dipendente</span>
                <select value={rapportoId} onChange={(e) => setRapportoId(e.target.value)}>
                  {dipendenti.map((d) => (
                    <option key={d.rapportoId} value={d.rapportoId}>{d.nominativo || "—"}</option>
                  ))}
                </select>
              </label>
              <div className="empty-state wide-field">
                Presenze e assenze si compilano solo nella pagina Presenze. Qui trovi lo storico
                approvato dal flusso mensile e puoi scaricare il PDF.
              </div>
            </div>

            {lista.length === 0 ? (
              <div className="empty-state">
                Nessun cedolino archiviato per questo dipendente. Premi “Genera busta paga”,
                controlla presenze e assenze del mese, quindi conferma il calcolo.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table spacious">
                  <thead>
                    <tr>
                      <th>Periodo</th>
                      <th className="right">Imponibile IRPEF</th>
                      <th className="right">Ritenute IRPEF</th>
                      <th className="right">Netto in busta</th>
                      <th className="right">Costo azienda</th>
                      <th className="right">PDF</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...lista].sort((a, b) => b.anno - a.anno || b.mese - a.mese).map((c) => (
                      <tr key={c.id}>
                        <td><strong>{mesi[c.mese]} {c.anno}</strong></td>
                        <td className="right num">{c.imponibileIrpef != null ? `${euro(c.imponibileIrpef)} €` : "—"}</td>
                        <td className="right num">{c.irpefNetta != null ? `${euro(c.irpefNetta)} €` : "—"}</td>
                        <td className="right num"><strong>{euro(c.nettoInBusta)} €</strong></td>
                        <td className="right num">{euro(c.costoAzienda)} €</td>
                        <td className="right">
                          <button
                            className="btn secondary"
                            type="button"
                            disabled={scaricandoId === c.id}
                            onClick={() => scaricaPdf(c.id)}
                          >
                            {scaricandoId === c.id ? "⏳ Download..." : "📄 PDF"}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {lista.length > 0 && (
              <div style={{ marginTop: 18, padding: 14, borderRadius: 10,
                background: "var(--panel-strong)", border: "1px solid var(--line)",
                display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
                <div style={{ flex: 1, minWidth: 220 }}>
                  <strong style={{ fontSize: 14 }}>Certificazione Unica (CU)</strong>
                  <p style={{ margin: "2px 0 0", fontSize: 12, opacity: 0.7 }}>
                    Riepilogo annuale del dipendente sul modello ufficiale dell'Agenzia delle Entrate,
                    compilato dai cedolini archiviati. Bozza da validare con il consulente.
                  </p>
                </div>
                <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
                  Anno
                  <select value={annoCuScelto} onChange={(e) => setAnnoCu(Number(e.target.value))}>
                    {anniCu.map((a) => <option key={a} value={a}>{a}</option>)}
                  </select>
                </label>
                <button className="btn secondary" type="button" disabled={scaricandoCu} onClick={scaricaCu}>
                  {scaricandoCu ? "⏳ Download..." : "📄 Scarica CU"}
                </button>
              </div>
            )}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>A cosa serve</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          Ogni cedolino archiviato confluisce nelle dichiarazioni: <b>CU</b>, <b>770</b> (e nei totali annui).
        </div>
        <div className="rail-metric"><span>Cedolini del dipendente</span><strong className="num">{lista.length}</strong></div>
        <div className="rail-metric"><span>CCNL</span><strong>Commercio</strong></div>
      </aside>
    </div>
  );
}
