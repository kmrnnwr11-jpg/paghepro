import { useCallback, useEffect, useState } from "react";
import { Archive, RefreshCw, ShieldAlert, Trash2 } from "lucide-react";
import {
  api, ApiError,
  type AnteprimaRetention, type BloccoConservazione,
  type PolicyRetention, type RicevutaRetention, type SalvaPolicyRetention,
} from "../api";

const MOTIVI_BLOCCO: Array<{ codice: string; etichetta: string }> = [
  { codice: "RichiestaInteressato", etichetta: "Richiesta dell'interessato" },
  { codice: "Contenzioso", etichetta: "Contenzioso in corso" },
  { codice: "VerificaAutorita", etichetta: "Verifica di un'autorità" },
  { codice: "ObbligoLegale", etichetta: "Obbligo legale" },
];

function dataOraLocale(valore: string): string {
  return new Intl.DateTimeFormat("it-IT", { dateStyle: "short", timeStyle: "short" })
    .format(new Date(valore));
}

function messaggioErrore(e: unknown, fallback: string): string {
  if (e instanceof ApiError) {
    if (e.status === 403) return "La conservazione dati può essere gestita solo dall'amministratore dello studio.";
    const corpo = e.corpo as { errore?: string } | undefined;
    if (corpo?.errore) return corpo.errore;
  }
  return fallback;
}

interface FormPolicy {
  giorni: string;
  baseGiuridica: string;
  riferimentoDecisione: string;
  istruzioniBackup: string;
  approvata: boolean;
  attiva: boolean;
}

function formDaPolicy(p: PolicyRetention): FormPolicy {
  return {
    giorni: p.giorniConservazione?.toString() ?? "",
    baseGiuridica: p.baseGiuridica ?? "",
    riferimentoDecisione: p.riferimentoDecisione ?? "",
    istruzioniBackup: p.istruzioniBackup ?? "",
    approvata: p.approvata,
    attiva: p.attiva,
  };
}

