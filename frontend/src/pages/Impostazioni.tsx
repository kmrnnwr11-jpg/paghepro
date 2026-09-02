import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type FormEvent,
  type ReactNode,
} from "react";
import {
  Building2,
  Check,
  ChevronRight,
  FileText,
  KeyRound,
  Monitor,
  RotateCcw,
  Settings2,
  ShieldCheck,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { api, getRuolo } from "../api";
import {
  collegamentiImpostazioniPerRuolo,
  CoordinatoreSincronizzazioneImpostazioni,
  famigliaGraficaStile,
  hashSezioneImpostazioni,
  SEZIONE_IMPOSTAZIONI_INIZIALE,
  SEZIONI_IMPOSTAZIONI,
  sezioneImpostazioniDaNavigazione,
  type SezioneImpostazioni,
} from "../impostazioni";
import { SceltaStileBusta } from "../SceltaStileBusta";
import {
  applicaAspetto,
  dettaglioStileBusta,
  haPreferenzeDaSincronizzare,
  preferenzaLocale,
  salvaPreferenzaConEsito,
  STILE_BUSTA_PREDEFINITO,
  stileBustaPreferito,
  sincronizzaPreferenzeDalServer,
  type EsitoSincronizzazionePreferenze,
  type StileBusta,
} from "../preferenze";

type StatoSincronizzazione = "caricamento" | "salvataggio" | EsitoSincronizzazionePreferenze;
type Messaggio = { tipo: "successo" | "errore" | "avviso"; testo: string } | null;
type StatoDueFattori = "caricamento" | "attivo" | "non-attivo" | "non-disponibile";

const ICONE_SEZIONI: Record<SezioneImpostazioni, LucideIcon> = {
  documenti: FileText,
  interfaccia: Monitor,
  aurora: Sparkles,
  sicurezza: ShieldCheck,
  studio: Building2,
};

const OPZIONI_SCALA = [
  { valore: "normale", titolo: "Normale", descrizione: "Equilibrata" },
  { valore: "grande", titolo: "Grande", descrizione: "Più leggibile" },
  { valore: "molto-grande", titolo: "Molto grande", descrizione: "Massima leggibilità" },
] as const;

const OPZIONI_FONT_UI = [
  { valore: "sistema", titolo: "Sistema", descrizione: "Familiare e veloce" },
  { valore: "lato", titolo: "Lato", descrizione: "Coerente con i documenti" },
] as const;

const OPZIONI_DENSITA = [
  { valore: "comoda", titolo: "Comoda", descrizione: "Più spazio" },
  { valore: "compatta", titolo: "Compatta", descrizione: "Più righe" },
] as const;

function IntestazionePannello({
  icona,
  soprattitolo,
  titolo,
  descrizione,
}: {
  icona: ReactNode;
  soprattitolo: string;
  titolo: string;
  descrizione: string;
}) {
  return (
    <header className="settings-panel-header">
      <span className="settings-panel-icon" aria-hidden="true">{icona}</span>
      <div>
        <span className="settings-eyebrow">{soprattitolo}</span>
        <h2>{titolo}</h2>
        <p>{descrizione}</p>
      </div>
    </header>
  );
}

function MessaggioStato({ messaggio, id }: { messaggio: Messaggio; id?: string }) {
  if (!messaggio) return null;
  return (
    <p
      id={id}
      className={`settings-message ${messaggio.tipo}`}
      role={messaggio.tipo === "errore" ? "alert" : "status"}
      aria-live={messaggio.tipo === "errore" ? "assertive" : "polite"}
    >
      {messaggio.testo}
    </p>
  );
}

function GruppoScelte({
  etichetta,
  descrizione,
  valore,
  opzioni,
  onChange,
}: {
  etichetta: string;
  descrizione: string;
  valore: string;
  opzioni: readonly { valore: string; titolo: string; descrizione: string }[];
  onChange: (valore: string) => void;
}) {
  return (
    <div className="settings-choice-block">
      <div className="settings-control-heading">
        <strong>{etichetta}</strong>
        <span>{descrizione}</span>
      </div>
      <div className="settings-segmented" role="group" aria-label={etichetta}>
        {opzioni.map((opzione) => (
          <button
            key={opzione.valore}
            type="button"
            className={valore === opzione.valore ? "active" : ""}
            aria-pressed={valore === opzione.valore}
            onClick={() => onChange(opzione.valore)}
          >
            <span>{opzione.titolo}</span>
            <small>{opzione.descrizione}</small>
          </button>
        ))}
      </div>
    </div>
  );
}

function StatoPreferenze({ stato }: { stato: StatoSincronizzazione }) {
  const contenuto = stato === "caricamento"
    ? ["Caricamento preferenze", "Recupero delle tue impostazioni"]
    : stato === "salvataggio"
      ? ["Salvataggio in corso", "Aggiornamento del tuo account"]
      : stato === "server"
        ? ["Preferenze sincronizzate", "Disponibili sui tuoi dispositivi"]
        : ["Salvate su questo dispositivo", "Il server non è raggiungibile ora"];
  return (
    <div className={`settings-sync-state ${stato}`} role="status" aria-live="polite">
      <span aria-hidden="true"><Check size={15} /></span>
      <div>
        <strong>{contenuto[0]}</strong>
        <small>{contenuto[1]}</small>
      </div>
    </div>
  );
}

export function Impostazioni() {
  const [pronto, setPronto] = useState(false);
  const [sezioneAttiva, setSezioneAttiva] = useState<SezioneImpostazioni>(
    () => sezioneImpostazioniDaNavigazione(window.location.hash) ?? SEZIONE_IMPOSTAZIONI_INIZIALE,
  );
  const [statoSincronizzazione, setStatoSincronizzazione] = useState<StatoSincronizzazione>("caricamento");
  const coordinatoreSincronizzazione = useRef(new CoordinatoreSincronizzazioneImpostazioni());
  const versioneDicitura = useRef(0);

  const [stile, setStile] = useState<StileBusta>(STILE_BUSTA_PREDEFINITO);
  const [fontDocumenti, setFontDocumenti] = useState("lato");
  const [dicitura, setDicitura] = useState("");
  const [dicituraModificata, setDicituraModificata] = useState(false);
  const [esitoDicitura, setEsitoDicitura] = useState<Messaggio>(null);
  const [risposteBrevi, setRisposteBrevi] = useState("0");
  const [scala, setScala] = useState("normale");
  const [fontUi, setFontUi] = useState("sistema");
  const [densita, setDensita] = useState("comoda");
  const [ai, setAi] = useState<{ provider: string; attivo: boolean; consumoMese: number; limiteMensile: number } | null>(null);

  const [pwAttuale, setPwAttuale] = useState("");
  const [pwNuova, setPwNuova] = useState("");
  const [pwConferma, setPwConferma] = useState("");
  const [passwordInCorso, setPasswordInCorso] = useState(false);
  const [esitoPassword, setEsitoPassword] = useState<Messaggio>(null);
  const [dueFattori, setDueFattori] = useState<{ segreto: string; uriOtpauth: string } | null>(null);
  const [statoDueFattori, setStatoDueFattori] = useState<StatoDueFattori>("caricamento");
  const [codice2fa, setCodice2fa] = useState("");
  const [dueFattoriInCorso, setDueFattoriInCorso] = useState(false);
  const [esito2fa, setEsito2fa] = useState<Messaggio>(null);

  // Ogni categoria è una pagina con il suo indirizzo: il cambio arriva dall'hash
  // (link diretti, avanti/indietro del browser) oltre che dai pulsanti qui sotto.
  useEffect(() => {
    const aggiornaDaHash = () => {
      const sezione = sezioneImpostazioniDaNavigazione(window.location.hash);
      if (sezione) setSezioneAttiva(sezione);
    };
    window.addEventListener("hashchange", aggiornaDaHash);
    return () => window.removeEventListener("hashchange", aggiornaDaHash);
  }, []);

  function apriSezione(sezione: SezioneImpostazioni) {
    setSezioneAttiva(sezione);
    const destinazione = `#${hashSezioneImpostazioni(sezione)}`;
    if (window.location.hash !== destinazione) window.location.hash = destinazione.slice(1);
  }

  useEffect(() => {
    let attivo = true;
    (async () => {
      const esito = await sincronizzaPreferenzeDalServer();
      if (!attivo) return;
      setStile(stileBustaPreferito());
      setFontDocumenti(preferenzaLocale("font_documenti", "lato") || "lato");
      setDicitura(preferenzaLocale("dicitura_studio", ""));
      setRisposteBrevi(preferenzaLocale("risposte_brevi_aurora", "0"));
      setScala(preferenzaLocale("scala_testo", "normale") || "normale");
      setFontUi(preferenzaLocale("font_ui", "sistema") || "sistema");
      setDensita(preferenzaLocale("densita", "comoda") || "comoda");
      setStatoSincronizzazione(esito);
      setPronto(true);
      api.statoAi().then((stato) => { if (attivo) setAi(stato); }).catch(() => { /* informativo */ });
      api.dueFattoriStato()
        .then((stato) => { if (attivo) setStatoDueFattori(stato.dueFattoriAttivo ? "attivo" : "non-attivo"); })
        .catch(() => { if (attivo) setStatoDueFattori("non-disponibile"); });
    })();
    return () => { attivo = false; };
  }, []);

  async function aggiornaPreferenza(chiave: string, valore: string, applica = false) {
    setStatoSincronizzazione(coordinatoreSincronizzazione.current.inizia());
    const salvataggio = salvaPreferenzaConEsito(chiave, valore);
    if (applica) applicaAspetto();
    const esito = await salvataggio;
    const statoAggregato = coordinatoreSincronizzazione.current.completa(esito);
    setStatoSincronizzazione(
      statoAggregato === "server" && haPreferenzeDaSincronizzare() ? "locale" : statoAggregato,
    );
    return esito;
  }

  async function salvaDicitura() {
    setEsitoDicitura(null);
    const valoreSalvato = dicitura.trim();
    const versioneSalvata = versioneDicitura.current;
    const esito = await aggiornaPreferenza("dicitura_studio", valoreSalvato);
    if (versioneSalvata !== versioneDicitura.current) return;
    setDicitura(valoreSalvato);
    setDicituraModificata(false);
    setEsitoDicitura(esito === "server"
      ? { tipo: "successo", testo: "Dicitura salvata e sincronizzata con il tuo account." }
      : { tipo: "avviso", testo: "Dicitura salvata su questo dispositivo; sincronizzazione non disponibile." });
  }

  async function ripristinaAspetto() {
    setScala("normale");
    setFontUi("sistema");
    setDensita("comoda");
    await Promise.all([
      aggiornaPreferenza("scala_testo", "normale"),
      aggiornaPreferenza("font_ui", "sistema"),
      aggiornaPreferenza("densita", "comoda"),
    ]);
    applicaAspetto();
  }

  async function inviaPassword(evento: FormEvent) {
    evento.preventDefault();
    if (passwordInCorso) return;
    setEsitoPassword(null);
    if (pwNuova.length < 8) {
      setEsitoPassword({ tipo: "errore", testo: "La nuova password deve avere almeno 8 caratteri." });
      return;
    }
    if (pwNuova !== pwConferma) {
      setEsitoPassword({ tipo: "errore", testo: "La conferma non coincide con la nuova password." });
      return;
    }
    setPasswordInCorso(true);
    try {
      await api.cambiaPassword(pwAttuale, pwNuova);
      setEsitoPassword({ tipo: "successo", testo: "Password cambiata correttamente." });
      setPwAttuale("");
      setPwNuova("");
      setPwConferma("");
    } catch (e) {
      setEsitoPassword({
        tipo: "errore",
        testo: e instanceof Error ? e.message : "Impossibile cambiare la password.",
      });
    } finally {
      setPasswordInCorso(false);
    }
  }

  function aggiornaCodice2fa(valore: string) {
    setCodice2fa(valore.replace(/\D/g, "").slice(0, 6));
    setEsito2fa(null);
  }

  async function inizia2fa() {
    if (dueFattoriInCorso) return;
    setDueFattoriInCorso(true);
    setCodice2fa("");
    setEsito2fa(null);
    try {
      setDueFattori(await api.dueFattoriInizia());
    } catch (e) {
      setEsito2fa({
        tipo: "errore",
        testo: e instanceof Error ? e.message : "Impossibile iniziare la configurazione 2FA.",
      });
    } finally {
      setDueFattoriInCorso(false);
    }
  }

  async function attiva2fa() {
    if (dueFattoriInCorso || codice2fa.length !== 6) return;
    setDueFattoriInCorso(true);
    setEsito2fa(null);
    try {
      await api.dueFattoriAttiva(codice2fa);
      setStatoDueFattori("attivo");
      setEsito2fa({ tipo: "successo", testo: "Verifica in due passaggi attivata." });
      setDueFattori(null);
      setCodice2fa("");
    } catch (e) {
      setEsito2fa({
        tipo: "errore",
        testo: e instanceof Error ? e.message : "Codice non valido.",
      });
    } finally {
      setDueFattoriInCorso(false);
    }
  }

  async function disattiva2fa() {
    if (dueFattoriInCorso || codice2fa.length !== 6) return;
    setDueFattoriInCorso(true);
    setEsito2fa(null);
    try {
      await api.dueFattoriDisattiva(codice2fa);
      setStatoDueFattori("non-attivo");
      setEsito2fa({ tipo: "successo", testo: "Verifica in due passaggi disattivata." });
      setCodice2fa("");
    } catch (e) {
      setEsito2fa({
        tipo: "errore",
        testo: e instanceof Error ? e.message : "Codice non valido.",
      });
    } finally {
      setDueFattoriInCorso(false);
    }
  }

  if (!pronto) {
    return (
      <section className="glass-card settings-loading" aria-busy="true" aria-label="Caricamento impostazioni">
        <span className="settings-loading-icon" aria-hidden="true"><Settings2 size={24} /></span>
        <div><strong>Preparazione delle impostazioni</strong><p>Recupero le preferenze del tuo account…</p></div>
      </section>
    );
  }

  const dettaglioStile = dettaglioStileBusta(stile);
  const famigliaStile = famigliaGraficaStile(stile);
  const stileAnteprima = {
    "--settings-preview-accent": dettaglioStile.accento,
    "--settings-preview-bg": dettaglioStile.sfondo,
  } as CSSProperties;
  const collegamentiStudio = collegamentiImpostazioniPerRuolo(getRuolo());
  const consumoPercentuale = ai?.attivo && ai.limiteMensile > 0
    ? Math.min(100, Math.round((ai.consumoMese / ai.limiteMensile) * 100))
    : 0;

  return (
    <div className="settings-page">
      <header className="glass-card settings-hero">
        <span className="settings-hero-icon" aria-hidden="true"><Settings2 size={25} /></span>
        <div className="settings-hero-copy">
          <span className="settings-eyebrow">Studio e account</span>
          <h1>Impostazioni</h1>
          <p>Personalizza il tuo lavoro, proteggi l’account e raggiungi la configurazione dello studio.</p>
        </div>
        <StatoPreferenze stato={statoSincronizzazione} />
      </header>

      <div className="settings-workspace">
        <nav className="glass-card settings-navigation" aria-label="Categorie delle impostazioni">
          <div className="settings-nav-intro">
            <strong>Tutto al suo posto</strong>
            <span>Scegli una categoria per vedere solo ciò che ti serve.</span>
          </div>
          {["Preferenze", "Account", "Studio"].map((gruppo) => (
            <div className="settings-nav-group" key={gruppo}>
              <span className="settings-nav-label">{gruppo}</span>
              <div className="settings-nav-list">
                {SEZIONI_IMPOSTAZIONI.filter((sezione) => sezione.gruppo === gruppo).map((sezione) => {
                  const Icona = ICONE_SEZIONI[sezione.id];
                  const attiva = sezioneAttiva === sezione.id;
                  return (
                    <button
                      key={sezione.id}
                      type="button"
                      className={`settings-nav-button ${attiva ? "active" : ""}`}
                      aria-current={attiva ? "page" : undefined}
                      aria-controls="pannello-impostazioni"
                      onClick={() => apriSezione(sezione.id)}
                    >
                      <span className="settings-nav-icon" aria-hidden="true"><Icona size={17} /></span>
                      <span><strong>{sezione.titolo}</strong><small>{sezione.descrizione}</small></span>
                      <ChevronRight size={16} aria-hidden="true" />
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
          <div className="settings-nav-note">
            <ShieldCheck size={16} aria-hidden="true" />
            <span><strong>Preferenze personali</strong>Stile e aspetto valgono per il tuo account.</span>
          </div>
        </nav>

        <section
          className="glass-card settings-panel"
          id="pannello-impostazioni"
          role="region"
          aria-live="polite"
          aria-label={SEZIONI_IMPOSTAZIONI.find((sezione) => sezione.id === sezioneAttiva)?.titolo}
        >
          {sezioneAttiva === "documenti" && (
            <>
              <IntestazionePannello
                icona={<FileText size={20} />}
                soprattitolo="Preferenza personale"
                titolo="Buste paga e documenti"
                descrizione="Scegli come presentare i nuovi prospetti senza cambiare numeri o documenti già chiusi."
              />
              <div className="settings-document-layout">
                <div className="settings-card-stack">
                  <article className="settings-surface-card">
                    <label className="settings-control-label" htmlFor="settings-stile-busta">
                      <strong>Stile della busta paga</strong>
                      <span>15 stili organizzati in tre famiglie professionali.</span>
                    </label>
                    <SceltaStileBusta
                      id="settings-stile-busta"
                      ariaLabel="Stile predefinito della busta paga"
                      value={stile}
                      onChange={(nuovoStile) => {
                        setStile(nuovoStile);
                        void aggiornaPreferenza("stile_busta", nuovoStile);
                      }}
                    />
                  </article>

                  <article className="settings-surface-card">
                    <label className="settings-control-label" htmlFor="settings-font-documenti">
                      <strong>Carattere dei PDF</strong>
                      <span>Tutti i caratteri sono incorporati nel documento.</span>
                    </label>
                    <select
                      id="settings-font-documenti"
                      value={fontDocumenti}
                      onChange={(evento) => {
                        setFontDocumenti(evento.target.value);
                        void aggiornaPreferenza("font_documenti", evento.target.value);
                      }}
                    >
                      <option value="lato">Lato (predefinito)</option>
                      <option value="carlito">Carlito (stile Calibri)</option>
                      <option value="plex">IBM Plex Sans</option>
                    </select>
                  </article>

                  <article className="settings-surface-card settings-surface-card-wide">
                    <label className="settings-control-label" htmlFor="settings-dicitura-studio">
                      <strong>Dicitura a piè di pagina</strong>
                      <span>Facoltativa, massimo 90 caratteri. Non sostituisce gli avvisi obbligatori.</span>
                    </label>
                    <div className="settings-inline-action">
                      <input
                        id="settings-dicitura-studio"
                        type="text"
                        maxLength={90}
                        value={dicitura}
                        placeholder="Es. Elaborato da Studio Rossi — Piacenza"
                        onChange={(evento) => {
                          setDicitura(evento.target.value);
                          versioneDicitura.current++;
                          setDicituraModificata(true);
                          setEsitoDicitura(null);
                        }}
                      />
                      <button
                        className="btn secondary"
                        type="button"
                        disabled={!dicituraModificata}
                        onClick={() => void salvaDicitura()}
                      >
                        Salva dicitura
                      </button>
                    </div>
                    <MessaggioStato messaggio={esitoDicitura} />
                  </article>
                </div>

                <aside className="settings-document-preview" aria-label="Anteprima grafica dello stile selezionato">
                  <div className="settings-preview-heading">
                    <span>Anteprima grafica</span>
                    <em>{famigliaStile === "editoriale" ? "Prospetto esteso" : "Prospetto compatto"}</em>
                  </div>
                  <div className="settings-paper-stage" style={stileAnteprima}>
                    <div className={`settings-paper settings-paper-${famigliaStile}`} aria-hidden="true">
                      <div className="settings-paper-brand">PAGHEPRO</div>
                      <div className="settings-paper-title" />
                      <div className="settings-paper-meta"><i /><i /><i /></div>
                      <div className="settings-paper-summary"><i /><i /><i /></div>
                      <div className="settings-paper-table"><i /><i /><i /><i /><i /></div>
                      <div className="settings-paper-total" />
                      <div className="settings-paper-footer" />
                    </div>
                  </div>
                  <div className="settings-preview-caption">
                    <strong>{dettaglioStile.nome}</strong>
                    <span>{dettaglioStile.descrizione}</span>
                    <small>Nessun dato reale è mostrato nell’anteprima.</small>
                  </div>
                </aside>
              </div>
              <div className="settings-info-strip">
                <ShieldCheck size={17} aria-hidden="true" />
                <span><strong>I PDF chiusi restano identici.</strong> Le modifiche valgono per nuove anteprime e nuove chiusure.</span>
              </div>
            </>
          )}

          {sezioneAttiva === "interfaccia" && (
            <>
              <IntestazionePannello
                icona={<Monitor size={20} />}
                soprattitolo="Preferenza personale"
                titolo="Aspetto e accessibilità"
                descrizione="Adatta l’interfaccia alla tua vista e alla quantità di informazioni che vuoi vedere."
              />
              <div className="settings-card-stack">
                <article className="settings-surface-card">
                  <GruppoScelte etichetta="Dimensione del testo" descrizione="Ingrandisce testi e controlli in tutto il software." valore={scala} opzioni={OPZIONI_SCALA}
                    onChange={(valore) => { setScala(valore); void aggiornaPreferenza("scala_testo", valore, true); }} />
                </article>
                <article className="settings-surface-card">
                  <GruppoScelte etichetta="Carattere dell’interfaccia" descrizione="Scegli il carattere usato nei menu, moduli e tabelle." valore={fontUi} opzioni={OPZIONI_FONT_UI}
                    onChange={(valore) => { setFontUi(valore); void aggiornaPreferenza("font_ui", valore, true); }} />
                </article>
                <article className="settings-surface-card">
                  <GruppoScelte etichetta="Spaziatura" descrizione="La modalità compatta mostra più righe mantenendo gli stessi dati." valore={densita} opzioni={OPZIONI_DENSITA}
                    onChange={(valore) => { setDensita(valore); void aggiornaPreferenza("densita", valore, true); }} />
                </article>
              </div>
              <div className="settings-interface-preview">
                <div><span>Anteprima immediata</span><strong>Testo chiaro, controlli ordinati</strong><p>Le modifiche sono già applicate a questa pagina.</p></div>
                <div className="settings-interface-tags" aria-label="Scelte correnti">
                  <span>{OPZIONI_SCALA.find((opzione) => opzione.valore === scala)?.titolo}</span>
                  <span>{OPZIONI_FONT_UI.find((opzione) => opzione.valore === fontUi)?.titolo}</span>
                  <span>{OPZIONI_DENSITA.find((opzione) => opzione.valore === densita)?.titolo}</span>
                </div>
                <button className="btn secondary compact" type="button" onClick={() => void ripristinaAspetto()}>
                  <RotateCcw size={15} aria-hidden="true" /> Ripristina
                </button>
              </div>
            </>
          )}

          {sezioneAttiva === "aurora" && (
            <>
              <IntestazionePannello icona={<Sparkles size={20} />} soprattitolo="Preferenza personale" titolo="Aurora"
                descrizione="Regola la lunghezza delle risposte e controlla lo stato dell’assistente." />
              <div className="settings-two-columns">
                <article className="settings-surface-card">
                  <label className="settings-control-label" htmlFor="settings-risposte-aurora">
                    <strong>Stile delle risposte</strong><span>La scelta cambia solo la lunghezza, mai permessi o strumenti.</span>
                  </label>
                  <select id="settings-risposte-aurora" value={risposteBrevi} onChange={(evento) => {
                    setRisposteBrevi(evento.target.value); void aggiornaPreferenza("risposte_brevi_aurora", evento.target.value);
                  }}>
                    <option value="0">Risposte normali</option><option value="1">Risposte brevi (2–3 frasi)</option>
                  </select>
                </article>
                <article className="settings-assistant-card">
                  <div className="settings-assistant-heading">
                    <span className={`settings-assistant-dot ${ai?.attivo ? "online" : ""}`} aria-hidden="true" />
                    <div><small>Stato assistente</small><strong>{ai?.attivo ? ai.provider : "Non attivo"}</strong></div>
                  </div>
                  {ai?.attivo ? (
                    <>
                      <div className="settings-usage-row"><span>Utilizzo del mese</span><strong>{ai.consumoMese} / {ai.limiteMensile === 0 ? "senza limite" : ai.limiteMensile}</strong></div>
                      {ai.limiteMensile > 0 && (
                        <div className="settings-usage-track" role="progressbar" aria-label="Utilizzo mensile di Aurora"
                          aria-valuemin={0} aria-valuemax={ai.limiteMensile} aria-valuenow={ai.consumoMese}>
                          <span style={{ width: `${consumoPercentuale}%` }} />
                        </div>
                      )}
                    </>
                  ) : <p>Lo stato live non è disponibile. Consenso e strumenti si gestiscono dal pannello Aurora.</p>}
                </article>
              </div>
              <div className="settings-info-strip"><Sparkles size={17} aria-hidden="true" /><span><strong>Controllo separato.</strong> Questa preferenza non attiva Aurora e non amplia il suo accesso ai dati.</span></div>
            </>
          )}

          {sezioneAttiva === "sicurezza" && (
            <>
              <IntestazionePannello icona={<ShieldCheck size={20} />} soprattitolo="Il tuo account" titolo="Sicurezza dell’accesso"
                descrizione="Aggiorna la password e aggiungi un secondo controllo oltre alla password." />
              <div className="settings-security-grid">
                <form className="settings-security-card" onSubmit={(evento) => void inviaPassword(evento)}>
                  <div className="settings-security-title"><span aria-hidden="true"><KeyRound size={18} /></span><div><strong>Cambia password</strong><small>Usa almeno 8 caratteri.</small></div></div>
                  <label htmlFor="settings-password-attuale">Password attuale</label>
                  <input id="settings-password-attuale" type="password" autoComplete="current-password" value={pwAttuale}
                    onChange={(evento) => { setPwAttuale(evento.target.value); setEsitoPassword(null); }} required />
                  <label htmlFor="settings-password-nuova">Nuova password</label>
                  <input id="settings-password-nuova" type="password" autoComplete="new-password" minLength={8}
                    aria-invalid={esitoPassword?.tipo === "errore" || undefined}
                    aria-describedby={esitoPassword ? "settings-password-aiuto settings-password-esito" : "settings-password-aiuto"} value={pwNuova}
                    onChange={(evento) => { setPwNuova(evento.target.value); setEsitoPassword(null); }} required />
                  <small id="settings-password-aiuto">Minimo 8 caratteri.</small>
                  <label htmlFor="settings-password-conferma">Ripeti la nuova password</label>
                  <input id="settings-password-conferma" type="password" autoComplete="new-password" minLength={8} value={pwConferma}
                    aria-invalid={esitoPassword?.tipo === "errore" || undefined}
                    aria-describedby={esitoPassword ? "settings-password-esito" : undefined}
                    onChange={(evento) => { setPwConferma(evento.target.value); setEsitoPassword(null); }} required />
                  <button className="btn primary" type="submit" disabled={passwordInCorso || !pwAttuale || pwNuova.length < 8 || !pwConferma}>
                    {passwordInCorso ? "Aggiornamento…" : "Aggiorna password"}
                  </button>
                  <MessaggioStato id="settings-password-esito" messaggio={esitoPassword} />
                </form>

                <div className="settings-security-card">
                  <div className="settings-security-title"><span aria-hidden="true"><ShieldCheck size={18} /></span><div><strong>Verifica in due passaggi</strong><small>Codice temporaneo a 6 cifre.</small></div></div>
                  {!dueFattori ? (
                    <>
                      <p className={`settings-security-status ${statoDueFattori}`} role="status">
                        {statoDueFattori === "caricamento" && "Controllo dello stato 2FA…"}
                        {statoDueFattori === "attivo" && "La verifica in due passaggi è attiva."}
                        {statoDueFattori === "non-attivo" && "La verifica in due passaggi non è attiva."}
                        {statoDueFattori === "non-disponibile" && "Stato 2FA non disponibile in questo momento."}
                      </p>
                      {statoDueFattori !== "attivo" && (
                        <button className="btn primary" type="button" disabled={dueFattoriInCorso || statoDueFattori === "caricamento"} onClick={() => void inizia2fa()}>
                          {dueFattoriInCorso ? "Preparazione…" : "Configura 2FA"}
                        </button>
                      )}
                      <details className="settings-details" open={statoDueFattori === "attivo" || undefined}>
                        <summary>{statoDueFattori === "attivo" ? "Disattiva la 2FA" : "Devi disattivare la 2FA?"}</summary>
                        <label htmlFor="settings-2fa-disattiva">Codice mostrato dall’app</label>
                        <div className="settings-code-action">
                          <input id="settings-2fa-disattiva" type="text" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6}
                            aria-invalid={esito2fa?.tipo === "errore" || undefined}
                            aria-describedby={esito2fa ? "settings-2fa-esito" : undefined}
                            placeholder="000000" value={codice2fa} onChange={(evento) => aggiornaCodice2fa(evento.target.value)} />
                          <button className="btn secondary" type="button" disabled={dueFattoriInCorso || codice2fa.length !== 6} onClick={() => void disattiva2fa()}>
                            {dueFattoriInCorso ? "Attendi…" : "Disattiva"}
                          </button>
                        </div>
                      </details>
                    </>
                  ) : (
                    <div className="settings-enrollment">
                      <ol><li>Apri la tua app di autenticazione.</li><li>Aggiungi un account usando il segreto qui sotto.</li><li>Inserisci il codice generato e conferma.</li></ol>
                      <code>{dueFattori.segreto}</code>
                      <a href={dueFattori.uriOtpauth}>Apri nell’app di autenticazione</a>
                      <label htmlFor="settings-2fa-attiva">Codice a 6 cifre</label>
                      <input id="settings-2fa-attiva" type="text" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6}
                        aria-invalid={esito2fa?.tipo === "errore" || undefined}
                        aria-describedby={esito2fa ? "settings-2fa-esito" : undefined}
                        placeholder="000000" value={codice2fa} onChange={(evento) => aggiornaCodice2fa(evento.target.value)} />
                      <div className="settings-form-actions">
                        <button className="btn primary" type="button" disabled={dueFattoriInCorso || codice2fa.length !== 6} onClick={() => void attiva2fa()}>
                          {dueFattoriInCorso ? "Verifica…" : "Conferma e attiva"}
                        </button>
                        <button className="btn secondary" type="button" disabled={dueFattoriInCorso}
                          onClick={() => { setDueFattori(null); setCodice2fa(""); setEsito2fa(null); }}>Annulla</button>
                      </div>
                    </div>
                  )}
                  <MessaggioStato id="settings-2fa-esito" messaggio={esito2fa} />
                </div>
              </div>
            </>
          )}

          {sezioneAttiva === "studio" && (
            <>
              <IntestazionePannello icona={<Building2 size={20} />} soprattitolo="Gestione condivisa" titolo="Configurazione dello studio"
                descrizione="Apri le aree operative dedicate: ogni pagina salva e controlla i propri dati." />
              <div className="settings-link-grid">
                {collegamentiStudio.map((voce) => (
                  <button className="settings-link-card" type="button" key={voce.key} onClick={() => { window.location.hash = `#${voce.key}`; }}>
                    <span className="settings-link-icon" aria-hidden="true"><Building2 size={18} /></span>
                    <span><strong>{voce.label}</strong><small>{voce.detail}</small></span>
                    <ChevronRight size={17} aria-hidden="true" />
                  </button>
                ))}
              </div>
              <div className="settings-info-strip"><ShieldCheck size={17} aria-hidden="true" /><span><strong>Accessi rispettati.</strong> Vedi soltanto le aree consentite dal tuo ruolo.</span></div>
            </>
          )}
        </section>
      </div>
    </div>
  );
}
