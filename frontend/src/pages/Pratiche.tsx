import { useEffect, useState } from "react";
import {
  api,
  type DatiPraticaEstratti,
  type NuovaPraticaComUnica,
  type PianoComUnica,
  type PraticaComUnica,
} from "../api";

// Pratiche ComUnica: apertura/variazione/cessazione P.IVA e impresa.
// Il software prepara dati, checklist e distinta; l'invio telematico reale
// passa dai canali ufficiali InfoCamere o da un intermediario abilitato.

const TIPI_PRATICA = [
  { value: "NuovaImpresa", label: "Nuova impresa / apertura P.IVA" },
  { value: "Variazione", label: "Variazione dati" },
  { value: "Cessazione", label: "Cessazione attività" },
];

const FORME = [
  { value: "ProfessionistaPersonaFisica", label: "Professionista / persona fisica" },
  { value: "ImpresaIndividuale", label: "Impresa individuale" },
  { value: "SocietaPersone", label: "Società di persone" },
  { value: "SocietaCapitali", label: "Società di capitali" },
];

const REGIMI = [
  { value: "Forfettario", label: "Forfettario" },
  { value: "Semplificato", label: "Semplificato" },
  { value: "Ordinario", label: "Ordinario" },
];

const STATI_ADEMPIMENTO = ["DaPreparare", "Pronto", "Presentato", "Completato", "NonApplicabile"];

// Transizioni ammesse (specchio della macchina a stati del backend, solo per la UI).
const PROSSIMI_STATI: Record<string, string[]> = {
  Bozza: ["Pronta", "Annullata"],
  Pronta: ["Presentata", "Bozza", "Annullata"],
  Presentata: ["Protocollata", "Scartata"],
  Protocollata: ["Evasa", "Scartata"],
  Scartata: ["Bozza"],
  Evasa: [],
  Annullata: [],
};

const tonoStato = (stato: string) =>
  stato === "Evasa" ? "green"
    : stato === "Protocollata" || stato === "Presentata" ? "blue"
    : stato === "Scartata" || stato === "Annullata" ? "red"
    : stato === "Pronta" ? "amber"
    : "slate";

const etichettaTipo = (tipo: string) =>
  TIPI_PRATICA.find((t) => t.value === tipo)?.label ?? tipo;

const FORM_VUOTO: NuovaPraticaComUnica = {
  tipoPratica: "NuovaImpresa",
  formaGiuridica: "ImpresaIndividuale",
  regimeContabile: "Forfettario",
  dataInizioAttivita: new Date().toISOString().slice(0, 10),
  denominazione: "",
  codiceFiscale: "",
  partitaIva: "",
  pec: "",
  codiceAteco: "",
  descrizioneAttivita: "",
  indirizzoSede: "",
  cap: "",
  comune: "",
  provincia: "",
  attivitaCommercio: false,
  attivitaArtigiana: false,
  impresaAgricola: false,
  haDipendentiPrevisti: false,
  soggettaInail: false,
};

// Etichette leggibili dei campi trovati dall'analisi del testo incollato.
const NOMI_CAMPO: Record<string, string> = {
  tipoPratica: "tipo pratica",
  formaGiuridica: "forma giuridica",
  regimeContabile: "regime contabile",
  dataDecorrenza: "data decorrenza",
  denominazione: "denominazione",
  codiceFiscale: "codice fiscale",
  partitaIva: "partita IVA",
  pec: "PEC",
  codiceAteco: "codice ATECO",
  descrizioneAttivita: "descrizione attività",
  indirizzo: "indirizzo",
  cap: "CAP",
  comune: "comune",
  provincia: "provincia",
  attivitaCommercio: "attività di commercio",
  attivitaArtigiana: "attività artigiana",
  impresaAgricola: "impresa agricola",
  haDipendentiPrevisti: "dipendenti previsti",
  soggettaInail: "soggetta INAIL",
};

interface MessaggioChat {
  da: "tu" | "assistente";
  testo: string;
}

