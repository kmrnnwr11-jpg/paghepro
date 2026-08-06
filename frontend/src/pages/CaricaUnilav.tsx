import { useEffect, useRef, useState } from "react";
import { AlertTriangle, Banknote, BriefcaseBusiness, Building2, Check, ClipboardPaste, FileText, LoaderCircle, UploadCloud, UserRound, X } from "lucide-react";
import { api, fetchAutenticata, type CampoDaCompletare, type Cedolino, type EsitoImportUnilav } from "../api";
import { salvaAziendaCorrenteId } from "../aziendaCorrente";
const MESI = ["Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
  "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

// Upload diretto (multipart) con azienda scelta opzionale.
async function inviaUnilav(file: File, aziendaId: string): Promise<EsitoImportUnilav> {
  const form = new FormData();
  form.append("file", file);
  if (aziendaId) form.append("aziendaId", aziendaId);
  const res = await fetchAutenticata("/api/unilav/pdf", { method: "POST", body: form });
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
  const res = await fetchAutenticata("/api/ccnl");
  if (!res.ok) return [];
  return (await res.json()) as { codice: string; descrizione: string }[];
}

/// "Trascina l'UNILAV e fa tutto": scegli l'azienda (o automatico dal PDF), il sistema registra
/// azienda + lavoratore + rapporto, poi con un clic fai la busta paga del mese.
export function CaricaUnilav({ onImportato }: { onImportato?: (azienda: { id: string; ragioneSociale: string }) => void }) {
  const [aziende, setAziende] = useState<{ id: string; ragioneSociale: string }[]>([]);
  const [aziendaScelta, setAziendaScelta] = useState(""); // "" = automatico dal PDF
  const [modo, setModo] = useState<"file" | "testo">("file");
  const [testo, setTesto] = useState("");
  const [trascina, setTrascina] = useState(false);
  const [caricamento, setCaricamento] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);
  const [esito, setEsito] = useState<EsitoImportUnilav | null>(null);
  const [checklist, setChecklist] = useState<CampoDaCompletare[] | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => { api.aziende().then(setAziende).catch(() => { /* ignora */ }); }, []);

  // Dopo un import riuscito: mostra l'esito e, se qualcosa manca, apre subito la guida.
  function registraEsito(r: EsitoImportUnilav) {
    setEsito(r);
    if (r.daCompletare.length > 0) setChecklist(r.daCompletare);
    onImportato?.({ id: r.azienda.id, ragioneSociale: r.azienda.ragioneSociale });
    api.aziende().then(setAziende).catch(() => { /* ignora */ }); // potrebbe aver creato una nuova azienda
  }

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
      registraEsito(await inviaUnilav(file, aziendaScelta));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Non sono riuscito a leggere il PDF.");
    } finally {
      setCaricamento(false);
    }
  }

  async function leggiTesto() {
    if (testo.trim().length < 20) {
      setErrore("Incolla il testo della comunicazione UNILAV (datore, lavoratore e rapporto).");
      return;
    }
    setErrore(null);
    setEsito(null);
    setCaricamento(true);
    try {
      registraEsito(await api.importUnilavTesto(testo, { aziendaId: aziendaScelta || undefined }));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Non sono riuscito a leggere il testo.");
    } finally {
      setCaricamento(false);
    }
  }

  return (
    <section className="glass-card page-card unilav-workspace">
      <div className="card-title-row unilav-heading">
        <div>
          <h2><FileText size={18} /> Importa comunicazione UNILAV</h2>
          <p>Il documento compila azienda, lavoratore e rapporto di lavoro.</p>
        </div>
      </div>

      <label className="unilav-company-select">
        <span>Dove salvo i dati</span>
        <select
          value={aziendaScelta}
          onChange={(e) => setAziendaScelta(e.target.value)}
        >
          <option value="">Automatico: usa o crea l'azienda indicata nel documento</option>
          {aziende.map((a) => <option key={a.id} value={a.id}>{a.ragioneSociale}</option>)}
        </select>
      </label>

      <div className="unilav-modi" role="tablist">
        <button
          type="button" role="tab" aria-selected={modo === "file"}
          className={modo === "file" ? "attivo" : ""}
          onClick={() => { setModo("file"); setErrore(null); }}
        >
          <UploadCloud size={15} aria-hidden="true" /> Carica il documento
        </button>
        <button
          type="button" role="tab" aria-selected={modo === "testo"}
          className={modo === "testo" ? "attivo" : ""}
          onClick={() => { setModo("testo"); setErrore(null); }}
        >
          <ClipboardPaste size={15} aria-hidden="true" /> Incolla il testo
        </button>
      </div>

      {modo === "file" ? (
        <div
          role="button"
          tabIndex={0}
          aria-label="Trascina o scegli il PDF UNILAV"
          onClick={() => inputRef.current?.click()}
          onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") inputRef.current?.click(); }}
          onDragOver={(e) => { e.preventDefault(); setTrascina(true); }}
          onDragLeave={() => setTrascina(false)}
          onDrop={(e) => { e.preventDefault(); setTrascina(false); gestisci(e.dataTransfer.files?.[0]); }}
          className={`unilav-dropzone ${trascina ? "dragging" : ""}`}
        >
          <input ref={inputRef} type="file" accept="application/pdf,.pdf,image/*" hidden onChange={(e) => gestisci(e.target.files?.[0])} />
          {caricamento ? (
            <><LoaderCircle className="spin" size={30} aria-hidden="true" /><strong>Lettura e registrazione in corso…</strong></>
          ) : (
            <>
              <UploadCloud size={34} aria-hidden="true" />
              <strong>Trascina qui il PDF UNILAV o una scansione</strong>
              <span>PDF, JPG o PNG. Puoi anche fare clic per scegliere il file.</span>
            </>
          )}
        </div>
      ) : (
        <div className="unilav-incolla">
          <label>
            <span>Testo della comunicazione</span>
            <textarea
              rows={10}
              value={testo}
              onChange={(e) => setTesto(e.target.value)}
              placeholder={"Incolla qui la comunicazione UNILAV.\n\nEsempio:\nDatore di lavoro\nCodice fiscale: ...\nRagione sociale: ...\nLavoratore\nCodice fiscale: ...\nRapporto di lavoro\nData inizio: ..."}
            />
          </label>
          <p className="muto unilav-incolla-nota">
            Va bene sia il testo copiato dalla comunicazione sia un riepilogo con elenchi puntati:
            il sistema riconosce le sezioni datore, lavoratore e rapporto. Se nome e cognome sono su
            un campo unico, l'ordine ti viene chiesto di confermarlo.
          </p>
          <button className="btn primary" type="button" onClick={leggiTesto} disabled={caricamento}>
            {caricamento ? "Lettura e registrazione in corso…" : "Leggi e registra"}
          </button>
        </div>
      )}

      {errore && <p className="errore" style={{ marginTop: 12 }}>{errore}</p>}

      {esito && <Risultato esito={esito} onApriChecklist={() => setChecklist(esito.daCompletare)} />}

      {checklist && checklist.length > 0 && (
        <GuidaDatiMancanti
          campi={checklist}
          aziendaId={esito?.azienda.id ?? ""}
          onChiudi={() => setChecklist(null)}
        />
      )}
    </section>
  );
}

