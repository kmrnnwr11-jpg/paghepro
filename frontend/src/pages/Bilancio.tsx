import { useEffect, useState } from "react";
import {
  api,
  type Azienda,
  type BilancioSintesi,
  type BilancioDettaglio,
  type VoceBilancio,
} from "../api";

const statoTone: Record<string, string> = {
  Bozza: "amber",
  Approvato: "blue",
  Depositato: "green",
  Annullato: "slate",
};

// Transizioni di stato consentite (allineate alla macchina a stati del backend).
const transizioni: Record<string, { stato: string; label: string }[]> = {
  Bozza: [
    { stato: "Approvato", label: "Approva" },
    { stato: "Annullato", label: "Annulla" },
  ],
  Approvato: [
    { stato: "Depositato", label: "Segna depositato" },
    { stato: "Bozza", label: "Riporta in bozza" },
    { stato: "Annullato", label: "Annulla" },
  ],
  Depositato: [],
  Annullato: [],
};

const eur = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";

export function Bilancio() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [aziendaId, setAziendaId] = useState("");
  const [anno, setAnno] = useState(new Date().getFullYear() - 1);
  const [lista, setLista] = useState<BilancioSintesi[]>([]);
  const [dettaglio, setDettaglio] = useState<BilancioDettaglio | null>(null);
  const [precompilate, setPrecompilate] = useState<VoceBilancio[] | null>(null);
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [caricando, setCaricando] = useState(false);

  useEffect(() => {
    api.aziende()
      .then((a) => {
        setAziende(a);
        if (a.length > 0) setAziendaId(a[0].id);
      })
      .catch((e) => setErrore((e as Error).message));
  }, []);

  useEffect(() => {
    if (aziendaId) void carica(aziendaId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aziendaId]);

  async function carica(id: string) {
    setCaricando(true);
    setErrore(null);
    try {
      setLista(await api.bilanci(id));
    } catch (e) {
      setErrore((e as Error).message);
    } finally {
      setCaricando(false);
    }
  }

  async function apri(id: string) {
    setErrore(null);
    try {
      setDettaglio(await api.bilancio(id));
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function creaEsempio() {
    if (!aziendaId) return setErrore("Seleziona prima un'azienda.");
    setErrore(null);
    setInfo(null);
    try {
      const b = await api.creaBilancioEsempio(aziendaId, Number(anno));
      setInfo(`Bilancio d'esempio ${b.anno} creato (utile ${eur(b.utilePerditaEsercizio)}).`);
      await carica(aziendaId);
      await apri(b.id);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function precompila() {
    if (!aziendaId) return setErrore("Seleziona prima un'azienda.");
    setErrore(null);
    setInfo(null);
    setPrecompilate(null);
    try {
      const r = await api.precompilaBilancio(aziendaId, Number(anno));
      setPrecompilate(r.voci);
      setInfo(r.voci.length === 0
        ? "Nessun dato di contabilità per l'anno scelto (fatture, spese o cedolini)."
        : `${r.voci.length} voci di Conto Economico ricavate dalla contabilità. ${r.nota}`);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function creaDaPrecompilate() {
    if (!aziendaId || !precompilate || precompilate.length === 0) return;
    setErrore(null);
    try {
      const b = await api.creaBilancio(aziendaId, { anno: Number(anno), voci: precompilate });
      setInfo(`Bozza di bilancio ${b.anno} creata dalla contabilità. Aggiungi le voci patrimoniali per la quadratura.`);
      setPrecompilate(null);
      await carica(aziendaId);
      await apri(b.id);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function cambiaStato(stato: string) {
    if (!dettaglio) return;
    setErrore(null);
    setInfo(null);
    try {
      await api.cambiaStatoBilancio(dettaglio.id, stato);
      await carica(aziendaId);
      await apri(dettaglio.id);
      setInfo(`Stato aggiornato → ${stato}.`);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Bilancio d'esercizio</h1>
            <p>Stato Patrimoniale e Conto Economico (schema CEE, art. 2424/2425 c.c.) per azienda e anno</p>
          </div>
          <label className="soft-button" style={{ gap: 8 }}>
            <span>🏢</span>
            <select
              value={aziendaId}
              onChange={(e) => { setAziendaId(e.target.value); setDettaglio(null); setPrecompilate(null); }}
              style={{ border: "none", background: "transparent", font: "inherit" }}
            >
              {aziende.length === 0 && <option value="">Nessuna azienda — creane una in “Aziende”</option>}
              {aziende.map((a) => (
                <option key={a.id} value={a.id}>{a.ragioneSociale}</option>
              ))}
            </select>
          </label>
        </div>

        {errore && <div className="status-pill red" style={{ display: "block", marginBottom: 12 }}>⚠️ {errore}</div>}
        {info && <div className="status-pill green" style={{ display: "block", marginBottom: 12 }}>✓ {info}</div>}

        <section className="glass-card page-card" style={{ marginBottom: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>Nuovo bilancio</h2>
              <p>Genera un esempio quadrato oppure precompila dal giro contabile registrato</p>
            </div>
            <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
              Anno
              <input
                type="number"
                value={anno}
                onChange={(e) => setAnno(Number(e.target.value))}
                style={{ width: 90 }}
              />
            </label>
          </div>
          <div className="actions-row" style={{ marginTop: 12, flexWrap: "wrap", gap: 8 }}>
            <button className="btn primary" type="button" onClick={() => void creaEsempio()}>
              + Bilancio d'esempio (quadrato)
            </button>
            <button className="btn secondary" type="button" onClick={() => void precompila()}>
              Precompila da contabilità
            </button>
          </div>

          {precompilate && precompilate.length > 0 && (
            <div style={{ marginTop: 12 }}>
              <table className="data-table">
                <thead>
                  <tr><th>Conto</th><th>Voce CEE</th><th className="right">Importo</th></tr>
                </thead>
                <tbody>
                  {precompilate.map((v) => (
                    <tr key={v.codice}>
                      <td><strong>{v.descrizione}</strong></td>
                      <td>{v.voce}</td>
                      <td className="right num">{eur(v.importo)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="actions-row" style={{ marginTop: 8 }}>
                <button className="btn primary" type="button" onClick={() => void creaDaPrecompilate()}>
                  Crea bozza con queste voci
                </button>
              </div>
            </div>
          )}
        </section>

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>Bilanci archiviati</h2>
              <p>{caricando ? "Caricamento…" : `${lista.length} bilanci`}</p>
            </div>
          </div>
          <div className="table-wrap">
            <table className="data-table spacious">
              <thead>
                <tr>
                  <th>Anno</th>
                  <th>Stato</th>
                  <th className="right">Utile/(perdita)</th>
                  <th>Quadratura</th>
                  <th className="right">Azioni</th>
                </tr>
              </thead>
              <tbody>
                {lista.length === 0 && (
                  <tr><td colSpan={5}>Nessun bilancio. Creane uno qui sopra.</td></tr>
                )}
                {lista.map((b) => (
                  <tr key={b.id}>
                    <td><strong>{b.anno}</strong></td>
                    <td><span className={`status-pill ${statoTone[b.stato] ?? "slate"}`}>{b.stato}</span></td>
                    <td className="right num">{eur(b.utilePerditaEsercizio)}</td>
                    <td>
                      <span className={`status-pill ${b.quadra ? "green" : "amber"}`}>
                        {b.quadra ? "Quadra" : `Sbilancio ${eur(b.sbilancioAttivoPassivo)}`}
                      </span>
                    </td>
                    <td className="right">
                      <button className="mini-action" type="button" onClick={() => void apri(b.id)}>Apri</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <aside className="glass-card module-rail">
        {dettaglio ? (
          <>
            <h2>Bilancio {dettaglio.anno}</h2>
            <div className="rail-metric"><span>Stato</span>
              <strong><span className={`status-pill ${statoTone[dettaglio.stato] ?? "slate"}`}>{dettaglio.stato}</span></strong>
            </div>
            <div className="rail-metric"><span>Valore produzione</span><strong>{eur(dettaglio.valoreDellaProduzione)}</strong></div>
            <div className="rail-metric"><span>Costi produzione</span><strong>{eur(dettaglio.costiDellaProduzione)}</strong></div>
            <div className="rail-metric"><span>Imposte</span><strong>{eur(dettaglio.imposte)}</strong></div>
            <div className="rail-metric"><span>Utile/(perdita)</span><strong>{eur(dettaglio.utilePerditaEsercizio)}</strong></div>
            <div className="rail-metric"><span>Totale attivo</span><strong>{eur(dettaglio.totaleAttivo)}</strong></div>
            <div className="rail-metric"><span>Totale passivo</span><strong>{eur(dettaglio.totalePassivo)}</strong></div>
            <div className="rail-metric"><span>Patrimonio netto</span><strong>{eur(dettaglio.patrimonioNetto)}</strong></div>
            <div className="rail-metric"><span>Quadratura</span>
              <strong><span className={`status-pill ${dettaglio.quadra ? "green" : "amber"}`}>{dettaglio.quadra ? "OK" : "No"}</span></strong>
            </div>

            {(transizioni[dettaglio.stato] ?? []).length > 0 && (
              <div className="actions-row" style={{ marginTop: 10, flexWrap: "wrap", gap: 6 }}>
                {(transizioni[dettaglio.stato] ?? []).map((t) => (
                  <button key={t.stato} className="mini-action" type="button" onClick={() => void cambiaStato(t.stato)}>
                    {t.label}
                  </button>
                ))}
              </div>
            )}

            <details style={{ marginTop: 12 }}>
              <summary style={{ cursor: "pointer", fontSize: 13 }}>Prospetto completo</summary>
              <pre style={{ whiteSpace: "pre-wrap", fontSize: 11, lineHeight: 1.4, marginTop: 8 }}>
                {dettaglio.bozza}
              </pre>
            </details>
          </>
        ) : (
          <>
            <h2>Come funziona</h2>
            <p style={{ fontSize: 13, lineHeight: 1.5, color: "var(--muted, #64748b)" }}>
              1. <strong>Bilancio d'esempio</strong>: crea uno schema CEE quadrato pronto da rivedere.<br />
              2. <strong>Precompila</strong>: porta ricavi e costi da fatture, spese e cedolini dell'anno.<br />
              3. Apri un bilancio per vedere il prospetto e gestirne lo stato
              (Bozza → Approvato → Depositato).
            </p>
            <p style={{ fontSize: 12, color: "var(--muted, #94a3b8)", marginTop: 8 }}>
              Il deposito ufficiale al Registro Imprese (formato XBRL) non è ancora generato.
            </p>
          </>
        )}
      </aside>
    </div>
  );
}
