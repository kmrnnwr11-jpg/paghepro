import { useRef, useState } from "react";
import { api, type EsitoImportUnilav } from "../api";

/// Schermata "trascina l'UNILAV e fa tutto": l'utente trascina (o sceglie) il PDF della
/// Comunicazione Obbligatoria; il sistema lo legge e registra azienda + lavoratore + rapporto.
export function CaricaUnilav({ onImportato }: { onImportato?: (azienda: { id: string; ragioneSociale: string }) => void }) {
  const [trascina, setTrascina] = useState(false);
  const [caricamento, setCaricamento] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);
  const [esito, setEsito] = useState<EsitoImportUnilav | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  async function gestisci(file: File | undefined | null) {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".pdf")) {
      setErrore("Serve il PDF dell'UNILAV (un file .pdf).");
      return;
    }
    setErrore(null);
    setEsito(null);
    setCaricamento(true);
    try {
      const r = await api.importUnilavPdf(file);
      setEsito(r);
      onImportato?.({ id: r.azienda.id, ragioneSociale: r.azienda.ragioneSociale });
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
          <p>Trascina qui il PDF della Comunicazione Obbligatoria: registro in automatico azienda, lavoratore e rapporto.</p>
        </div>
      </div>

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
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf,.pdf"
          hidden
          onChange={(e) => gestisci(e.target.files?.[0])}
        />
        {caricamento ? (
          <strong>⏳ Sto leggendo il PDF e registrando i dati…</strong>
        ) : (
          <>
            <div style={{ fontSize: 42, lineHeight: 1 }}>⬆️</div>
            <strong style={{ fontSize: 17 }}>Trascina qui il PDF dell'UNILAV</strong>
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
    </div>
  );
}

function formattaData(iso: string): string {
  const [a, m, g] = iso.split("-");
  return a && m && g ? `${g}/${m}/${a}` : iso;
}