/// Popup che elenca i dati mancanti dopo l'import: cosa manca, a cosa serve, dove trovarlo,
/// e un pulsante che porta nella sezione dove si completa (con l'azienda giusta già scelta).
function GuidaDatiMancanti({
  campi, aziendaId, onChiudi,
}: { campi: CampoDaCompletare[]; aziendaId: string; onChiudi: () => void }) {
  function completa(dove: string) {
    if (aziendaId) salvaAziendaCorrenteId(aziendaId);
    window.location.hash = dove === "azienda" ? "aziende" : "dipendenti";
    onChiudi();
  }

  return (
    <div className="unilav-guida-overlay" role="dialog" aria-modal="true" aria-label="Dati ancora da completare">
      <div className="unilav-guida">
        <div className="unilav-guida-testa">
          <div>
            <strong><AlertTriangle size={16} aria-hidden="true" /> Manca ancora qualcosa</strong>
            <small className="muto">
              L'UNILAV non contiene tutto quello che serve alla busta paga: ecco cosa completare.
            </small>
          </div>
          <button className="icon-button" type="button" aria-label="Chiudi" onClick={onChiudi}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>

        <ul className="unilav-guida-lista">
          {campi.map((c, i) => (
            <li key={i}>
              <div>
                <strong>{c.campo}</strong>
                <span className="unilav-guida-perche">{c.perche}</span>
                <span className="unilav-guida-dove">Dove trovarlo: {c.doveTrovarlo}</span>
              </div>
              <button className="btn secondary" type="button" onClick={() => completa(c.dove)}>
                Completa
              </button>
            </li>
          ))}
        </ul>

        <button className="btn primary unilav-guida-chiudi" type="button" onClick={onChiudi}>
          Ho capito, lo faccio dopo
        </button>
      </div>
    </div>
  );
}

