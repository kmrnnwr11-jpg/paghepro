import { useEffect, useState, type FormEvent } from "react";
import { api, type Cedolino as CedolinoT, type Livello } from "../api";
import { ComuneAutocomplete } from "../ComuneAutocomplete";

const CCNL = "COMMERCIO_TERZIARIO";
const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function Cedolino() {
  const [livelli, setLivelli] = useState<Livello[]>([]);
  const [livello, setLivello] = useState("3");
  const [scatti, setScatti] = useState(0);
  const [comune, setComune] = useState("Milano");
  const [regione, setRegione] = useState("Lombardia");
  const [cat, setCat] = useState("F205");
  const [mese, setMese] = useState(6);
  const [straordinario, setStraordinario] = useState(0);
  const [cedolino, setCedolino] = useState<CedolinoT | null>(null);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    api.livelli(CCNL).then((r) => setLivelli(r.livelli)).catch(() => { /* ignora */ });
  }, []);

  async function calcola(e: FormEvent) {
    e.preventDefault();
    setErrore(null);
    setCedolino(null);
    try {
      const c = await api.calcolaCedolino(
        {
          codiceCcnl: CCNL, livello, qualifica: "Impiegato", numeroMensilita: 14,
          scattiAnzianita: scatti, regione, codiceCatastaleComune: cat,
        },
        { anno: 2026, mese, oreStraordinario: straordinario },
      );
      setCedolino(c);
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Errore");
    }
  }

  return (
    <section className="glass-card form-card">
      <div className="card-title-row">
        <div>
          <h2>Simulazione cedolino</h2>
          <p>CCNL Commercio · calcolo contributi, IRPEF, addizionali e netto in busta</p>
        </div>
        <span className="status-pill blue">2026</span>
      </div>

      <form className="form-grid" onSubmit={calcola}>
        <label>
          <span>Livello lavoratore</span>
          <select value={livello} onChange={(e) => setLivello(e.target.value)}>
            {livelli.map((l) => <option key={l.codice} value={l.codice}>{l.codice} - {l.descrizione}</option>)}
          </select>
        </label>
        <label>
          <span>Scatti anzianità</span>
          <input type="number" min={0} value={scatti} onChange={(e) => setScatti(Number(e.target.value))} />
        </label>
        <label>
          <span>Comune lavoratore</span>
          <ComuneAutocomplete
            valore={comune}
            onSelect={(c) => { setComune(c.nome); setRegione(c.reg); setCat(c.cat); }}
          />
          <small>{comune ? `${comune} · ${regione}` : "Serve per addizionali regionali e comunali."}</small>
        </label>
        <label>
          <span>Mese</span>
          <input type="number" min={1} max={12} value={mese} onChange={(e) => setMese(Number(e.target.value))} />
        </label>
        <label>
          <span>Ore straordinario</span>
          <input type="number" min={0} value={straordinario} onChange={(e) => setStraordinario(Number(e.target.value))} />
        </label>
        <button className="btn primary" type="submit">
          Calcola cedolino
        </button>
      </form>

      {errore && <p className="errore">{errore}</p>}

      {cedolino && (
        <div className="payroll-result">
          <div className="result-summary">
            <div>
              <span>Competenze</span>
              <strong>{euro(cedolino.totaleCompetenze)} €</strong>
            </div>
            <div>
              <span>Trattenute</span>
              <strong>{euro(cedolino.totaleTrattenute)} €</strong>
            </div>
            <div className="net-result">
              <span>Netto in busta</span>
              <strong>{euro(cedolino.nettoInBusta)} €</strong>
            </div>
          </div>

          <div className="table-wrap">
            <table className="data-table compact">
              <thead><tr><th>Voce</th><th className="right">Importo</th></tr></thead>
              <tbody>
                {cedolino.righe.map((r, i) => (
                  <tr key={i} className={r.tipo}>
                    <td>{r.descrizione}</td>
                    <td className="right num">{r.tipo === "Trattenuta" ? "-" : ""}{euro(Math.abs(r.importo))}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="meta-strip">
            <span>Costo azienda <b>{euro(cedolino.costoAzienda)} €</b></span>
            <span>Quota TFR <b>{euro(cedolino.quotaTfr)} €</b></span>
          </div>
        </div>
      )}
    </section>
  );
}
