import { useEffect, useMemo, useRef, useState } from "react";
import { api, type Azienda, type EsitoCaricamentoScadenza, type Promemoria } from "../api";
import { leggiAziendaCorrenteId } from "../aziendaCorrente";
import { apriLinkEsterno } from "../mobile/piattaforma";

// Servizio ufficiale "Durc On Line" (INPS/INAIL): la richiesta si fa SOLO lì, con
// SPID/CIE/CNS — non esiste un'API pubblica per inviarla da software esterni.
export const DURC_ONLINE_URL =
  "https://www.inps.it/it/it/dettaglio-scheda.it.schede-servizio-strumento.schede-servizi.50130.durc-online.html";

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
  Calendario: { label: "Scadenza", tone: "green" },
};

function inputDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function aggiungiGiorni(value: string, giorni: number): string {
  const base = new Date(`${value}T00:00:00`);
  if (Number.isNaN(base.getTime())) return "";
  base.setDate(base.getDate() + giorni);
  return inputDate(base);
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
  const [invioInCorso, setInvioInCorso] = useState<string | null>(null);
  const [statoInCorso, setStatoInCorso] = useState<string | null>(null);

  // Upload documento scadenza
  const [uploadInCorso, setUploadInCorso] = useState(false);
  const [esitoUpload, setEsitoUpload] = useState<EsitoCaricamentoScadenza | null>(null);
  const [aziendaUpload, setAziendaUpload] = useState("");
  const [dragOver, setDragOver] = useState(false);
  const inputFileRef = useRef<HTMLInputElement>(null);
  const richiestaCaricaRef = useRef(0);

  async function caricaDocumento(file: File) {
    const nome = file.name.toLowerCase();
    const formatoValido = file.type === "application/pdf" || nome.endsWith(".pdf");
    if (!formatoValido) {
      setEsitoUpload({
        aziendaTrovata: false,
        errore: "Carica un PDF DURC con testo leggibile. Le foto/scansioni senza OCR non sono supportate qui.",
      });
      return;
    }
    setUploadInCorso(true);
    setEsitoUpload(null);
    setErrore(null);
    try {
      const esito = await api.caricaDocumentoScadenza(file, aziendaUpload || undefined);
      setEsitoUpload(esito);
      if (esito.aziendaTrovata && !esito.errore) await carica();
    } catch (e) {
      setEsitoUpload({ aziendaTrovata: false, errore: e instanceof Error ? e.message : "Errore upload" });
    } finally {
      setUploadInCorso(false);
    }
  }

  // Form nuovo DURC
  const [aziendaDurc, setAziendaDurc] = useState("");
  const [numeroDurc, setNumeroDurc] = useState("");
  const [emissioneDurc, setEmissioneDurc] = useState(inputDate(new Date()));
  const [scadenzaDurc, setScadenzaDurc] = useState("");
  const [salvataggioDurc, setSalvataggioDurc] = useState(false);
  const scadenzaCalcolataDurc = useMemo(() => aggiungiGiorni(emissioneDurc, 120), [emissioneDurc]);
  const durcRegistrabile = aziende.length > 0 && numeroDurc.trim().length > 0 && emissioneDurc.trim().length > 0;

  // Form promemoria manuale
  const [titoloManuale, setTitoloManuale] = useState("");
  const [scadenzaManuale, setScadenzaManuale] = useState("");
  const [descrizioneManuale, setDescrizioneManuale] = useState("");
  const [aziendaManuale, setAziendaManuale] = useState("");
  const [salvataggioManuale, setSalvataggioManuale] = useState(false);
  const manualeRegistrabile = titoloManuale.trim().length > 0 && scadenzaManuale.trim().length > 0;

  async function carica() {
    const richiesta = ++richiestaCaricaRef.current;
    setCaricando(true);
    setErrore(null);
    try {
      const [az, lista] = await Promise.all([api.aziende(), api.scadenzario()]);
      if (richiesta !== richiestaCaricaRef.current) return;
      setAziende(az);
      setPromemoria(lista);
      setAziendaDurc((corrente) => {
        if (az.length === 0) return "";
        if (az.some((a) => a.id === corrente)) return corrente;
        const scelta = leggiAziendaCorrenteId();
        return scelta && az.some((a) => a.id === scelta) ? scelta : "";
      });
    } catch (e) {
      if (richiesta !== richiestaCaricaRef.current) return;
      setErrore(e instanceof Error ? e.message : "Errore di caricamento");
    } finally {
      if (richiesta === richiestaCaricaRef.current) setCaricando(false);
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
    setStatoInCorso(id);
    try {
      await api.cambiaStatoPromemoria(id, stato);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setStatoInCorso(null);
    }
  }

  async function registraDurc() {
    setErrore(null);
    setInfo(null);
    if (!aziendaDurc || !numeroDurc.trim() || !emissioneDurc) {
      setErrore("Seleziona l'azienda, inserisci il numero di protocollo e la data di emissione del DURC.");
      return;
    }
    setSalvataggioDurc(true);
    try {
      await api.registraDurc(aziendaDurc, {
        numero: numeroDurc.trim(),
        dataEmissione: emissioneDurc,
        // Se l'utente non forza una scadenza diversa, lascia calcolare il +120 giorni al backend.
        dataScadenza: scadenzaDurc || undefined,
      });
      await api.rigeneraScadenzario();
      setNumeroDurc("");
      setScadenzaDurc("");
      setInfo("DURC registrato: il promemoria di rinnovo è stato generato.");
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setSalvataggioDurc(false);
    }
  }

  async function creaManuale() {
    setErrore(null);
    setInfo(null);
    if (!manualeRegistrabile) {
      setErrore("Inserisci titolo e data scadenza del promemoria.");
      return;
    }
    setSalvataggioManuale(true);
    try {
      await api.creaPromemoria({
        titolo: titoloManuale.trim(),
        dataScadenza: scadenzaManuale,
        aziendaId: aziendaManuale || undefined,
        descrizione: descrizioneManuale.trim() || undefined,
        giorniPreavviso: 7,
      });
      setTitoloManuale("");
      setScadenzaManuale("");
      setDescrizioneManuale("");
      setInfo("Promemoria manuale creato nello scadenzario.");
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setSalvataggioManuale(false);
    }
  }

  async function invia(canale: string) {
    if (!documento) return;
    setEsitoInvio(null);
    setErrore(null);
    setInvioInCorso(canale);
    try {
      const r = await api.inviaPromemoria(documento.id, canale, destinatario || undefined);
      setEsitoInvio(r.inviato ? `✓ Inviato via ${r.canale}. ${r.dettaglio}` : `⚠ ${r.dettaglio}`);
      if (r.inviato) await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setInvioInCorso(null);
    }
  }

  async function copiaMessaggio(testo: string) {
    try {
      await navigator.clipboard.writeText(testo);
      setCopiato(true);
      setTimeout(() => setCopiato(false), 1500);
    } catch {
      setEsitoInvio("Non sono riuscito a copiare negli appunti: seleziona il testo e copialo manualmente.");
    }
  }

  function apriDocumento(p: Promemoria) {
    setDocumento(p);
    setDestinatario("");
    setEsitoInvio(null);
    setCopiato(false);
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
            {caricando ? "Aggiorno…" : "↻ Aggiorna scadenzario"}
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
          {promemoria.length === 0 ? (
            <div className="empty-state" style={{ display: "grid", gap: 10 }}>
              <strong>Nessuna scadenza ancora.</strong>
              <span>
                Primo passo: registra un DURC dal pannello a destra. La data di rinnovo viene calcolata
                automaticamente a 120 giorni e apparirà qui come promemoria.
              </span>
              <div className="actions-row">
                <button className="btn secondary compact" type="button" onClick={rigenera} disabled={caricando}>
                  Aggiorna scadenzario
                </button>
                <a className="btn primary compact" href={DURC_ONLINE_URL} onClick={(e) => {
                  e.preventDefault();
                  void apriLinkEsterno(DURC_ONLINE_URL);
                }}>
                  Apri portale DURC
                </a>
              </div>
            </div>
          ) : (
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
                          {p.aziendaId && (() => {
                            const az = aziende.find(a => a.id === p.aziendaId);
                            return az?.canalePredefinito ? (
                              <span style={{ marginLeft: 6, fontSize: 11, padding: "2px 6px",
                                borderRadius: 4, background: "rgba(39,174,96,0.15)",
                                color: "#27ae60", fontWeight: 600 }}>
                                ⚡ Auto {az.canalePredefinito}
                              </span>
                            ) : null;
                          })()}
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
                          <button
                            className="mini-action"
                            type="button"
                            aria-label={`Apri messaggio per ${p.titolo}`}
                            onClick={() => apriDocumento(p)}
                          >
                            Messaggio
                          </button>
                          {p.urgenza !== "chiuso" && (
                            <>
                              <button
                                className="mini-action"
                                type="button"
                                aria-label={`Completa promemoria ${p.titolo}`}
                                disabled={statoInCorso === p.id}
                                onClick={() => cambiaStato(p.id, "Completato")}
                              >
                                {statoInCorso === p.id ? "Salvo…" : "Completa"}
                              </button>
                              <button
                                className="mini-action"
                                type="button"
                                aria-label={`Annulla promemoria ${p.titolo}`}
                                disabled={statoInCorso === p.id}
                                onClick={() => cambiaStato(p.id, "Annullato")}
                              >
                                {statoInCorso === p.id ? "Salvo…" : "Annulla"}
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
          )}
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
            <textarea aria-label="Messaggio del promemoria" readOnly value={documento.messaggio} rows={10} />
            <label className="field">
              <span>Destinatario (email, oppure numero con prefisso es. +39333…)</span>
              <input
                value={destinatario}
                onChange={(e) => setDestinatario(e.target.value)}
                placeholder="mario@studio.it  ·  +393331112233"
              />
            </label>
            <div className="actions-row">
              <button className="btn primary" type="button" onClick={() => invia("Email")} disabled={invioInCorso !== null}>
                {invioInCorso === "Email" ? "Invio…" : "✉️ Invia email"}
              </button>
              <button className="btn primary" type="button" onClick={() => invia("Sms")} disabled={invioInCorso !== null}>
                {invioInCorso === "Sms" ? "Invio…" : "💬 Invia SMS"}
              </button>
              <button className="btn primary" type="button" onClick={() => invia("WhatsApp")} disabled={invioInCorso !== null}>
                {invioInCorso === "WhatsApp" ? "Invio…" : "🟢 Invia WhatsApp"}
              </button>
            </div>
            {esitoInvio && <div className="alert info" role="status" aria-live="polite">{esitoInvio}</div>}
            <p className="muted">
              Se un canale non è ancora configurato, il sistema lo segnala qui: i messaggi restano pronti da
              copiare. Le credenziali (SMTP / Twilio / Meta) si impostano nella sezione “Notifiche”.
            </p>
          </section>
        )}
      </div>

      <aside className="glass-card module-rail">
        <h2>Richiedi DURC</h2>
        <p className="muted">
          La richiesta si fa gratis sul portale ufficiale INPS/INAIL, entrando con SPID, CIE o CNS.
          Il nuovo DURC vale 120 giorni.
        </p>
        <a className="btn primary" href={DURC_ONLINE_URL} onClick={(e) => {
          e.preventDefault();
          void apriLinkEsterno(DURC_ONLINE_URL);
        }}>
          🌐 Apri portale DURC
        </a>
        <div className="empty-state" style={{ marginTop: 12 }}>
          Questo software non richiede il DURC al posto tuo: ti aiuta a registrarlo e a ricordare
          il rinnovo dopo averlo scaricato dal portale ufficiale.
        </div>

        <h2 style={{ marginTop: 18 }}>Registra DURC</h2>
        <p className="muted">La scadenza genera il promemoria di rinnovo.</p>
        <label className="field">
          <span>Azienda</span>
          <select value={aziendaDurc} onChange={(e) => setAziendaDurc(e.target.value)} disabled={aziende.length === 0}>
            {aziende.length === 0 && <option value="">Nessuna azienda registrata</option>}
            {aziende.length > 0 && <option value="">Scegli azienda</option>}
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
          <span>Scadenza, se diversa da 120 giorni</span>
          <input type="date" value={scadenzaDurc} onChange={(e) => setScadenzaDurc(e.target.value)} />
        </label>
        <p className="muted" style={{ marginTop: -6 }}>
          Se lasci vuoto, il sistema userà {dataIt(scadenzaCalcolataDurc)}.
        </p>
        {!durcRegistrabile && (
          <div className="empty-state" style={{ margin: "10px 0" }}>
            {aziende.length === 0
              ? "Prima aggiungi almeno un'azienda dalla pagina Aziende."
              : "Inserisci protocollo e data emissione per abilitare il salvataggio."}
          </div>
        )}
        <button className="btn primary" type="button" onClick={registraDurc} disabled={!durcRegistrabile || salvataggioDurc}>
          {salvataggioDurc ? "Salvataggio…" : "Registra DURC"}
        </button>

        <h2 style={{ marginTop: 24 }}>Promemoria manuale</h2>
        <p className="muted">Per scadenze interne dello studio non generate in automatico.</p>
        <label className="field">
          <span>Titolo</span>
          <input value={titoloManuale} onChange={(e) => setTitoloManuale(e.target.value)} placeholder="es. Rinnovo delega cliente" />
        </label>
        <label className="field">
          <span>Data scadenza</span>
          <input type="date" value={scadenzaManuale} onChange={(e) => setScadenzaManuale(e.target.value)} />
        </label>
        {aziende.length > 0 && (
          <label className="field">
            <span>Azienda (facoltativa)</span>
            <select value={aziendaManuale} onChange={(e) => setAziendaManuale(e.target.value)}>
              <option value="">Nessuna azienda specifica</option>
              {aziende.map((a) => (
                <option key={a.id} value={a.id}>{a.ragioneSociale}</option>
              ))}
            </select>
          </label>
        )}
        <label className="field">
          <span>Nota (facoltativa)</span>
          <textarea
            value={descrizioneManuale}
            onChange={(e) => setDescrizioneManuale(e.target.value)}
            rows={3}
            placeholder="Cosa deve ricordare lo studio?"
          />
        </label>
        <button className="btn secondary" type="button" onClick={creaManuale} disabled={!manualeRegistrabile || salvataggioManuale}>
          {salvataggioManuale ? "Salvataggio…" : "Crea promemoria"}
        </button>

        <h2 style={{ marginTop: 24 }}>Carica documento scadenza</h2>
        <p className="muted">
          Carica un DURC PDF testuale: il sistema legge P.IVA e date, collega l'azienda e crea il
          promemoria automaticamente.
        </p>

        {aziende.length > 0 && (
          <label className="field">
            <span>Azienda (se non trovata nel PDF)</span>
            <select value={aziendaUpload} onChange={e => setAziendaUpload(e.target.value)}>
              <option value="">— Rileva automaticamente —</option>
              {aziende.map(a => <option key={a.id} value={a.id}>{a.ragioneSociale}</option>)}
            </select>
          </label>
        )}

        <div
          role="button"
          tabIndex={0}
          aria-label="Scegli PDF DURC da caricare"
          onClick={() => !uploadInCorso && inputFileRef.current?.click()}
          onKeyDown={e => {
            if ((e.key === "Enter" || e.key === " ") && !uploadInCorso) {
              e.preventDefault();
              inputFileRef.current?.click();
            }
          }}
          onDragOver={e => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={e => {
            e.preventDefault(); setDragOver(false);
            const f = e.dataTransfer.files[0];
            if (f) void caricaDocumento(f);
          }}
          style={{
            border: `2px dashed ${dragOver ? "var(--accent, #6c63ff)" : "var(--border, #444)"}`,
            borderRadius: 10, padding: "18px 12px", textAlign: "center",
            cursor: uploadInCorso ? "wait" : "pointer",
            background: dragOver ? "rgba(14,114,104,0.08)" : "transparent",
            transition: "all 0.2s",
          }}
        >
          {uploadInCorso
            ? "⏳ Lettura in corso…"
            : "📎 Scegli PDF o trascinalo qui"}
        </div>

        <input
          ref={inputFileRef}
          type="file"
          accept="application/pdf,.pdf"
          style={{ display: "none" }}
          onChange={e => { const f = e.target.files?.[0]; if (f) void caricaDocumento(f); e.target.value = ""; }}
        />

        {esitoUpload && (
          <div style={{ marginTop: 10, fontSize: 13, lineHeight: 1.6,
            padding: "10px 12px", borderRadius: 8,
            background: esitoUpload.errore && !esitoUpload.aziendaTrovata
              ? "rgba(231,76,60,0.1)" : "rgba(39,174,96,0.1)" }}>
            {esitoUpload.aziendaTrovata ? (
              <>
                ✅ <b>{esitoUpload.ragioneSociale}</b><br />
                Scadenza: <b>{esitoUpload.scadenza ?? "—"}</b><br />
                {esitoUpload.promemoriaCreatoId
                  ? "Promemoria creato nello scadenzario."
                  : "Nessun promemoria creato."}
                {esitoUpload.errore && <><br />⚠ {esitoUpload.errore}</>}
              </>
            ) : (
              <>⚠ {esitoUpload.errore}</>
            )}
          </div>
        )}
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
