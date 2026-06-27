import { useEffect, useMemo, useState } from "react";
import { api, type Azienda, type Promemoria } from "../api";

const urgenzaMeta: Record<string, { label: string; tone: string }> = {
  scaduto: { label: "Scaduto", tone: "red" },
  oggi: { label: "Scade oggi", tone: "red" },
  in_scadenza: { label: "In scadenza", tone: "amber" },
  futuro: { label: "Futuro", tone: "blue" },
  chiuso: { label: "Chiuso", tone: "slate" },
};

const tipoMeta: Record<string, { label: string; tone: string }> = {
  Durc: { label: "DURC", tone: "violet" },
  F24: { label: "F24", tone: "amber" },
  Manuale: { label: "Promemoria", tone: "blue" },
};

function inputDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function dataIt(value?: string | null): string {
  if (!value) return "—";
  return new Date(`${value}T00:00:00`).toLocaleDateString("it-IT");
}

function scadenzaLabel(p: Promemoria): string {
  if (p.urgenza === "chiuso") return p.stato;
  if (p.giorniAllaScadenza < 0) return `${Math.abs(p.giorniAllaScadenza)} gg in ritardo`;
  if (p.giorniAllaScadenza === 0) return "Scade oggi";
  return `tra ${p.giorniAllaScadenza} gg`;
}

