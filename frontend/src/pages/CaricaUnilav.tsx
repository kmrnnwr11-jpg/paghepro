import { useEffect, useRef, useState } from "react";
import { api, getToken, type Cedolino, type EsitoImportUnilav } from "../api";

const BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? "http://localhost:5171";
const MESI = ["Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
  "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

// Upload diretto (multipart) con azienda scelta opzionale.
async function inviaUnilav(file: File, aziendaId: string): Promise<EsitoImportUnilav> {
  const form = new FormData();
  form.append("file", file);
  if (aziendaId) form.append("aziendaId", aziendaId);
  const token = getToken();
  const headers: Record<string, string> = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;
  const res = await fetch(`${BASE}/api/unilav/pdf`, { method: "POST", body: form, headers });
  if (!res.ok) {
    let messaggio = `Errore ${res.status}`;
    try {
      const body = await res.json();
      if (body?.errore) messaggio = body.errore as string;
    } catch {
      /* corpo non JSON */
    }
    throw new Error(messaggio);
  }
  return (await res.json()) as EsitoImportUnilav;
}

// Elenco dei CCNL configurati nel motore (per il menu della busta paga).
async function listaCcnl(): Promise<{ codice: string; descrizione: string }[]> {
  const token = getToken();
  const headers: Record<string, string> = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;
  const res = await fetch(`${BASE}/api/ccnl`, { headers });
  if (!res.ok) return [];
  return (await res.json()) as { codice: string; descrizione: string }[];
}

/// "Trascina l'UNILAV e fa tutto": scegli l'azienda (o automatico dal PDF), il sistema registra
/// azienda + lavoratore + rapporto, poi con un clic fai la busta paga del mese.
export function CaricaUnilav({ onImportato }: { onImportato?: (azienda: { id: string; ragioneSociale: string }) => void }) {
  const [aziende, setAziende] = useState<{ id: string; ragioneSociale: string }[]>([]);
  const [aziendaScelta, setAziendaScelta] = useState(""); // "" = automatico dal PDF
  const [trascina, setTrascina] = useState(false);
  const [caricamento, setCaricamento] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);
  const [esito, setEsito] = useState<EsitoImportUnilav | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => { api.aziende().then(setAziende).catch(() => { /* ignora */ }); }, []);

  async function gestisci(file: File | undefined | null) {
    if (!file) return;
    const nome = file.name.toLowerCase();
    const ok = nome.endsWith(".pdf") || file.type.startsWith("image/")
      || /\.(jpe?g|png|webp|tiff?)$/.test(nome);
    if (!ok) {
      setErrore("Serve il PDF dell'UNILAV oppure una sua foto/scansione (JPG, PNG).");
      return;
    }
    setErrore(null);
    setEsito(null);
    setCaricamento(true);
    try {
      const r = await inviaUnilav(file, aziendaScelta);
      setEsito(r);
      onImportato?.({ id: r.azienda.id, ragioneSociale: r.azienda.ragioneSociale });
      api.aziende().then(setAziende).catch(() => { /* ignora */ }); // potrebbe aver creato una nuova azienda
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Non sono riuscito a leggere il PDF.");
    } finally {
      setCaricamento(false);
    }
  }

  return (
    <section className="glass-card page-card" style={{ marginBottom: 16 }}>
      <div className="card-title-row">
        <div>
          <h1>📄 Aggiungi da UNILAV</h1>
          <p>Trascina il PDF della Comunicazione Obbligatoria: registro azienda, lavoratore e rapporto — poi fai subito la busta paga.</p>
        </div>
      </div>

      <label style={{ display: "block", marginBottom: 12 }}>
        <span style={{ display: "block", fontSize: 13, marginBottom: 4, color: "var(--muted, #64748b)" }}>
          Dove salvo i dati
        </span>
        <select
          value={aziendaScelta}
          onChange={(e) => setAziendaScelta(e.target.value)}
          style={{ width: "100%", maxWidth: 460, padding: "9px 11px", borderRadius: 10 }}
        >
          <option value="">🤖 Automatico — usa o crea l'azienda dal PDF</option>
          {aziende.map((a) => <option key={a.id} value={a.id}>🏢 {a.ragioneSociale}</option>)}
        </select>
      </label>

      <div
        role="button"
        tabIndex={0}
        aria-label="Trascina o scegli il PDF UNILAV"
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") inputRef.current?.click(); }}
        onDragOver={(e) => { e.preventDefault(); setTrascina(true); }}
        onDragLeave={() => setTrascina(false)}
        onDrop={(e) => { e.preventDefault(); setTrascina(false); gestisci(e.dataTransfer.files?.[0]); }}
        style={{
          display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
          gap: 8, textAlign: "center", cursor: "pointer", userSelect: "none",
          padding: "36px 20px", borderRadius: 16,
          border: `2px dashed ${trascina ? "#3b82f6" : "rgba(148,163,184,.5)"}`,
          background: trascina ? "rgba(59,130,246,.08)" : "rgba(148,163,184,.06)",
          transition: "all .15s ease",
        }}
      >
        <input ref={inputRef} type="file" accept="application/pdf,.pdf,image/*" hidden onChange={(e) => gestisci(e.target.files?.[0])} />
        {caricamento ? (
          <strong>⏳ Sto leggendo il documento e registrando i dati…</strong>
        ) : (
          <>
            <div style={{ fontSize: 42, lineHeight: 1 }}>⬆️</div>
            <strong style={{ fontSize: 17 }}>Trascina qui il PDF dell'UNILAV (o una sua foto)</strong>
            <span style={{ color: "var(--muted, #64748b)" }}>oppure clicca per scegliere il file</span>
          </>
        )}
      </div>

      {errore && <p className="errore" style={{ marginTop: 12 }}>{errore}</p>}

      {esito && <Risultato esito={esito} />}
    </section>
  );
}

