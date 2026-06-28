import { useEffect, useState, type FormEvent } from "react";
import { api, type Azienda, type CedolinoArchiviato, type DipendenteRapporto } from "../api";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

export function Cedolini() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [lista, setLista] = useState<CedolinoArchiviato[]>([]);
  const [anno, setAnno] = useState(2026);
  const [mese, setMese] = useState(1);
  const [straordinario, setStraordinario] = useState(0);
  const [caricamento, setCaricamento] = useState(true);
  const [salvataggio, setSalvataggio] = useState(false);
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
        const a = aziende[0] ?? null;
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

  async function genera(e: FormEvent) {
    e.preventDefault();
    if (!rapportoId) return;
    setSalvataggio(true);
    setErrore(null);
    try {
      await api.archiviaCedolino(rapportoId, { anno, mese, oreStraordinario: straordinario });
      setLista(await api.cedoliniArchiviati(rapportoId));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel calcolo");
    } finally {
      setSalvataggio(false);
    }
  }

  return (
    <div className="module-layout wide">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Cedolini</h1>
            <p>Calcola e archivia i cedolini reali{azienda ? ` · ${azienda.ragioneSociale}` : ""} — alimentano CU e 770</p>
          </div>
          <span className="status-pill blue">2026</span>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : dipendenti.length === 0 ? (
          <div className="empty-state">Nessun dipendente. Aggiungine uno in “Dipendenti”, poi torna qui a generare i cedolini.</div>
        ) : (
          <>
            <form className="form-grid" onSubmit={genera}>
              <label>
                <span>Dipendente</span>
                <select value={rapportoId} onChange={(e) => setRapportoId(e.target.value)}>
                  {dipendenti.map((d) => (
                    <option key={d.rapportoId} value={d.rapportoId}>{d.nominativo || "—"}</option>
                  ))}
                </select>
              </label>
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
              <label>
                <span>Ore straordinario</span>
                <input type="number" min={0} value={straordinario} onChange={(e) => setStraordinario(Number(e.target.value))} />
              </label>
              <button className="btn primary wide-field" type="submit" disabled={salvataggio}>
                {salvataggio ? "Calcolo…" : "Calcola e archivia cedolino"}
              </button>
            </form>

            {lista.length === 0 ? (
              <div className="empty-state">Nessun cedolino archiviato per questo dipendente. Generane uno qui sopra.</div>
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
                      </tr>
                    ))}
                  </tbody>
                </table>
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