export function Pratiche() {
  const [pratiche, setPratiche] = useState<PraticaComUnica[]>([]);
  const [selezionata, setSelezionata] = useState<PraticaComUnica | null>(null);
  const [distinta, setDistinta] = useState<string | null>(null);
  const [form, setForm] = useState<NuovaPraticaComUnica>(FORM_VUOTO);
  const [piano, setPiano] = useState<PianoComUnica | null>(null);
  const [formAperto, setFormAperto] = useState(false);
  const [caricamento, setCaricamento] = useState(true);
  const [lavoro, setLavoro] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [chat, setChat] = useState<MessaggioChat[]>([
    {
      da: "assistente",
      testo:
        "Incolla qui i dati che hai (email del cliente, appunti, messaggio): compilo io il modulo e ti dico cosa manca.",
    },
  ]);
  const [testoChat, setTestoChat] = useState("");

  const ricarica = async () => {
    try {
      setPratiche(await api.pratiche());
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore di caricamento");
    } finally {
      setCaricamento(false);
    }
  };

  useEffect(() => {
    ricarica();
  }, []);

  const campo = (k: keyof NuovaPraticaComUnica, v: string | boolean) => {
    setForm((f) => ({ ...f, [k]: v }));
    setPiano(null); // il piano simulato non vale più se cambiano i dati
  };

  const pulisciMessaggi = () => {
    setErrore(null);
    setInfo(null);
  };

  // Unisce i dati estratti dal testo nel modulo: un campo trovato vince su quello attuale.
  const unisci = (f: NuovaPraticaComUnica, d: DatiPraticaEstratti): NuovaPraticaComUnica => ({
    ...f,
    tipoPratica: d.tipoPratica ?? f.tipoPratica,
    formaGiuridica: d.formaGiuridica ?? f.formaGiuridica,
    regimeContabile: d.regimeContabile ?? f.regimeContabile,
    dataInizioAttivita: d.dataDecorrenza ?? f.dataInizioAttivita,
    denominazione: d.denominazione ?? f.denominazione,
    codiceFiscale: d.codiceFiscale ?? f.codiceFiscale,
    partitaIva: d.partitaIva ?? f.partitaIva,
    pec: d.pec ?? f.pec,
    codiceAteco: d.codiceAteco ?? f.codiceAteco,
    descrizioneAttivita: d.descrizioneAttivita ?? f.descrizioneAttivita,
    indirizzoSede: d.indirizzo ?? f.indirizzoSede,
    cap: d.cap ?? f.cap,
    comune: d.comune ?? f.comune,
    provincia: d.provincia ?? f.provincia,
    attivitaCommercio: d.attivitaCommercio ?? f.attivitaCommercio,
    attivitaArtigiana: d.attivitaArtigiana ?? f.attivitaArtigiana,
    impresaAgricola: d.impresaAgricola ?? f.impresaAgricola,
    haDipendentiPrevisti: d.haDipendentiPrevisti ?? f.haDipendentiPrevisti,
    soggettaInail: d.soggettaInail ?? f.soggettaInail,
  });

  // Cosa manca ancora nel modulo (dopo l'unione) per poter creare la pratica.
  const campiAncoraVuoti = (f: NuovaPraticaComUnica): string[] => {
    const vuoti: string[] = [];
    if (!f.denominazione.trim()) vuoti.push("denominazione / nome e cognome");
    if (!f.codiceFiscale.trim()) vuoti.push("codice fiscale");
    if (!f.codiceAteco.trim()) vuoti.push("codice ATECO");
    if (!f.descrizioneAttivita.trim()) vuoti.push("descrizione attività");
    if (!f.indirizzoSede.trim()) vuoti.push("indirizzo sede");
    if (!f.cap.trim()) vuoti.push("CAP");
    if (!f.comune.trim()) vuoti.push("comune");
    if (!f.provincia.trim()) vuoti.push("provincia (2 lettere)");
    if (f.tipoPratica !== "NuovaImpresa" && !(f.partitaIva ?? "").trim())
      vuoti.push("partita IVA (obbligatoria per variazione/cessazione)");
    return vuoti;
  };

  const analizza = async () => {
    const testo = testoChat.trim();
    if (!testo || lavoro) return;
    pulisciMessaggi();
    setChat((c) => [...c, { da: "tu", testo }]);
    setTestoChat("");
    setLavoro(true);
    try {
      const dati = await api.analizzaTestoPratica(testo);
      const trovati = Object.entries(NOMI_CAMPO)
        .filter(([k]) => {
          const v = (dati as unknown as Record<string, unknown>)[k];
          return v !== null && v !== undefined && v !== "";
        })
        .map(([, nome]) => nome);

      const unito = unisci(form, dati);
      setForm(unito);
      setPiano(null);
      setFormAperto(true);

      const mancano = campiAncoraVuoti(unito);
      let risposta: string;
      if (trovati.length === 0) {
        risposta =
          "Non ho riconosciuto dati in questo testo. Prova a scriverli con l'etichetta davanti, ad esempio: \"Codice fiscale: RSSMRA80A01F205X\" oppure \"PEC: nome@pec.it\".";
      } else {
        risposta = `Fatto, ho compilato: ${trovati.join(", ")}.`;
        risposta += mancano.length > 0
          ? ` Mi serve ancora: ${mancano.join("; ")}. Scrivilo qui sotto (anche solo \"CAP: 20121\") o completa il modulo a mano.`
          : " Il modulo è completo: controlla i campi qui sotto, poi premi \"Simula piano adempimenti\" e \"Crea pratica\".";
      }
      setChat((c) => [...c, { da: "assistente", testo: risposta }]);
    } catch (e) {
      setChat((c) => [
        ...c,
        { da: "assistente", testo: e instanceof Error ? e.message : "Errore nell'analisi del testo." },
      ]);
    } finally {
      setLavoro(false);
    }
  };

  const corpoRichiesta = (): NuovaPraticaComUnica => ({
    ...form,
    partitaIva: form.partitaIva?.trim() || undefined,
    pec: form.pec?.trim() || undefined,
  });

  const simula = async () => {
    pulisciMessaggi();
    setLavoro(true);
    try {
      setPiano(await api.simulaPratica(corpoRichiesta()));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nella simulazione");
    } finally {
      setLavoro(false);
    }
  };

  const crea = async () => {
    pulisciMessaggi();
    setLavoro(true);
    try {
      const pratica = await api.creaPratica(corpoRichiesta());
      setInfo(`Pratica creata: ${pratica.denominazione}.`);
      setForm(FORM_VUOTO);
      setPiano(null);
      setFormAperto(false);
      await ricarica();
      apri(pratica.id);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nella creazione");
    } finally {
      setLavoro(false);
    }
  };

  const apri = async (id: string) => {
    pulisciMessaggi();
    setDistinta(null);
    try {
      setSelezionata(await api.pratica(id));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore di caricamento");
    }
  };

  const cambiaStato = async (stato: string) => {
    if (!selezionata) return;
    pulisciMessaggi();
    try {
      const aggiornata = await api.cambiaStatoPratica(selezionata.id, stato);
      setSelezionata(aggiornata);
      await ricarica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel cambio stato");
    }
  };

  const cambiaAdempimento = async (adempimentoId: string, stato: string) => {
    if (!selezionata) return;
    pulisciMessaggi();
    try {
      await api.aggiornaAdempimentoPratica(selezionata.id, adempimentoId, stato);
      await apri(selezionata.id);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nell'aggiornamento");
    }
  };

  const caricaDistinta = async () => {
    if (!selezionata) return;
    pulisciMessaggi();
    try {
      const r = await api.distintaPratica(selezionata.id);
      setDistinta(r.distinta);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nella distinta");
    }
  };

  const copiaDistinta = async () => {
    if (!distinta) return;
    try {
      await navigator.clipboard.writeText(distinta);
      setInfo("Distinta copiata: incollala su ComunicaStarweb/DIRE o inviala all'intermediario.");
    } catch {
      setErrore("Copia non riuscita: seleziona il testo e copialo a mano.");
    }
  };

  const scaricaDistinta = () => {
    if (!distinta || !selezionata) return;
    const blob = new Blob([distinta], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `distinta-comunica-${selezionata.id}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Pratiche ComUnica</h1>
            <p className="muted">
              Apertura, variazione e cessazione di P.IVA e impresa: dati, checklist per ente e
              distinta pronta per l'invio ufficiale.
            </p>
          </div>
          <button
            className="btn primary"
            type="button"
            onClick={() => setFormAperto((v) => !v)}
          >
            {formAperto ? "Chiudi" : "+ Nuova pratica"}
          </button>
        </div>

        {errore && <div className="alert error">{errore}</div>}
        {info && <div className="alert info" role="status" aria-live="polite">{info}</div>}

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>💬 Assistente compilazione</h2>
              <p className="muted">
                Incolla i dati come li hai (email, messaggio, appunti): il modulo si compila da
                solo e ti dico cosa manca.
              </p>
            </div>
          </div>

          <div
            role="log"
            aria-live="polite"
            style={{ display: "grid", gap: 8, marginBottom: 10, maxHeight: 260, overflowY: "auto" }}
          >
            {chat.map((m, i) => (
              <div
                key={i}
                className={m.da === "assistente" ? "alert info" : ""}
                style={
                  m.da === "tu"
                    ? {
                        justifySelf: "end",
                        maxWidth: "85%",
                        padding: "8px 12px",
                        borderRadius: 12,
                        border: "1px solid rgba(148, 163, 184, 0.35)",
                        whiteSpace: "pre-wrap",
                        fontSize: 13,
                      }
                    : { maxWidth: "92%", whiteSpace: "pre-wrap" }
                }
              >
                {m.da === "tu" ? m.testo : m.testo}
              </div>
            ))}
          </div>

          <label className="field">
            <span>Scrivi o incolla qui</span>
            <textarea
              rows={4}
              value={testoChat}
              onChange={(e) => setTestoChat(e.target.value)}
              placeholder={
                'Es.: "Apertura impresa individuale, forfettario, dal 01/09/2026.\nNome e cognome: Luigi Bianchi\nCodice fiscale: BNCLGU75B02F839W\nATECO: 47.11.40 — negozio minimarket\nSede: Via Napoli 12 - 80100 Napoli (NA)\nPEC: luigi@pec.it"'
              }
            />
          </label>
          <div className="actions-row">
            <button className="btn primary" type="button" onClick={analizza} disabled={lavoro || !testoChat.trim()}>
              {lavoro ? "Analizzo…" : "Analizza e compila"}
            </button>
          </div>
        </section>

        {formAperto && (
          <section className="glass-card page-card">
            <div className="card-title-row">
              <div>
                <h2>Nuova pratica</h2>
                <p className="muted">
                  Compila i dati, simula il piano degli adempimenti, poi crea la pratica.
                </p>
              </div>
            </div>

            <div className="form-grid">
              <label>
                <span>Tipo pratica</span>
                <select value={form.tipoPratica} onChange={(e) => campo("tipoPratica", e.target.value)}>
                  {TIPI_PRATICA.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                </select>
              </label>
              <label>
                <span>Forma giuridica</span>
                <select value={form.formaGiuridica} onChange={(e) => campo("formaGiuridica", e.target.value)}>
                  {FORME.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
                </select>
              </label>
              <label>
                <span>Regime contabile</span>
                <select value={form.regimeContabile} onChange={(e) => campo("regimeContabile", e.target.value)}>
                  {REGIMI.map((r) => <option key={r.value} value={r.value}>{r.label}</option>)}
                </select>
              </label>
              <label>
                <span>Data decorrenza</span>
                <input
                  type="date"
                  value={form.dataInizioAttivita}
                  onChange={(e) => campo("dataInizioAttivita", e.target.value)}
                />
              </label>
              <label>
                <span>Denominazione / nominativo</span>
                <input value={form.denominazione} onChange={(e) => campo("denominazione", e.target.value)} placeholder="Es. Rossi Mario o Rossi S.r.l." />
              </label>
              <label>
                <span>Codice fiscale</span>
                <input value={form.codiceFiscale} onChange={(e) => campo("codiceFiscale", e.target.value)} />
              </label>
              <label>
                <span>Partita IVA {form.tipoPratica === "NuovaImpresa" ? "(se già attribuita)" : "(obbligatoria)"}</span>
                <input value={form.partitaIva ?? ""} onChange={(e) => campo("partitaIva", e.target.value)} />
              </label>
              <label>
                <span>PEC</span>
                <input value={form.pec ?? ""} onChange={(e) => campo("pec", e.target.value)} placeholder="domicilio digitale" />
              </label>
              <label>
                <span>Codice ATECO</span>
                <input value={form.codiceAteco} onChange={(e) => campo("codiceAteco", e.target.value)} placeholder="Es. 47.11.40" />
              </label>
              <label>
                <span>Descrizione attività</span>
                <input value={form.descrizioneAttivita} onChange={(e) => campo("descrizioneAttivita", e.target.value)} />
              </label>
              <label>
                <span>Indirizzo sede</span>
                <input value={form.indirizzoSede} onChange={(e) => campo("indirizzoSede", e.target.value)} />
              </label>
              <label>
                <span>CAP</span>
                <input value={form.cap} onChange={(e) => campo("cap", e.target.value)} />
              </label>
              <label>
                <span>Comune</span>
                <input value={form.comune} onChange={(e) => campo("comune", e.target.value)} />
              </label>
              <label>
                <span>Provincia</span>
                <input value={form.provincia} onChange={(e) => campo("provincia", e.target.value)} maxLength={2} placeholder="MI" />
              </label>
            </div>

            <div className="form-grid">
              {([
                ["attivitaCommercio", "Attività di commercio"],
                ["attivitaArtigiana", "Attività artigiana"],
                ["impresaAgricola", "Impresa agricola"],
                ["haDipendentiPrevisti", "Dipendenti previsti"],
                ["soggettaInail", "Soggetta INAIL"],
              ] as const).map(([k, label]) => (
                <label key={k} className="check-row">
                  <input
                    type="checkbox"
                    checked={Boolean(form[k])}
                    onChange={(e) => campo(k, e.target.checked)}
                  />
                  <strong>{label}</strong>
                </label>
              ))}
            </div>

            <div className="actions-row">
              <button className="btn secondary" type="button" onClick={simula} disabled={lavoro}>
                Simula piano adempimenti
              </button>
              <button className="btn primary" type="button" onClick={crea} disabled={lavoro}>
                Crea pratica
              </button>
            </div>

            {piano && (
              <div style={{ marginTop: 14 }}>
                <h3>Piano simulato — modello {piano.modelloAgenziaEntrate}</h3>
                <ul>
                  {piano.adempimenti.map((a) => (
                    <li key={a.codice}>
                      <strong>{a.ente}</strong> — {a.descrizione}
                    </li>
                  ))}
                </ul>
                {piano.avvertenze.length > 0 && (
                  <div className="alert info">
                    {piano.avvertenze.map((avv) => <div key={avv}>• {avv}</div>)}
                  </div>
                )}
              </div>
            )}
          </section>
        )}

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>Pratiche archiviate</h2>
              <p className="muted">Ogni pratica tiene la checklist per ente e lo stato di lavorazione.</p>
            </div>
          </div>

          {caricamento ? (
            <div className="empty-state">Caricamento…</div>
          ) : pratiche.length === 0 ? (
            <div className="empty-state">
              Nessuna pratica. Crea la prima con "+ Nuova pratica": apertura, variazione o
              cessazione di P.IVA e impresa.
            </div>
          ) : (
            <div className="table-wrap">
              <table className="data-table spacious">
                <thead>
                  <tr>
                    <th>Soggetto</th>
                    <th>Tipo</th>
                    <th>Decorrenza</th>
                    <th>Stato</th>
                    <th className="right">Azioni</th>
                  </tr>
                </thead>
                <tbody>
                  {pratiche.map((p) => (
                    <tr key={p.id}>
                      <td>
                        <strong>{p.denominazione}</strong>
                        <div className="muted">{p.codiceAteco} · {p.comune} ({p.provincia})</div>
                      </td>
                      <td>{etichettaTipo(p.tipoPratica)}</td>
                      <td>{new Date(p.dataInizioAttivita).toLocaleDateString("it-IT")}</td>
                      <td><span className={`status-pill ${tonoStato(p.stato)}`}>{p.stato}</span></td>
                      <td className="right">
                        <button className="mini-action" type="button" onClick={() => apri(p.id)}>
                          Apri
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        {selezionata && (
          <section className="glass-card page-card">
            <div className="card-title-row">
              <div>
                <h2>{selezionata.denominazione}</h2>
                <p className="muted">
                  {etichettaTipo(selezionata.tipoPratica)} · modello {selezionata.modelloAgenziaEntrate}
                  {selezionata.richiedeComunicazioneUnica ? " · Comunicazione Unica" : ""}
                </p>
              </div>
              <span className={`status-pill ${tonoStato(selezionata.stato)}`}>{selezionata.stato}</span>
            </div>

            {(PROSSIMI_STATI[selezionata.stato] ?? []).length > 0 && (
              <div className="actions-row">
                {(PROSSIMI_STATI[selezionata.stato] ?? []).map((s) => (
                  <button key={s} className="btn secondary compact" type="button" onClick={() => cambiaStato(s)}>
                    Segna {s}
                  </button>
                ))}
              </div>
            )}

            <h3>Adempimenti</h3>
            <div className="table-wrap">
              <table className="data-table spacious">
                <thead>
                  <tr>
                    <th>Ente</th>
                    <th>Adempimento</th>
                    <th>Stato</th>
                  </tr>
                </thead>
                <tbody>
                  {selezionata.adempimenti.map((a) => (
                    <tr key={a.id}>
                      <td><strong>{a.ente}</strong></td>
                      <td>{a.descrizione}</td>
                      <td>
                        <select value={a.stato} onChange={(e) => cambiaAdempimento(a.id, e.target.value)}>
                          {STATI_ADEMPIMENTO.map((s) => <option key={s} value={s}>{s}</option>)}
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="actions-row">
              <button className="btn primary" type="button" onClick={caricaDistinta}>
                Genera distinta
              </button>
              {distinta && (
                <>
                  <button className="btn secondary" type="button" onClick={copiaDistinta}>
                    Copia
                  </button>
                  <button className="btn secondary" type="button" onClick={scaricaDistinta}>
                    Scarica .txt
                  </button>
                </>
              )}
              <button className="btn secondary" type="button" onClick={() => setSelezionata(null)}>
                Chiudi dettaglio
              </button>
            </div>

            {distinta && (
              <pre
                style={{
                  whiteSpace: "pre-wrap",
                  fontSize: 12,
                  lineHeight: 1.45,
                  overflowX: "auto",
                  padding: 12,
                }}
              >
                {distinta}
              </pre>
            )}
          </section>
        )}
      </div>

      <aside className="glass-card module-rail">
        <h2>Come funziona</h2>
        <p className="muted">
          1. Crea la pratica: il sistema deduce gli adempimenti per ente (Agenzia Entrate,
          Registro Imprese, INPS, INAIL, SUAP).
        </p>
        <p className="muted">
          2. Lavora la checklist e aggiorna lo stato di ogni adempimento.
        </p>
        <p className="muted">
          3. Genera la distinta e usala su ComunicaStarweb/DIRE (utenza Telemaco) o consegnala
          al commercialista/intermediario abilitato: l'invio telematico reale passa dai canali
          ufficiali.
        </p>
      </aside>
    </div>
  );
}