function Risultato({ esito, onApriChecklist }: { esito: EsitoImportUnilav; onApriChecklist: () => void }) {
  const d = esito.rapporto.dipendente;
  return (
    <div style={{ marginTop: 16 }}>
      <p className="unilav-success">
        <span className="status-pill green"><Check size={12} /> Completato</span> Registrazione dal documento riuscita
      </p>

      <div className="ledger-grid" style={{ marginTop: 8 }}>
        <article className="ledger-card">
          <span><Building2 size={13} /> Datore di lavoro</span>
          <strong>{esito.azienda.ragioneSociale}</strong>
          <em className={`status-pill ${esito.azienda.creata ? "blue" : "slate"}`}>
            {esito.azienda.creata ? "Creato ora" : "Già presente"}
            {esito.azienda.partitaIva ? ` · P.IVA ${esito.azienda.partitaIva}` : esito.azienda.codiceFiscale ? ` · CF ${esito.azienda.codiceFiscale}` : ""}
          </em>
        </article>
        <article className="ledger-card">
          <span><UserRound size={13} /> Lavoratore</span>
          <strong>{`${d.nome} ${d.cognome}`.trim() || "—"}</strong>
          <em className={`status-pill ${esito.rapporto.lavoratoreGiaPresente ? "slate" : "green"}`}>
            {d.codiceFiscale}{esito.rapporto.lavoratoreGiaPresente ? " · nuovo rapporto" : ""}
          </em>
        </article>
        <article className="ledger-card">
          <span><BriefcaseBusiness size={13} /> Rapporto</span>
          <strong>{esito.rapporto.qualifica} · liv. {esito.rapporto.livello || "—"}</strong>
          <em className="status-pill blue">
            Dal {formattaData(esito.rapporto.dataAssunzione)}
            {esito.rapporto.codiceCcnl ? ` · CCNL ${esito.rapporto.codiceCcnl}` : ""}
          </em>
        </article>
      </div>

      {esito.daCompletare.length > 0 && (
        <button className="btn secondary" type="button" style={{ marginTop: 12 }} onClick={onApriChecklist}>
          <AlertTriangle size={15} /> Mancano {esito.daCompletare.length} dati: vedi cosa fare
        </button>
      )}

      {esito.avvertenze.length > 0 && (
        <div style={{ marginTop: 12 }}>
          <strong className="unilav-warning-title"><AlertTriangle size={15} /> Cosa ha fatto il sistema</strong>
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
        <button className="btn primary" type="button" onClick={() => setAperto(true)}><Banknote size={16} /> Prepara busta paga</button>
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
