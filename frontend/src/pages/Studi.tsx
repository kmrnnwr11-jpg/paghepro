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
    <section style={{ maxWidth: 840 }}>
      <h1>🏢 Studi (super-admin)</h1>
      <p style={{ color: "#555" }}>
        Tutti gli studi della piattaforma. Da qui puoi <strong>sospendere</strong> o{" "}
        <strong>riattivare</strong> l'accesso di ciascuno: uno studio sospeso non può più entrare.
      </p>

      {errore && <p style={{ color: "#c0392b" }}>{errore}</p>}

      {caricamento ? (
        <p>Caricamento…</p>
      ) : (
        <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 12 }}>
          <thead>
            <tr style={{ textAlign: "left", borderBottom: "2px solid #e0e0e0" }}>
              <th style={{ padding: "8px 6px" }}>Studio</th>
              <th style={{ padding: "8px 6px" }}>Utenti</th>
              <th style={{ padding: "8px 6px" }}>Stato</th>
              <th style={{ padding: "8px 6px" }} aria-label="Azioni" />
            </tr>
          </thead>
          <tbody>
            {studi.map((s) => (
              <tr key={s.id} style={{ borderBottom: "1px solid #eee" }}>
                <td style={{ padding: "8px 6px" }}>{s.ragioneSociale}</td>
                <td style={{ padding: "8px 6px" }}>{s.numeroUtenti}</td>
                <td style={{ padding: "8px 6px" }}>
                  <span
                    style={{
                      padding: "2px 10px",
                      borderRadius: 999,
                      fontSize: 13,
                      background: s.sospeso ? "#fdecea" : "#e8f5e9",
                      color: s.sospeso ? "#c0392b" : "#2e7d32",
                    }}
                  >
                    {s.sospeso ? "Sospeso" : "Attivo"}
                  </span>
                </td>
                <td style={{ padding: "8px 6px" }}>
                  <button
                    type="button"
                    onClick={() => void cambiaStato(s)}
                    disabled={inCorso === s.id}
                    style={{
                      padding: "6px 14px",
                      borderRadius: 8,
                      border: "none",
                      cursor: "pointer",
                      color: "#fff",
                      background: s.sospeso ? "#2e7d32" : "#c0392b",
                      opacity: inCorso === s.id ? 0.6 : 1,
                    }}
                  >
                    {inCorso === s.id ? "…" : s.sospeso ? "Riattiva" : "Sospendi"}
                  </button>
                </td>
              </tr>
            ))}
            {studi.length === 0 && (
              <tr>
                <td colSpan={4} style={{ padding: 12, color: "#888" }}>
                  Nessuno studio registrato.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  );
}
