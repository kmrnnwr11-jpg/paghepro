import { useEffect, useRef, useState } from "react";
import { ArrowLeft, Calculator, CheckCircle2, LockKeyhole } from "lucide-react";
import { api, getRuolo, scaricaBlob, type Azienda, type CedolinoArchiviato, type DipendenteRapporto, type StatoWorkflowCedolino } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";
import { creaSequenziatoreRichieste } from "../ultimaRichiesta";
import {
  ANNO_REDDITI_CU_UFFICIALE,
  alimentaAdempimenti,
  anniContabilizzabili,
  coloreStatoCedolino,
  etichettaDownloadCedolino,
  etichettaStatoCedolino,
  statoEffettivo,
  versioneWorkflow,
} from "../cedolinoStati";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];
const VALIDAZIONE_VUOTA = { nominativo: "", qualifica: "Consulente del lavoro", riferimento: "" };

export function Cedolini({ onTornaPresenze }: { onTornaPresenze: () => void }) {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [lista, setLista] = useState<CedolinoArchiviato[]>([]);
  const [caricamentoLista, setCaricamentoLista] = useState(false);
  const [caricamento, setCaricamento] = useState(true);
  const [scaricandoId, setScaricandoId] = useState<string | null>(null);
  const [errore, setErrore] = useState<string | null>(null);
  const [annoCu, setAnnoCu] = useState<number | null>(null);
  const [scaricandoCu, setScaricandoCu] = useState(false);
  const [azioneInCorso, setAzioneInCorso] = useState<string | null>(null);
  const [validaId, setValidaId] = useState<string | null>(null);
  const [validazione, setValidazione] = useState(VALIDAZIONE_VUOTA);
  const richiesteCedolini = useRef(creaSequenziatoreRichieste());
  const ruolo = getRuolo();
  const puoValidare = ruolo === "admin" || ruolo === "superadmin";
  const puoControllare = puoValidare || ruolo === "operatore";
  const validazioneCompleta = validazione.nominativo.trim().length > 0
    && validazione.qualifica.trim().length > 0
    && validazione.riferimento.trim().length > 0;

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
    const richiesta = richiesteCedolini.current.apri();
    setLista([]);
    setErrore(null);
    setValidaId(null);
    setValidazione(VALIDAZIONE_VUOTA);
    if (!rapportoId) { setCaricamentoLista(false); return richiesta.invalida; }
    setCaricamentoLista(true);
    api.cedoliniArchiviati(rapportoId)
      .then((righe) => { if (richiesta.attiva()) setLista(righe); })
      .catch((e) => { if (richiesta.attiva()) setErrore(e instanceof Error ? e.message : "Errore"); })
      .finally(() => { if (richiesta.attiva()) setCaricamentoLista(false); });
    return richiesta.invalida;
  }, [rapportoId]);

  async function scaricaPdf(id: string) {
    setErrore(null);
    setScaricandoId(id);
    try {
      const { blob, filename } = await api.cedolinoPdf(id);
      await scaricaBlob(blob, filename);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Impossibile scaricare il PDF.");
    } finally {
      setScaricandoId(null);
    }
  }

  function applicaStato(esito: StatoWorkflowCedolino) {
    setLista((corrente) => corrente.map((c) => c.id === esito.id
      ? { ...c, stato: esito.stato, versione: esito.versione, codiceCnel: esito.codiceCnel }
      : c));
  }

  async function controlla(c: CedolinoArchiviato) {
    if (!window.confirm("Dichiari di aver verificato presenze, assenze, contratto e importi? Il programma congelerà questa versione, ma la validazione professionale resta un passaggio separato.")) return;
    setErrore(null); setAzioneInCorso(c.id);
    try { applicaStato(await api.controllaCedolino(c.id, c.versione)); }
    catch (e) { setErrore(e instanceof Error ? e.message : "Impossibile completare il controllo."); }
    finally { setAzioneInCorso(null); }
  }

  async function registraValidazione(c: CedolinoArchiviato) {
    setErrore(null); setAzioneInCorso(c.id);
    try {
      applicaStato(await api.validaCedolino(c.id, {
        versioneAttesa: c.versione,
        nominativo: validazione.nominativo.trim(),
        qualifica: validazione.qualifica.trim(),
        riferimento: validazione.riferimento.trim(),
      }));
      setValidaId(null);
      setValidazione(VALIDAZIONE_VUOTA);
    } catch (e) { setErrore(e instanceof Error ? e.message : "Impossibile registrare la validazione."); }
    finally { setAzioneInCorso(null); }
  }

  async function chiudi(c: CedolinoArchiviato) {
    if (!window.confirm("Dopo la chiusura dati e PDF non potranno più cambiare. Vuoi chiudere questo cedolino?")) return;
    setErrore(null); setAzioneInCorso(c.id);
    try { applicaStato(await api.chiudiCedolino(c.id, c.versione)); }
    catch (e) { setErrore(e instanceof Error ? e.message : "Impossibile chiudere il cedolino."); }
    finally { setAzioneInCorso(null); }
  }

  // Gli archivi pre-workflow restano contabilizzabili per continuità, ma la UI non li presenta
  // mai come chiusi o validati dal flusso introdotto successivamente.
  const anniContabili = anniContabilizzabili(lista);
  const anniCuPdf = anniContabili.filter((anno) => anno === ANNO_REDDITI_CU_UFFICIALE);
  const annoCuScelto = annoCu && anniCuPdf.includes(annoCu) ? annoCu : anniCuPdf[0];

  async function scaricaCu() {
    if (!rapportoId || !annoCuScelto) return;
    setErrore(null);
    setScaricandoCu(true);
    try {
      const { blob, filename } = await api.certificazioneUnicaPdf(rapportoId, annoCuScelto);
      await scaricaBlob(blob, filename);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Impossibile scaricare la CU.");
    } finally {
      setScaricandoCu(false);
    }
  }

  return (
    <div className="module-layout wide">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div style={{ display: "flex", alignItems: "flex-start", gap: 12 }}>
            <button
              className="icon-button"
              type="button"
              aria-label="Torna a Presenze"
              title="Torna al passaggio precedente: Presenze"
              onClick={onTornaPresenze}
            >
              <ArrowLeft size={19} aria-hidden="true" />
            </button>
            <div>
              <h1>Cedolini</h1>
              <p>Controllo e archivio cedolini{azienda ? ` · ${azienda.ragioneSociale}` : ""}</p>
            </div>
          </div>
          <div className="actions-row">
            <button className="btn primary" type="button" onClick={onTornaPresenze}>
              <Calculator size={16} aria-hidden="true" />
              Genera busta paga
            </button>
            <span className="status-pill blue">Workflow in 4 passaggi</span>
          </div>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : dipendenti.length === 0 ? (
          <div className="empty-state">Nessun dipendente. Aggiungine uno in “Dipendenti”, poi torna qui a generare i cedolini.</div>
        ) : (
          <>
            <div className="form-grid">
              <label>
                <span>Dipendente</span>
                <select value={rapportoId} disabled={caricamentoLista || azioneInCorso !== null || scaricandoId !== null}
                  onChange={(e) => setRapportoId(e.target.value)}>
                  {dipendenti.map((d) => (
                    <option key={d.rapportoId} value={d.rapportoId}>{d.nominativo || "—"}</option>
                  ))}
                </select>
              </label>
              <div className="empty-state wide-field">
                Presenze e assenze si compilano nella pagina Presenze. Qui controlli ogni busta,
                registri la validazione ricevuta e la chiudi senza poterla più cambiare.
              </div>
            </div>

            {caricamentoLista ? (
              <div className="empty-state">Caricamento cedolini del dipendente…</div>
            ) : lista.length === 0 ? (
              <div className="empty-state">
                Nessun cedolino archiviato per questo dipendente. Premi “Genera busta paga”,
                controlla presenze e assenze del mese, quindi conferma il calcolo.
              </div>
            ) : (
              <div className="table-wrap" aria-busy={azioneInCorso !== null || scaricandoId !== null}>
                <table className="data-table spacious">
                  <thead>
                    <tr>
                      <th>Periodo</th>
                      <th className="right">Imponibile IRPEF</th>
                      <th className="right">Ritenute IRPEF</th>
                      <th className="right">Netto in busta</th>
                      <th className="right">Costo azienda</th>
                      <th>Stato</th>
                      <th className="right">Azioni</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...lista].sort((a, b) => b.anno - a.anno || b.mese - a.mese).map((c) => {
                      const stato = statoEffettivo(c);
                      const versione = versioneWorkflow(c);
                      const occupato = azioneInCorso !== null || scaricandoId !== null;
                      return (
                      <tr key={c.id}>
                        <td><strong>{mesi[c.mese]} {c.anno}</strong></td>
                        <td className="right num">{c.imponibileIrpef != null ? `${euro(c.imponibileIrpef)} €` : "—"}</td>
                        <td className="right num">{c.irpefNetta != null ? `${euro(c.irpefNetta)} €` : "—"}</td>
                        <td className="right num"><strong>{euro(c.nettoInBusta)} €</strong></td>
                        <td className="right num">{euro(c.costoAzienda)} €</td>
                        <td>
                          <span className={`status-pill ${coloreStatoCedolino[stato]}`} aria-label={`Stato: ${etichettaStatoCedolino[stato]}`}>
                            {stato === "Chiusa" && <LockKeyhole size={12} aria-hidden="true" />}
                            {stato === "Validata" && <CheckCircle2 size={12} aria-hidden="true" />}
                            {etichettaStatoCedolino[stato]}
                          </span>
                          <div style={{ marginTop: 5, fontSize: 11, opacity: 0.68 }}>
                            {stato === "PreWorkflow"
                              ? "Nessuna validazione retroattiva · dati da verificare"
                              : versione === null
                                ? "Versione workflow non disponibile · azioni bloccate"
                                : <>{c.codiceCnel ? `CNEL ${c.codiceCnel}` : "Codice CNEL mancante"} · v{versione}</>}
                          </div>
                        </td>
                        <td className="right">
                          <div style={{ display: "flex", justifyContent: "flex-end", gap: 7, flexWrap: "wrap" }}>
                            {stato === "Bozza" && puoControllare && versione !== null && (
                              <button className="btn primary" type="button" disabled={occupato} onClick={() => void controlla(c)}>
                                {azioneInCorso === c.id ? "Attendere…" : "Dichiara controllo e congela"}
                              </button>
                            )}
                            {stato === "Controllata" && puoValidare && versione !== null && (
                              <button className="btn primary" type="button" disabled={occupato} onClick={() => {
                                setValidaId(c.id);
                                setValidazione(VALIDAZIONE_VUOTA);
                              }}>
                                Registra validazione
                              </button>
                            )}
                            {stato === "Validata" && puoValidare && versione !== null && (
                              <button className="btn primary" type="button" disabled={occupato} onClick={() => void chiudi(c)}>
                                {azioneInCorso === c.id ? "Chiusura…" : "Chiudi cedolino"}
                              </button>
                            )}
                            {stato !== "Sconosciuto" && (
                              <button className="btn secondary" type="button" disabled={occupato} onClick={() => void scaricaPdf(c.id)}>
                                {scaricandoId === c.id ? "Download…" : etichettaDownloadCedolino(stato)}
                              </button>
                            )}
                          </div>
                          {stato === "Controllata" && ruolo === "operatore" && (
                            <p style={{ margin: "7px 0 0", fontSize: 11, opacity: 0.72 }}>
                              In attesa della validazione di un amministratore.
                            </p>
                          )}
                          {validaId === c.id && stato === "Controllata" && (
                            <div style={{ marginTop: 10, minWidth: 310, textAlign: "left", padding: 10,
                              background: "var(--panel-strong)", border: "1px solid var(--line)", borderRadius: 9 }}>
                              <strong style={{ fontSize: 12 }}>Evidenza della validazione ricevuta</strong>
                              <input aria-label="Nominativo validatore" placeholder="Nome e cognome"
                                maxLength={160}
                                value={validazione.nominativo} onChange={(e) => setValidazione({ ...validazione, nominativo: e.target.value })} />
                              <input aria-label="Qualifica validatore" placeholder="Qualifica"
                                maxLength={160}
                                value={validazione.qualifica} onChange={(e) => setValidazione({ ...validazione, qualifica: e.target.value })} />
                              <input aria-label="Riferimento validazione" placeholder="Es. verbale o pratica"
                                maxLength={500}
                                value={validazione.riferimento} onChange={(e) => setValidazione({ ...validazione, riferimento: e.target.value })} />
                              <p style={{ fontSize: 11, opacity: 0.7, margin: "6px 0" }}>
                                L’account admin registra l’evidenza: non viene presentato automaticamente come professionista abilitato.
                              </p>
                              <div style={{ display: "flex", gap: 7 }}>
                                <button className="btn primary" type="button" disabled={occupato || !validazioneCompleta}
                                  onClick={() => void registraValidazione(c)}>Conferma</button>
                                <button className="btn secondary" type="button" onClick={() => setValidaId(null)}>Annulla</button>
                              </div>
                            </div>
                          )}
                        </td>
                      </tr>
                    );})}
                  </tbody>
                </table>
              </div>
            )}

            {anniContabili.length > 0 && (
              <div style={{ marginTop: 18, padding: 14, borderRadius: 10,
                background: "var(--panel-strong)", border: "1px solid var(--line)",
                display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
                <div style={{ flex: 1, minWidth: 220 }}>
                  <strong style={{ fontSize: 14 }}>Certificazione Unica (CU)</strong>
                  <p style={{ margin: "2px 0 0", fontSize: 12, opacity: 0.7 }}>
                    È integrato soltanto il modello CU 2026, relativo ai redditi 2025. I dati degli
                    altri anni restano disponibili nei riepiloghi, ma non vengono sovrapposti a un
                    modulo ufficiale dell'anno sbagliato. Gli archivi pre-workflow non risultano
                    validati retroattivamente.
                  </p>
                </div>
                {anniCuPdf.length > 0 ? (
                  <>
                    <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
                      Anno redditi
                      <select value={annoCuScelto} onChange={(e) => setAnnoCu(Number(e.target.value))}>
                        {anniCuPdf.map((a) => <option key={a} value={a}>{a}</option>)}
                      </select>
                    </label>
                    <button className="btn secondary" type="button" disabled={scaricandoCu} onClick={scaricaCu}>
                      {scaricandoCu ? "⏳ Download..." : "📄 Scarica CU 2026"}
                    </button>
                  </>
                ) : (
                  <span className="status-pill amber">PDF ufficiale non disponibile per questi anni</span>
                )}
              </div>
            )}
          </>
        )}

        {errore && <p className="errore" role="alert" aria-live="assertive">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>A cosa serve</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          Solo un cedolino <b>chiuso</b> dal nuovo workflow è ristampabile identico. Gli archivi
          precedenti restano nei conteggi per continuità, ma sono ricostruzioni storiche da verificare;
          bozze e versioni in lavorazione non alimentano gli adempimenti.
        </div>
        <div className="rail-metric"><span>Cedolini del dipendente</span><strong className="num">{lista.length}</strong></div>
        <div className="rail-metric"><span>Chiusi</span><strong className="num">{lista.filter((c) => statoEffettivo(c) === "Chiusa").length}</strong></div>
        <div className="rail-metric"><span>Storici pre-workflow</span><strong className="num">{lista.filter((c) => statoEffettivo(c) === "PreWorkflow").length}</strong></div>
        <div className="rail-metric"><span>Inclusi nei conteggi (storici da verificare)</span><strong className="num">{lista.filter((c) => alimentaAdempimenti(statoEffettivo(c))).length}</strong></div>
        <div className="rail-metric"><span>Firma/timbro datore</span><strong>{azienda?.haFirmaSiglaTimbro ? "Configurata" : "Da configurare"}</strong></div>
      </aside>
    </div>
  );
}