export function Retention() {
  const [policy, setPolicy] = useState<PolicyRetention[]>([]);
  const [form, setForm] = useState<Record<string, FormPolicy>>({});
  const [blocchi, setBlocchi] = useState<BloccoConservazione[]>([]);
  const [ricevute, setRicevute] = useState<RicevutaRetention[]>([]);
  const [anteprime, setAnteprime] = useState<Record<string, AnteprimaRetention | null>>({});
  const [chiavi, setChiavi] = useState<Record<string, string>>({});
  const [conferme, setConferme] = useState<Record<string, string>>({});
  const [caricamento, setCaricamento] = useState(true);
  const [lavoro, setLavoro] = useState(false);
  const [errore, setErrore] = useState("");
  const [esito, setEsito] = useState("");
  const [nuovoBlocco, setNuovoBlocco] = useState({ categoria: "", motivo: MOTIVI_BLOCCO[0].codice });

  const carica = useCallback(async () => {
    setCaricamento(true);
    setErrore("");
    try {
      const [elencoPolicy, elencoBlocchi, elencoRicevute] = await Promise.all([
        api.retentionPolicy(), api.blocchiConservazione(), api.esecuzioniRetention(),
      ]);
      setPolicy(elencoPolicy);
      setForm(Object.fromEntries(elencoPolicy.map((p) => [p.categoria, formDaPolicy(p)])));
      setBlocchi(elencoBlocchi);
      setRicevute(elencoRicevute);
      setNuovoBlocco((b) => ({ ...b, categoria: b.categoria || elencoPolicy[0]?.categoria || "" }));
    } catch (e) {
      setErrore(messaggioErrore(e, "Impossibile caricare la conservazione dati. Riprova tra poco."));
    } finally {
      setCaricamento(false);
    }
  }, []);

  useEffect(() => { void carica(); }, [carica]);

  const salvaPolicy = async (p: PolicyRetention) => {
    const f = form[p.categoria];
    if (!f) return;
    const dati: SalvaPolicyRetention = {
      versioneAttesa: p.versione,
      giorniConservazione: f.giorni ? Number(f.giorni) : null,
      attiva: f.attiva,
      approvata: f.approvata,
      baseGiuridica: f.baseGiuridica || null,
      riferimentoDecisione: f.riferimentoDecisione || null,
      istruzioniBackup: f.istruzioniBackup || null,
    };
    setLavoro(true);
    setErrore("");
    setEsito("");
    try {
      const salvata = await api.salvaPolicyRetention(p.categoria, dati);
      setPolicy((elenco) => elenco.map((x) => (x.categoria === p.categoria ? salvata : x)));
      setForm((f2) => ({ ...f2, [p.categoria]: formDaPolicy(salvata) }));
      setAnteprime((a) => ({ ...a, [p.categoria]: null }));
      setEsito(`Policy «${p.titolo}» salvata (versione ${salvata.versione}).`);
    } catch (e) {
      setErrore(messaggioErrore(e, "Salvataggio non riuscito. Ricarica e riprova."));
    } finally {
      setLavoro(false);
    }
  };

  const creaAnteprima = async (p: PolicyRetention) => {
    setLavoro(true);
    setErrore("");
    setEsito("");
    try {
      const anteprima = await api.anteprimaRetention(p.categoria);
      setAnteprime((a) => ({ ...a, [p.categoria]: anteprima }));
      // Una chiave idempotenza per anteprima: un eventuale doppio clic non cancella due volte.
      setChiavi((c) => ({ ...c, [anteprima.id]: crypto.randomUUID() }));
      setConferme((c) => ({ ...c, [anteprima.id]: "" }));
    } catch (e) {
      setErrore(messaggioErrore(e, "Anteprima non riuscita. Riprova tra poco."));
    } finally {
      setLavoro(false);
    }
  };

  const esegui = async (p: PolicyRetention, anteprima: AnteprimaRetention) => {
    setLavoro(true);
    setErrore("");
    setEsito("");
    try {
      const ricevuta = await api.eseguiRetention({
        anteprimaId: anteprima.id,
        chiaveIdempotenza: chiavi[anteprima.id],
        codiceConferma: conferme[anteprima.id] ?? "",
      });
      setAnteprime((a) => ({ ...a, [p.categoria]: null }));
      setRicevute((r) => [ricevuta, ...r]);
      setEsito(ricevuta.esito === "Parziale"
        ? `Cancellati ${ricevuta.elaborati} record; ne restano circa ${ricevuta.restantiStimati}. Crea una nuova anteprima per continuare.`
        : `Cancellati ${ricevuta.elaborati} record. Esecuzione completata.`);
    } catch (e) {
      setErrore(messaggioErrore(e, "Esecuzione non riuscita. Ricalcola l'anteprima e riprova."));
    } finally {
      setLavoro(false);
    }
  };

  const creaBlocco = async () => {
    if (!nuovoBlocco.categoria) return;
    setLavoro(true);
    setErrore("");
    try {
      const blocco = await api.creaBloccoConservazione(nuovoBlocco.categoria, nuovoBlocco.motivo);
      setBlocchi((b) => [blocco, ...b.filter((x) => x.id !== blocco.id)]);
      setEsito("Blocco di conservazione attivo: la cancellazione della categoria è sospesa.");
    } catch (e) {
      setErrore(messaggioErrore(e, "Creazione del blocco non riuscita."));
    } finally {
      setLavoro(false);
    }
  };

  const revocaBlocco = async (id: string) => {
    setLavoro(true);
    setErrore("");
    try {
      const blocco = await api.revocaBloccoConservazione(id);
      setBlocchi((b) => b.map((x) => (x.id === id ? blocco : x)));
    } catch (e) {
      setErrore(messaggioErrore(e, "Revoca del blocco non riuscita."));
    } finally {
      setLavoro(false);
    }
  };

  const aggiornaForm = (categoria: string, campi: Partial<FormPolicy>) =>
    setForm((f) => ({ ...f, [categoria]: { ...f[categoria], ...campi } }));

  const etichettaMotivo = (codice: string) =>
    MOTIVI_BLOCCO.find((m) => m.codice === codice)?.etichetta ?? codice;

  return (
    <section className="glass-card page-card audit-page" aria-busy={caricamento || lavoro}>
      <div className="card-title-row">
        <div>
          <h1>Conservazione dati (GDPR)</h1>
          <p>Quanto a lungo lo studio conserva i dati e come vengono cancellati, con anteprima vincolante e ricevuta.</p>
        </div>
        <button className="btn secondary" type="button" disabled={caricamento || lavoro} onClick={() => void carica()}>
          <RefreshCw size={16} aria-hidden="true" /> Aggiorna
        </button>
      </div>

      <div className="audit-notice">
        <ShieldAlert size={18} aria-hidden="true" />
        <span>
          Le durate non sono predefinite: vanno decise e approvate con il professionista privacy
          (base giuridica e riferimento della decisione sono obbligatori). La cancellazione riguarda
          solo l'archivio operativo, non i log dell'infrastruttura né i backup, che seguono la loro rotazione.
        </span>
      </div>

      <div className="audit-summary" aria-live="polite">
        {caricamento ? "Caricamento…" : errore || esito || `${policy.length} categorie governate, ${blocchi.filter((b) => !b.revocatoUtc).length} blocchi attivi`}
      </div>

      {!caricamento && !errore && policy.map((p) => {
        const f = form[p.categoria];
        const anteprima = anteprime[p.categoria];
        const bloccoAttivo = blocchi.some((b) => b.categoria === p.categoria && !b.revocatoUtc);
        if (!f) return null;
        return (
          <div className="glass-panel retention-card" key={p.categoria} style={{ padding: 16, marginBlockEnd: 16 }}>
            <div className="card-title-row">
              <div>
                <h2 style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <Archive size={18} aria-hidden="true" /> {p.titolo}
                </h2>
                <p>{p.finalita}. Decorrenza: {p.decorrenza.toLowerCase()}. {p.avvertenza}</p>
              </div>
              <span className={`status-pill ${p.attiva ? "green" : "slate"}`}>
                {p.attiva ? `Attiva · v${p.versione}` : p.versione > 0 ? `Non attiva · v${p.versione}` : "Mai configurata"}
              </span>
            </div>

            <div className="audit-filters" role="group" aria-label={`Criteri di conservazione: ${p.titolo}`}>
              <label>
                <span>Giorni di conservazione</span>
                <input type="number" min={1} value={f.giorni} disabled={lavoro}
                  onChange={(e) => aggiornaForm(p.categoria, { giorni: e.target.value })} />
              </label>
              <label>
                <span>Base giuridica</span>
                <input type="text" value={f.baseGiuridica} maxLength={200} disabled={lavoro}
                  placeholder="Es. art. 6.1.f GDPR — legittimo interesse"
                  onChange={(e) => aggiornaForm(p.categoria, { baseGiuridica: e.target.value })} />
              </label>
              <label>
                <span>Riferimento della decisione</span>
                <input type="text" value={f.riferimentoDecisione} maxLength={200} disabled={lavoro}
                  placeholder="Es. verbale o parere del consulente privacy"
                  onChange={(e) => aggiornaForm(p.categoria, { riferimentoDecisione: e.target.value })} />
              </label>
              <label>
                <span>Istruzioni sui backup</span>
                <input type="text" value={f.istruzioniBackup} maxLength={300} disabled={lavoro}
                  placeholder="Es. i backup ruotano entro 30 giorni"
                  onChange={(e) => aggiornaForm(p.categoria, { istruzioniBackup: e.target.value })} />
              </label>
            </div>
            <div style={{ display: "flex", flexWrap: "wrap", gap: 16, alignItems: "center", marginBlock: 10 }}>
              <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
                <input type="checkbox" checked={f.approvata} disabled={lavoro}
                  onChange={(e) => aggiornaForm(p.categoria, { approvata: e.target.checked })} />
                <span>Criteri approvati formalmente</span>
              </label>
              <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
                <input type="checkbox" checked={f.attiva} disabled={lavoro}
                  onChange={(e) => aggiornaForm(p.categoria, { attiva: e.target.checked })} />
                <span>Policy attiva</span>
              </label>
              <button className="btn" type="button" disabled={lavoro} onClick={() => void salvaPolicy(p)}>
                Salva policy
              </button>
              <button className="btn secondary" type="button" disabled={lavoro || !p.attiva}
                title={p.attiva ? undefined : "Serve una policy attiva e approvata"}
                onClick={() => void creaAnteprima(p)}>
                Calcola anteprima
              </button>
            </div>

            {anteprima && (
              <div className="audit-notice" role="group" aria-label={`Anteprima cancellazione: ${p.titolo}`}>
                <Trash2 size={18} aria-hidden="true" />
                <span style={{ display: "grid", gap: 8 }}>
                  <span>
                    <strong>{anteprima.elaborabili}</strong> record cancellabili
                    su {anteprima.candidati} più vecchi del {dataOraLocale(anteprima.sogliaUtc)}
                    {anteprima.bloccati > 0 && <> ({anteprima.bloccati} protetti da un blocco)</>}.
                    L'anteprima vale fino alle {dataOraLocale(anteprima.scadeUtc)}. {anteprima.nota}
                  </span>
                  {anteprima.elaborabili > 0 && (
                    <span style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
                      <span>Per procedere digita <code>{anteprima.codiceConferma}</code>:</span>
                      <input type="text" style={{ maxWidth: 220 }} value={conferme[anteprima.id] ?? ""}
                        aria-label="Codice di conferma" disabled={lavoro}
                        onChange={(e) => setConferme((c) => ({ ...c, [anteprima.id]: e.target.value }))} />
                      <button className="btn" type="button"
                        disabled={lavoro || (conferme[anteprima.id] ?? "").trim() === ""}
                        onClick={() => void esegui(p, anteprima)}>
                        Esegui cancellazione definitiva
                      </button>
                    </span>
                  )}
                </span>
              </div>
            )}
            {bloccoAttivo && !anteprima && (
              <p className="muto">Un blocco di conservazione è attivo su questa categoria: la cancellazione è sospesa.</p>
            )}
          </div>
        );
      })}

      {!caricamento && !errore && (
        <div className="glass-panel" style={{ padding: 16, marginBlockEnd: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>Blocchi di conservazione</h2>
              <p>Sospendono la cancellazione di una categoria (contenzioso, richiesta dell'interessato, verifica, obbligo legale).</p>
            </div>
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "end", marginBlockEnd: 10 }}>
            <label>
              <span>Categoria</span>
              <select value={nuovoBlocco.categoria} disabled={lavoro}
                onChange={(e) => setNuovoBlocco((b) => ({ ...b, categoria: e.target.value }))}>
                {policy.map((p) => <option key={p.categoria} value={p.categoria}>{p.titolo}</option>)}
              </select>
            </label>
            <label>
              <span>Motivo</span>
              <select value={nuovoBlocco.motivo} disabled={lavoro}
                onChange={(e) => setNuovoBlocco((b) => ({ ...b, motivo: e.target.value }))}>
                {MOTIVI_BLOCCO.map((m) => <option key={m.codice} value={m.codice}>{m.etichetta}</option>)}
              </select>
            </label>
            <button className="btn secondary" type="button" disabled={lavoro} onClick={() => void creaBlocco()}>
              Attiva blocco
            </button>
          </div>
          {blocchi.length === 0 ? (
            <p className="muto">Nessun blocco registrato.</p>
          ) : (
            <div className="table-wrap" role="region" aria-label="Blocchi di conservazione" tabIndex={0}>
              <table className="data-table spacious">
                <thead>
                  <tr><th>Categoria</th><th>Motivo</th><th>Creato</th><th>Stato</th><th></th></tr>
                </thead>
                <tbody>
                  {blocchi.map((b) => (
                    <tr key={b.id}>
                      <td><strong>{policy.find((p) => p.categoria === b.categoria)?.titolo ?? b.categoria}</strong></td>
                      <td>{etichettaMotivo(b.motivoCodice)}</td>
                      <td><time dateTime={b.creatoUtc}>{dataOraLocale(b.creatoUtc)}</time></td>
                      <td>
                        <span className={`status-pill ${b.revocatoUtc ? "slate" : "amber"}`}>
                          {b.revocatoUtc ? `Revocato ${dataOraLocale(b.revocatoUtc)}` : "Attivo"}
                        </span>
                      </td>
                      <td>
                        {!b.revocatoUtc && (
                          <button className="btn secondary" type="button" disabled={lavoro}
                            onClick={() => void revocaBlocco(b.id)}>
                            Revoca
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {!caricamento && !errore && (
        <div className="glass-panel" style={{ padding: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>Ricevute di cancellazione</h2>
              <p>Registro immutabile delle esecuzioni: ogni ricevuta è ancorata alla precedente con un'impronta.</p>
            </div>
          </div>
          {ricevute.length === 0 ? (
            <p className="muto">Nessuna cancellazione eseguita finora.</p>
          ) : (
            <div className="table-wrap" role="region" aria-label="Ricevute di cancellazione" tabIndex={0}>
              <table className="data-table spacious">
                <thead>
                  <tr><th>Completata</th><th>Categoria</th><th>Record</th><th>Esito</th><th>Impronta</th></tr>
                </thead>
                <tbody>
                  {ricevute.map((r) => (
                    <tr key={r.id}>
                      <td><time dateTime={r.completataUtc}>{dataOraLocale(r.completataUtc)}</time></td>
                      <td><strong>{policy.find((p) => p.categoria === r.categoria)?.titolo ?? r.categoria}</strong></td>
                      <td>{r.elaborati} su {r.candidatiAnteprima}{r.restantiStimati > 0 ? ` (≈${r.restantiStimati} restanti)` : ""}</td>
                      <td>
                        <span className={`status-pill ${r.esito === "Completata" ? "green" : "amber"}`}>{r.esito}</span>
                      </td>
                      <td><code className="audit-route">{r.digest.slice(0, 16)}…</code></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </section>
  );
}