export function Scadenzario() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [promemoria, setPromemoria] = useState<Promemoria[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [caricando, setCaricando] = useState(false);
  const [documento, setDocumento] = useState<Promemoria | null>(null);
  const [copiato, setCopiato] = useState(false);
  const [destinatario, setDestinatario] = useState("");
  const [esitoInvio, setEsitoInvio] = useState<string | null>(null);

  // Form nuovo DURC
  const [aziendaDurc, setAziendaDurc] = useState("");
  const [numeroDurc, setNumeroDurc] = useState("");
  const [emissioneDurc, setEmissioneDurc] = useState(inputDate(new Date()));
  const [scadenzaDurc, setScadenzaDurc] = useState("");

  async function carica() {
    setCaricando(true);
    setErrore(null);
    try {
      const [az, lista] = await Promise.all([api.aziende(), api.scadenzario()]);
      setAziende(az);
      setPromemoria(lista);
      if (az.length && !aziendaDurc) setAziendaDurc(az[0].id);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore di caricamento");
    } finally {
      setCaricando(false);
    }
  }

  useEffect(() => {
    void carica();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const conteggi = useMemo(() => {
    const attivi = promemoria.filter((p) => p.urgenza !== "chiuso");
    return {
      scaduti: attivi.filter((p) => p.urgenza === "scaduto").length,
      oggi: attivi.filter((p) => p.urgenza === "oggi").length,
      inScadenza: attivi.filter((p) => p.urgenza === "in_scadenza").length,
      totali: attivi.length,
    };
  }, [promemoria]);

  async function rigenera() {
    setInfo(null);
    setErrore(null);
    try {
      const r = await api.rigeneraScadenzario();
      setInfo(`Scadenzario aggiornato (${r.aggiornati} voci ricalcolate).`);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  async function cambiaStato(id: string, stato: string) {
    setErrore(null);
    try {
      await api.cambiaStatoPromemoria(id, stato);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  async function registraDurc() {
    setErrore(null);
    setInfo(null);
    if (!aziendaDurc || !numeroDurc.trim()) {
      setErrore("Seleziona l'azienda e inserisci il numero di protocollo del DURC.");
      return;
    }
    try {
      await api.registraDurc(aziendaDurc, {
        numero: numeroDurc.trim(),
        dataEmissione: emissioneDurc,
        dataScadenza: scadenzaDurc || undefined,
      });
      setNumeroDurc("");
      setScadenzaDurc("");
      setInfo("DURC registrato: il promemoria di rinnovo è stato generato.");
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  async function invia(canale: string) {
    if (!documento) return;
    setEsitoInvio(null);
    setErrore(null);
    try {
      const r = await api.inviaPromemoria(documento.id, canale, destinatario || undefined);
      setEsitoInvio(r.inviato ? `✓ Inviato via ${r.canale}. ${r.dettaglio}` : `⚠ ${r.dettaglio}`);
      if (r.inviato) await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  async function copiaMessaggio(testo: string) {
    try {
      await navigator.clipboard.writeText(testo);
      setCopiato(true);
      setTimeout(() => setCopiato(false), 1500);
    } catch {
      /* clipboard non disponibile */
    }
  }

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Scadenzario</h1>
            <p>Promemoria automatici per DURC e F24 · con messaggio già pronto</p>
          </div>
          <button className="btn primary" type="button" onClick={rigenera} disabled={caricando}>
            ↻ Rigenera
          </button>
        </div>

        {errore && <div className="alert error">{errore}</div>}
        {info && <div className="alert info">{info}</div>}

        <div className="metric-strip">
          <Metric label="Scaduti" value={String(conteggi.scaduti)} />
          <Metric label="Scade oggi" value={String(conteggi.oggi)} />
          <Metric label="In scadenza" value={String(conteggi.inScadenza)} />
          <Metric label="Attivi" value={String(conteggi.totali)} />
        </div>

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>Promemoria</h2>
              <p>Generati in automatico dalle scadenze DURC e dai versamenti F24 archiviati</p>
            </div>
          </div>
          <div className="table-wrap">
            <table className="data-table spacious">
              <thead>
                <tr>
                  <th>Tipo</th>
                  <th>Promemoria</th>
                  <th>Scadenza</th>
                  <th>Stato</th>
                  <th className="right">Azioni</th>
                </tr>
              </thead>
              <tbody>
                {promemoria.length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted">
                      Nessuna scadenza. Registra un DURC o archivia un F24 e premi “Rigenera”.
                    </td>
                  </tr>
                )}
                {promemoria.map((p) => {
                  const tipo = tipoMeta[p.tipo] ?? { label: p.tipo, tone: "slate" };
                  const urg = urgenzaMeta[p.urgenza] ?? { label: p.urgenza, tone: "slate" };
                  return (
                    <tr key={p.id}>
                      <td>
                        <span className={`tag ${tipo.tone}`}>{tipo.label}</span>
                      </td>
                      <td>
                        <strong>{p.titolo}</strong>
                        <div className="muted">{p.descrizione}</div>
                      </td>
                      <td className="num">
                        {dataIt(p.dataScadenza)}
                        <div className="muted">{scadenzaLabel(p)}</div>
                      </td>
                      <td>
                        <span className={`status-pill ${urg.tone}`}>{urg.label}</span>
                        {p.stato !== "Aperto" && <div className="muted">{p.stato}</div>}
                      </td>
                      <td className="right">
                        <button className="mini-action" type="button" onClick={() => setDocumento(p)}>
                          Messaggio
                        </button>
                        {p.urgenza !== "chiuso" && (
                          <>
                            <button
                              className="mini-action"
                              type="button"
                              onClick={() => cambiaStato(p.id, "Completato")}
                            >
                              Completa
                            </button>
                            <button
                              className="mini-action"
                              type="button"
                              onClick={() => cambiaStato(p.id, "Annullato")}
                            >
                              Annulla
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>

        {documento && (
          <section className="glass-card page-card">
            <div className="card-title-row">
              <div>
                <h2>{documento.titolo}</h2>
                <p>Messaggio precompilato — copialo, oppure invialo via Email / SMS / WhatsApp</p>
              </div>
              <div className="actions-row">
                <button className="btn secondary" type="button" onClick={() => copiaMessaggio(documento.messaggio)}>
                  {copiato ? "Copiato ✓" : "Copia"}
                </button>
                <button className="btn secondary" type="button" onClick={() => setDocumento(null)}>
                  Chiudi
                </button>
              </div>
            </div>
            <textarea readOnly value={documento.messaggio} rows={10} />
            <label className="field">
              <span>Destinatario (email, oppure numero con prefisso es. +39333…)</span>
              <input
                value={destinatario}
                onChange={(e) => setDestinatario(e.target.value)}
                placeholder="mario@studio.it  ·  +393331112233"
              />
            </label>
            <div className="actions-row">
              <button className="btn primary" type="button" onClick={() => invia("Email")}>
                ✉️ Invia email
              </button>
              <button className="btn primary" type="button" onClick={() => invia("Sms")}>
                💬 Invia SMS
              </button>
              <button className="btn primary" type="button" onClick={() => invia("WhatsApp")}>
                🟢 Invia WhatsApp
              </button>
            </div>
            {esitoInvio && <div className="alert info">{esitoInvio}</div>}
            <p className="muted">
              Se un canale non è ancora configurato, il sistema lo segnala qui: i messaggi restano pronti da
              copiare. Le credenziali (SMTP / Twilio / Meta) si impostano nella sezione “Notifiche”.
            </p>
          </section>
        )}
      </div>

      <aside className="glass-card module-rail">
        <h2>Registra DURC</h2>
        <p className="muted">La scadenza genera il promemoria di rinnovo.</p>
        <label className="field">
          <span>Azienda</span>
          <select value={aziendaDurc} onChange={(e) => setAziendaDurc(e.target.value)}>
            {aziende.map((a) => (
              <option key={a.id} value={a.id}>
                {a.ragioneSociale}
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>N. protocollo</span>
          <input value={numeroDurc} onChange={(e) => setNumeroDurc(e.target.value)} placeholder="es. PROT-2026-001" />
        </label>
        <label className="field">
          <span>Data emissione</span>
          <input type="date" value={emissioneDurc} onChange={(e) => setEmissioneDurc(e.target.value)} />
        </label>
        <label className="field">
          <span>Scadenza (opz., +120 gg)</span>
          <input type="date" value={scadenzaDurc} onChange={(e) => setScadenzaDurc(e.target.value)} />
        </label>
        <button className="btn primary" type="button" onClick={registraDurc} disabled={aziende.length === 0}>
          Registra DURC
        </button>
      </aside>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