function Risultato({ esito }: { esito: EsitoImportUnilav }) {
  const d = esito.rapporto.dipendente;
  return (
    <div style={{ marginTop: 16 }}>
      <p style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 600, color: "#16a34a" }}>
        <span className="status-pill green">OK</span> Registrato dal PDF UNILAV
      </p>

      <div className="ledger-grid" style={{ marginTop: 8 }}>
        <article className="ledger-card">
          <span>Datore di lavoro</span>
          <strong>{esito.azienda.ragioneSociale}</strong>
          <em className={`status-pill ${esito.azienda.creata ? "blue" : "slate"}`}>
            {esito.azienda.creata ? "Creato ora" : "Già presente"}
            {esito.azienda.partitaIva ? ` · P.IVA ${esito.azienda.partitaIva}` : esito.azienda.codiceFiscale ? ` · CF ${esito.azienda.codiceFiscale}` : ""}
          </em>
        </article>
        <article className="ledger-card">
          <span>Lavoratore</span>
          <strong>{`${d.nome} ${d.cognome}`.trim() || "—"}</strong>
          <em className={`status-pill ${esito.rapporto.lavoratoreGiaPresente ? "slate" : "green"}`}>
            {d.codiceFiscale}{esito.rapporto.lavoratoreGiaPresente ? " · nuovo rapporto" : ""}
          </em>
        </article>
        <article className="ledger-card">
          <span>Rapporto</span>
          <strong>{esito.rapporto.qualifica} · liv. {esito.rapporto.livello || "—"}</strong>
          <em className="status-pill blue">
            Dal {formattaData(esito.rapporto.dataAssunzione)}
            {esito.rapporto.codiceCcnl ? ` · CCNL ${esito.rapporto.codiceCcnl}` : ""}
          </em>
        </article>
      </div>

      {esito.avvertenze.length > 0 && (
        <div style={{ marginTop: 12 }}>
          <strong style={{ fontSize: 13 }}>⚠️ Da completare:</strong>
          <ul style={{ margin: "6px 0 0", paddingLeft: 20, color: "var(--muted, #64748b)", fontSize: 13 }}>
            {esito.avvertenze.map((a, i) => <li key={i}>{a}</li>)}
          </ul>
        </div>
      )}

      <BustaPaga esito={esito} />
    </div>
  );
}

