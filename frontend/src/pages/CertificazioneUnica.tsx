import { useEffect, useState } from "react";
import { api, type Azienda, type CuBozza, type DipendenteRapporto } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function CertificazioneUnica() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [anno, setAnno] = useState(2026);
  const [cu, setCu] = useState<CuBozza | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);

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
    if (!rapportoId) { setCu(null); return; }
    api.cuBozza(rapportoId, anno)
      .then(setCu)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [rapportoId, anno]);

  const righe: [string, number, string?][] = cu
    ? [
        ["Imponibile IRPEF (redditi certificati)", cu.totaleImponibileIrpef],
        ["Ritenute IRPEF", cu.totaleRitenuteIrpef],
        ["Addizionale regionale", cu.totaleAddizionaleRegionale],
        ["Addizionale comunale", cu.totaleAddizionaleComunale],
        ["Imponibile previdenziale INPS", cu.totaleImponibileInps],
      ]
    : [];

  return (
    <div className="module-layout">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Certificazione Unica</h1>
            <p>Bozza dai cedolini archiviati{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 1 dichiarazioni</p>
          </div>
          <span className="status-pill amber">Bozza</span>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : dipendenti.length === 0 ? (
          <div className="empty-state">
            Nessun dipendente. Aggiungine uno in “Dipendenti”, poi archivia i cedolini dell'anno: la CU si compone da soli.
          </div>
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
              <label>
                <span>Anno d'imposta</span>
                <input type="number" min={2000} max={2100} value={anno} onChange={(e) => setAnno(Number(e.target.value))} />
              </label>
            </div>

            {cu && (
              <div className="payroll-result">
                <div className="result-summary">
                  <div>
                    <span>Percipiente</span>
                    <strong style={{ fontSize: 15 }}>{cu.nominativo || "—"}</strong>
                  </div>
                  <div>
                    <span>Codice fiscale</span>
                    <strong className="num" style={{ fontSize: 14 }}>{cu.codiceFiscale || "—"}</strong>
                  </div>
                  <div>
                    <span>Cedolini anno {cu.anno}</span>
                    <strong className="num">{cu.numeroCedolini}</strong>
                  </div>
                </div>

                {cu.numeroCedolini === 0 ? (
                  <div className="empty-state">
                    Nessun cedolino archiviato per il {cu.anno}. Archivia i cedolini del dipendente e la CU si popola in automatico.
                  </div>
                ) : (
                  <div className="table-wrap">
                    <table className="data-table compact">
                      <thead><tr><th>Voce certificata</th><th className="right">Importo annuo</th></tr></thead>
                      <tbody>
                        {righe.map(([label, val]) => (
                          <tr key={label}>
                            <td>{label}</td>
                            <td className="right num">{euro(val)} €</td>
                          </tr>
                        ))}
                        <tr className="Trattenuta">
                          <td><strong>Netto erogato (somma cedolini)</strong></td>
                          <td className="right num"><strong>{euro(cu.totaleNetto)} €</strong></td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Cos'è questa schermata</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          È la <b>bozza</b> della Certificazione Unica: somma i cedolini dell'anno nei totali certificati.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
        <p className="muto" style={{ fontSize: 12, marginTop: 10, fontWeight: 600 }}>
          Il file telematico ufficiale e l'invio richiedono le specifiche dell'Agenzia Entrate e un canale Entratel. Vedi PIANO-DICHIARAZIONI.md.
        </p>
      </aside>
    </div>
  );
}
