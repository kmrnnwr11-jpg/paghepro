import { useEffect, useState } from "react";
import { api, type StudioSintesi } from "../api";

/// Pannello del super-admin di piattaforma: elenco di tutti gli studi con possibilità di
/// sospenderne/riattivarne l'accesso. Visibile solo a chi ha ruolo "superadmin".
export function Studi() {
  const [studi, setStudi] = useState<StudioSintesi[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [inCorso, setInCorso] = useState<string | null>(null);

  const carica = async () => {
    setCaricamento(true);
    setErrore(null);
    try {
      setStudi(await api.elencaStudi());
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel caricamento");
    } finally {
      setCaricamento(false);
    }
  };

  useEffect(() => {
    void carica();
  }, []);

  const cambiaStato = async (s: StudioSintesi) => {
    setInCorso(s.id);
    setErrore(null);
    try {
      if (s.sospeso) await api.riattivaStudio(s.id);
      else await api.sospendiStudio(s.id);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Operazione non riuscita");
    } finally {
      setInCorso(null);
    }
  };

  return (
    <section className="detail-layout">
      <div className="page-heading">
        <div>
          <h1>🏢 Studi (super-admin)</h1>
          <p>
            Tutti gli studi della piattaforma. Da qui puoi <strong>sospendere</strong> o{" "}
            <strong>riattivare</strong> l'accesso di ciascuno: uno studio sospeso non può più entrare.
          </p>
        </div>
      </div>

      {errore && <span className="errore">{errore}</span>}

      <div className="glass-card page-card">
        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : (
          <div className="table-wrap">
            <table className="data-table spacious">
              <thead>
                <tr>
                  <th>Studio</th>
                  <th>Utenti</th>
                  <th>Stato</th>
                  <th aria-label="Azioni" />
                </tr>
              </thead>
              <tbody>
                {studi.map((s) => (
                  <tr key={s.id}>
                    <td><strong>{s.ragioneSociale}</strong></td>
                    <td className="num">{s.numeroUtenti}</td>
                    <td>
                      <span className={`status-pill ${s.sospeso ? "red" : "green"}`}>
                        {s.sospeso ? "Sospeso" : "Attivo"}
                      </span>
                    </td>
                    <td className="right">
                      <button
                        type="button"
                        className="mini-action"
                        onClick={() => void cambiaStato(s)}
                        disabled={inCorso === s.id}
                        style={{ color: s.sospeso ? "var(--green)" : "var(--red)" }}
                      >
                        {inCorso === s.id ? "…" : s.sospeso ? "Riattiva" : "Sospendi"}
                      </button>
                    </td>
                  </tr>
                ))}
                {studi.length === 0 && (
                  <tr>
                    <td colSpan={4}>Nessuno studio registrato.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