function BustaPaga({ esito }: { esito: EsitoImportUnilav }) {
  const oggi = new Date();
  // Il backend mappa il CCNL UNILAV a un codice interno (MAIUSCOLE_CON_UNDERSCORE): se c'è, è riconosciuto.
  const ccnlMappato = /^[A-Z_]+$/.test(esito.rapporto.codiceCcnl) ? esito.rapporto.codiceCcnl : "";
  const ccnlNoto = ccnlMappato.length > 0;
  const [aperto, setAperto] = useState(false);
  const [mese, setMese] = useState(oggi.getMonth() + 1);
  const [anno, setAnno] = useState(oggi.getFullYear());
  const [straordinario, setStraordinario] = useState(0);
  const [ccnl, setCcnl] = useState(ccnlMappato || "COMMERCIO_TERZIARIO");
  const [livello, setLivello] = useState(/^\d+$/.test(esito.rapporto.livello) ? esito.rapporto.livello : "3");
  const [ccnlList, setCcnlList] = useState<{ codice: string; descrizione: string }[]>([]);

  useEffect(() => { listaCcnl().then(setCcnlList).catch(() => { /* ignora */ }); }, []);
  const [calc, setCalc] = useState<Cedolino | null>(null);
  const [errore, setErrore] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function fai() {
    setBusy(true);
    setErrore(null);
    setCalc(null);
    try {
      const c = await api.calcolaCedolino(
        {
          codiceCcnl: ccnl, livello, qualifica: esito.rapporto.qualifica,
          numeroMensilita: 14, scattiAnzianita: 0, regione: "Lombardia", codiceCatastaleComune: "F205",
        },
        { anno, mese, oreStraordinario: straordinario },
      );
      setCalc(c);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Calcolo non riuscito.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ marginTop: 16, borderTop: "1px solid rgba(148,163,184,.25)", paddingTop: 16 }}>
      {!aperto ? (
        <button className="btn primary" type="button" onClick={() => setAperto(true)}>💶 Fai la busta paga</button>
      ) : (
        <>
          <div className="form-grid">
            <label>
              <span>Mese</span>
              <select value={mese} onChange={(e) => setMese(+e.target.value)}>
                {MESI.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
              </select>
            </label>
            <label>
              <span>Anno</span>
              <input type="number" value={anno} onChange={(e) => setAnno(+e.target.value)} />
            </label>
            <label>
              <span>CCNL</span>
              <select value={ccnl} onChange={(e) => setCcnl(e.target.value)}>
                {(ccnlList.length > 0 ? ccnlList : [{ codice: "COMMERCIO_TERZIARIO", descrizione: "Commercio / Terziario" }])
                  .map((c) => <option key={c.codice} value={c.codice}>{c.descrizione}</option>)}
              </select>
            </label>
            <label>
              <span>Livello</span>
              <input value={livello} onChange={(e) => setLivello(e.target.value)} />
            </label>
            <label>
              <span>Ore straordinario <small>(facoltativo)</small></span>
              <input type="number" min={0} value={straordinario} onChange={(e) => setStraordinario(+e.target.value)} />
            </label>
            <button className="btn primary wide-field" type="button" onClick={fai} disabled={busy}>
              {busy ? "Calcolo…" : "Calcola busta paga"}
            </button>
          </div>

          {ccnlNoto ? (
            <p style={{ fontSize: 12, color: "#16a34a", marginTop: 4 }}>
              ✓ CCNL riconosciuto dall'UNILAV: {ccnlMappato.replace(/_/g, " ")}. Valori tabellari di esempio — verifica con il Consulente del Lavoro.
            </p>
          ) : (
            <p style={{ fontSize: 12, color: "var(--muted, #64748b)", marginTop: 4 }}>
              Il CCNL dell'UNILAV non è tra quelli configurati: scegli il contratto corretto dal menu (uso Commercio come stima).
            </p>
          )}
          {errore && <p className="errore">{errore}</p>}
          {calc && <RisultatoBusta c={calc} mese={mese} anno={anno} nominativo={`${esito.rapporto.dipendente.nome} ${esito.rapporto.dipendente.cognome}`.trim()} />}
        </>
      )}
    </div>
  );
}

function RisultatoBusta({ c, mese, anno, nominativo }: { c: Cedolino; mese: number; anno: number; nominativo: string }) {
  return (
    <div className="ledger-grid" style={{ marginTop: 12 }}>
      <article className="ledger-card">
        <span>Netto in busta · {MESI[mese - 1]} {anno}</span>
        <strong>{euro(c.nettoInBusta)}</strong>
        <em className="status-pill green">{nominativo || "Lavoratore"}</em>
      </article>
      <article className="ledger-card">
        <span>Totale competenze</span>
        <strong>{euro(c.totaleCompetenze)}</strong>
        <em className="status-pill blue">Lordo del mese</em>
      </article>
      <article className="ledger-card">
        <span>Costo azienda</span>
        <strong>{euro(c.costoAzienda)}</strong>
        <em className="status-pill amber">TFR {euro(c.quotaTfr)}</em>
      </article>
    </div>
  );
}

function euro(n: number): string {
  return n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";
}

function formattaData(iso: string): string {
  const [a, m, g] = iso.split("-");
  return a && m && g ? `${g}/${m}/${a}` : iso;
}
