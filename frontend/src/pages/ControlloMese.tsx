import { useEffect, useState } from "react";
import { ChevronDown, ChevronRight, Gauge } from "lucide-react";
import { api, type CruscottoMese, type StatoCruscottoRapporto } from "../api";

const oggi = new Date();
const MESI = ["", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
  "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

const SEMAFORO: Record<StatoCruscottoRapporto, { classe: string; etichetta: string }> = {
  protetto: { classe: "green", etichetta: "Chiuso" },
  gia_elaborato: { classe: "green", etichetta: "Elaborato" },
  cartellino_presente: { classe: "blue", etichetta: "Pronto" },
  da_compilare: { classe: "amber", etichetta: "Da compilare" },
  agricolo: { classe: "amber", etichetta: "Agricolo da fare" },
  bloccato: { classe: "red", etichetta: "Bloccato" },
};

/**
 * Cruscotto del mese di tutto lo studio: una vista sola con i semafori di ogni azienda,
 * per capire a colpo d'occhio chi è pronto, chi manca e chi è bloccato — senza aprire
 * le aziende una per una.
 */
export function ControlloMese() {
  const [anno, setAnno] = useState(oggi.getFullYear());
  const [mese, setMese] = useState(oggi.getMonth() + 1);
  const [cruscotto, setCruscotto] = useState<CruscottoMese | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);
  const [aperte, setAperte] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const controller = new AbortController();
    setCaricamento(true);
    setErrore(null);
    api.cruscottoMese(anno, mese, controller.signal)
      .then((dati) => setCruscotto(dati))
      .catch((e) => {
        if (controller.signal.aborted) return;
        setCruscotto(null);
        setErrore(e instanceof Error ? e.message : "Cruscotto non disponibile.");
      })
      .finally(() => { if (!controller.signal.aborted) setCaricamento(false); });
    return () => controller.abort();
  }, [anno, mese]);

  const totali = (cruscotto?.aziende ?? []).reduce(
    (somma, azienda) => ({
      elaborati: somma.elaborati + azienda.elaborati,
      pronti: somma.pronti + azienda.pronti,
      daCompilare: somma.daCompilare + azienda.daCompilare,
      bloccati: somma.bloccati + azienda.bloccati,
      agricoli: somma.agricoli + azienda.agricoli,
    }),
    { elaborati: 0, pronti: 0, daCompilare: 0, bloccati: 0, agricoli: 0 },
  );

  return (
    <div className="page">
      <section className="glass-card">
        <header className="page-head">
          <div>
            <h1><Gauge size={22} aria-hidden="true" /> Controllo del mese</h1>
            <p className="muto">
              Tutte le aziende dello studio in una vista sola: verde elaborato, blu pronto,
              giallo da completare, rosso bloccato. Sola lettura: si elabora dalle pagine
              Presenze e Agricoltura OTD.
            </p>
          </div>
          <div className="page-actions">
            <label>
              Mese{" "}
              <select value={mese} onChange={(e) => setMese(Number(e.target.value))}>
                {MESI.slice(1).map((nome, indice) => (
                  <option key={nome} value={indice + 1}>{nome}</option>
                ))}
              </select>
            </label>
            <label>
              Anno{" "}
              <input
                type="number" min={2020} max={2100} value={anno}
                onChange={(e) => setAnno(Number(e.target.value))}
              />
            </label>
          </div>
        </header>

        {caricamento && <p className="muto">Controllo delle aziende in corso…</p>}
        {errore && <p className="error">{errore}</p>}

        {cruscotto && !caricamento && (
          <>
            <div className="riepilogo-inline" aria-label="Totali del mese">
              <span className="status-pill green">{totali.elaborati} elaborati</span>{" "}
              <span className="status-pill blue">{totali.pronti} pronti</span>{" "}
              <span className="status-pill amber">{totali.daCompilare} da compilare</span>{" "}
              <span className="status-pill amber">{totali.agricoli} agricoli da fare</span>{" "}
              <span className="status-pill red">{totali.bloccati} bloccati</span>
            </div>
            {cruscotto.elencoTagliato && (
              <p className="muto">
                Mostrate le prime {cruscotto.aziende.length} aziende
                su {cruscotto.aziendeTotali}.
              </p>
            )}
            {cruscotto.aziende.length === 0 && (
              <p className="muto">Nessuna azienda in archivio: il cruscotto si riempie da solo
                appena registri il primo cliente.</p>
            )}

            {cruscotto.aziende.map((azienda) => {
              const aperta = aperte[azienda.aziendaId] ?? false;
              const tuttoFatto = azienda.rapporti.length > 0
                && azienda.elaborati === azienda.rapporti.length;
              return (
                <article className="glass-card" key={azienda.aziendaId}>
                  <button
                    type="button"
                    className="btn ghost"
                    aria-expanded={aperta}
                    onClick={() => setAperte((a) => ({ ...a, [azienda.aziendaId]: !aperta }))}
                  >
                    {aperta
                      ? <ChevronDown size={16} aria-hidden="true" />
                      : <ChevronRight size={16} aria-hidden="true" />}
                    <strong>{azienda.ragioneSociale}</strong>
                    {" "}
                    {azienda.rapporti.length === 0 && (
                      <span className="status-pill slate">Nessun rapporto nel mese</span>
                    )}
                    {tuttoFatto && <span className="status-pill green">Mese completo</span>}
                    {azienda.elaborati > 0 && !tuttoFatto && (
                      <span className="status-pill green">{azienda.elaborati}</span>
                    )}
                    {azienda.pronti > 0 && (
                      <span className="status-pill blue">{azienda.pronti}</span>
                    )}
                    {(azienda.daCompilare > 0 || azienda.agricoli > 0) && (
                      <span className="status-pill amber">
                        {azienda.daCompilare + azienda.agricoli}
                      </span>
                    )}
                    {azienda.bloccati > 0 && (
                      <span className="status-pill red">{azienda.bloccati}</span>
                    )}
                  </button>
                  {aperta && azienda.rapporti.length > 0 && (
                    <div className="table-wrap">
                      <table className="data-table">
                        <thead>
                          <tr><th>Lavoratore</th><th>Stato</th><th>Perché</th></tr>
                        </thead>
                        <tbody>
                          {azienda.rapporti.map((riga) => (
                            <tr key={riga.rapportoId}>
                              <td>{riga.nominativo}</td>
                              <td>
                                <span className={`status-pill ${SEMAFORO[riga.stato].classe}`}>
                                  {SEMAFORO[riga.stato].etichetta}
                                </span>
                              </td>
                              <td className="muto">{riga.motivo}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </article>
              );
            })}
          </>
        )}
      </section>
    </div>
  );
}
