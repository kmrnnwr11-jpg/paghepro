import { useEffect, useMemo, useState } from "react";
import { RefreshCw, ShieldCheck } from "lucide-react";
import { api, ApiError, type PaginaRegistroAudit, type VoceRegistroAudit } from "../api";

const DIMENSIONE = 50;

function inizioGiornoUtc(data: string): string | undefined {
  return data ? new Date(`${data}T00:00:00`).toISOString() : undefined;
}

function fineGiornoUtc(data: string): string | undefined {
  return data ? new Date(`${data}T23:59:59.999`).toISOString() : undefined;
}

function dataOraLocale(valore: string): string {
  return new Intl.DateTimeFormat("it-IT", {
    dateStyle: "short",
    timeStyle: "medium",
  }).format(new Date(valore));
}

function dettaglio(voce: VoceRegistroAudit): string {
  if (voce.campiModificati) return `Campi: ${voce.campiModificati.split(",").join(", ")}`;
  if (voce.percorsoTemplate) return voce.percorsoTemplate;
  return "—";
}

function tonoEsito(esito?: string | null): string {
  if (esito === "Riuscito") return "green";
  if (esito === "Negato" || esito === "Errore") return "red";
  if (esito === "Limitato" || esito === "Rifiutato" || esito === "NonTrovato") return "amber";
  return "slate";
}

export function RegistroAttivita() {
  const [pagina, setPagina] = useState(1);
  const [categoria, setCategoria] = useState("");
  const [azione, setAzione] = useState("");
  const [da, setDa] = useState("");
  const [a, setA] = useState("");
  const [dati, setDati] = useState<PaginaRegistroAudit | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [errore, setErrore] = useState("");
  const [aggiornamento, setAggiornamento] = useState(0);

  useEffect(() => {
    let vivo = true;
    setCaricamento(true);
    setErrore("");
    setDati(null);
    api.registroAttivita({
      pagina,
      dimensione: DIMENSIONE,
      categoria: categoria || undefined,
      azione: azione || undefined,
      daUtc: inizioGiornoUtc(da),
      aUtc: fineGiornoUtc(a),
    }).then((risultato) => {
      if (vivo) setDati(risultato);
    }).catch((e: unknown) => {
      if (vivo) setErrore(e instanceof ApiError && e.status === 403
        ? "Il registro attività può essere consultato solo da un amministratore."
        : "Impossibile caricare il registro. Riprova tra poco.");
    }).finally(() => {
      if (vivo) setCaricamento(false);
    });
    return () => { vivo = false; };
  }, [pagina, categoria, azione, da, a, aggiornamento]);

  const pagine = useMemo(
    () => Math.max(1, Math.ceil((dati?.totale ?? 0) / DIMENSIONE)),
    [dati?.totale],
  );
  const cambiaFiltro = (azioneCambio: () => void) => {
    setPagina(1);
    azioneCambio();
  };

  return (
    <section className="glass-card page-card audit-page" aria-busy={caricamento}>
      <div className="card-title-row">
        <div>
          <h1>Registro attività</h1>
          <p>Chi ha consultato o modificato i dati dello studio, quando e con quale esito.</p>
        </div>
        <button className="btn secondary" type="button" disabled={caricamento} onClick={() => setAggiornamento((v) => v + 1)}>
          <RefreshCw size={16} aria-hidden="true" /> Aggiorna
        </button>
      </div>

      <div className="audit-notice">
        <ShieldCheck size={18} aria-hidden="true" />
        <span>Registro in sola lettura. Conserva i metadati necessari — utente, risorsa ed esito — ma non registra contenuti, password o valori dei campi.</span>
      </div>

      <div className="audit-filters" role="group" aria-label="Filtri del registro">
        <label>
          <span>Tipo attività</span>
          <select value={categoria} onChange={(e) => cambiaFiltro(() => setCategoria(e.target.value))}>
            <option value="">Tutte</option>
            <option value="Accesso">Consultazioni e operazioni</option>
            <option value="ModificaDati">Modifiche ai dati</option>
          </select>
        </label>
        <label>
          <span>Azione</span>
          <select value={azione} onChange={(e) => cambiaFiltro(() => setAzione(e.target.value))}>
            <option value="">Tutte</option>
            <option value="Lettura">Lettura</option>
            <option value="Operazione">Operazione</option>
            <option value="Autenticazione">Autenticazione</option>
            <option value="Inserimento">Inserimento</option>
            <option value="Modifica">Modifica</option>
            <option value="Cancellazione">Cancellazione</option>
          </select>
        </label>
        <label>
          <span>Dal giorno</span>
          <input type="date" value={da} max={a || undefined} onChange={(e) => cambiaFiltro(() => setDa(e.target.value))} />
        </label>
        <label>
          <span>Al giorno</span>
          <input type="date" value={a} min={da || undefined} onChange={(e) => cambiaFiltro(() => setA(e.target.value))} />
        </label>
      </div>

      <div className="audit-summary" aria-live="polite">
        {caricamento ? "Caricamento…" : errore || `${dati?.totale ?? 0} attività registrate`}
      </div>

      {!errore && (
        <div className="table-wrap" role="region" aria-label="Attività registrate" tabIndex={0}>
          <table className="data-table spacious audit-table">
            <thead>
              <tr>
                <th>Data e ora</th>
                <th>Utente</th>
                <th>Azione</th>
                <th>Risorsa</th>
                <th>Dettaglio sicuro</th>
                <th>Esito</th>
              </tr>
            </thead>
            <tbody>
              {!caricamento && (dati?.voci.length ?? 0) === 0 ? (
                <tr><td colSpan={6} className="muto">Nessuna attività per i filtri scelti.</td></tr>
              ) : dati?.voci.map((voce) => (
                <tr key={voce.id}>
                  <td><time dateTime={voce.quandoUtc}>{dataOraLocale(voce.quandoUtc)}</time></td>
                  <td><strong>{voce.utente}</strong></td>
                  <td><span className="status-pill blue">{voce.azione}</span></td>
                  <td>
                    <strong>{voce.entita}</strong>
                    {voce.chiaveEntita && <small className="audit-record">Record {voce.chiaveEntita.slice(-12)}</small>}
                  </td>
                  <td><code className="audit-route">{dettaglio(voce)}</code></td>
                  <td>
                    <span className={`status-pill ${tonoEsito(voce.esito)}`}>
                      {voce.esito ?? "Registrato"}{voce.statusCode ? ` · ${voce.statusCode}` : ""}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!errore && dati && (
        <div className="audit-pagination">
          <button className="btn secondary" type="button" disabled={pagina <= 1 || caricamento} onClick={() => setPagina((p) => p - 1)}>
            Precedente
          </button>
          <span>Pagina {pagina} di {pagine}</span>
          <button className="btn secondary" type="button" disabled={pagina >= pagine || caricamento} onClick={() => setPagina((p) => p + 1)}>
            Successiva
          </button>
        </div>
      )}
    </section>
  );
}
