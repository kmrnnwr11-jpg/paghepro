import { useEffect, useRef, useState } from "react";
import { Send, ShieldCheck, Sparkles, X } from "lucide-react";
import {
  api,
  type Azienda,
  type CosaVedeAi,
  type EsitoPreflightAi,
  type EsitoQueryAi,
  type GrantAiEmesso,
  type ManifestGrantAi,
  type MessaggioChatAi,
  type RigaCedolinoMeseAi,
  type StatoChatAi,
} from "./api";
import { anteprimaContestoAurora, type ContestoAurora } from "./auroraContext";
import {
  ambitoControlloCedolini,
  contaStatiCedolini,
  richiestaControlloCedolini,
  stessoAmbitoControlloCedolini,
  type AmbitoControlloCedolini,
} from "./auroraCedolini";

// La chat resta senza strumenti. Il controllo cedolini è un percorso deterministico separato:
// preflight visibile, mandato del Governor, proiezione minimizzata e revoca immediata.

interface Bolla {
  id: number;
  chi: "utente" | "aurora";
  testo: string;
  citazione?: string;
}

const SUGGERIMENTI = [
  { etichetta: "Spiega questa pagina", richiesta: "Spiegami in modo semplice a cosa serve questa pagina." },
  { etichetta: "Cosa devo fare", richiesta: "Quali sono i passaggi normali da fare in questa pagina?" },
  { etichetta: "Non ho capito", richiesta: "Non ho capito questa schermata: fammi una domanda per capire cosa mi serve." },
];

const SUGGERIMENTI_CON_TESTO = [
  { etichetta: "Spiegami questo", richiesta: "Spiegami questo testo in parole semplici." },
  { etichetta: "Fammi un esempio", richiesta: "Spiegami questo testo con un esempio pratico." },
  { etichetta: "Cosa significa", richiesta: "Qual è il significato più importante di questo testo?" },
];

const BOZZA_TESTO_SELEZIONATO = "Spiegami questo testo in parole semplici.";

function periodoCorrente() {
  const oggi = new Date();
  return `${oggi.getFullYear()}-${String(oggi.getMonth() + 1).padStart(2, "0")}`;
}

export function AuroraPanel({
  azienda,
  sezione,
  contesto,
  onRimuoviContesto,
  onChiudi,
}: {
  azienda: Azienda | null;
  sezione: string;
  contesto: ContestoAurora | null;
  onRimuoviContesto: () => void;
  onChiudi: () => void;
}) {
  const [bolle, setBolle] = useState<Bolla[]>([]);
  const [storia, setStoria] = useState<MessaggioChatAi[]>([]);
  const [bozza, setBozza] = useState("");
  const [inCorso, setInCorso] = useState(false);
  const [stato, setStato] = useState<StatoChatAi | null>(null);
  const [periodo, setPeriodo] = useState(periodoCorrente);
  const [preflight, setPreflight] = useState<EsitoPreflightAi | null>(null);
  const [ambitoPreflight, setAmbitoPreflight] = useState<AmbitoControlloCedolini | null>(null);
  const [mandato, setMandato] = useState<GrantAiEmesso | null>(null);
  const [mandatiResidui, setMandatiResidui] = useState<ManifestGrantAi[]>([]);
  const [esitoCedolini, setEsitoCedolini] = useState<EsitoQueryAi<RigaCedolinoMeseAi> | null>(null);
  const [trasparenza, setTrasparenza] = useState<CosaVedeAi | null>(null);
  const [erroreControllo, setErroreControllo] = useState<string | null>(null);
  const [controlloInCorso, setControlloInCorso] = useState(false);
  const prossimoId = useRef(1);
  const fondo = useRef<HTMLDivElement>(null);
  const storiaRef = useRef<MessaggioChatAi[]>([]);
  const campoMessaggio = useRef<HTMLTextAreaElement>(null);
  const chiusuraInCorso = useRef(false);
  const ambitoCorrente = azienda ? ambitoControlloCedolini(azienda.id, periodo) : null;
  const ambitoCorrenteRef = useRef<AmbitoControlloCedolini | null>(ambitoCorrente);
  ambitoCorrenteRef.current = ambitoCorrente;
  const preflightCorrente = stessoAmbitoControlloCedolini(ambitoPreflight, ambitoCorrente)
    ? preflight
    : null;

  useEffect(() => { storiaRef.current = storia; }, [storia]);

  useEffect(() => {
    if (!contesto) return;
    setBozza((corrente) => corrente.trim() || BOZZA_TESTO_SELEZIONATO);
  }, [contesto]);

  useEffect(() => {
    const id = window.setTimeout(() => campoMessaggio.current?.focus(), 0);
    return () => window.clearTimeout(id);
  }, []);

  useEffect(() => {
    api.aiChatStato().then(setStato).catch(() => setStato(null));
    api.aiGrants()
      .then((grants) => setMandatiResidui(grants.filter((grant) =>
        grant.stato === "Attivo" && new Date(grant.scadeUtc).getTime() > Date.now())))
      .catch(() => setMandatiResidui([]));
  }, []);

  useEffect(() => {
    if (!mandato || mandato.manifest.aziendaId === (azienda?.id ?? null)) return;
    const grantId = mandato.manifest.grantId;
    const manifestPrecedente = mandato.manifest;
    // Non mostrare mai righe del cliente precedente sotto la nuova intestazione. Conserviamo
    // soltanto il manifest minimo necessario a rendere revocabile un eventuale errore di rete.
    setMandato(null);
    setMandatiResidui((correnti) => [
      manifestPrecedente,
      ...correnti.filter((grant) => grant.grantId !== grantId),
    ]);
    setPreflight(null);
    setAmbitoPreflight(null);
    setEsitoCedolini(null);
    setTrasparenza(null);
    setControlloInCorso(true);
    setErroreControllo("Cambio azienda: sto revocando il mandato precedente.");
    void api.aiRevocaGrant(grantId)
      .then(() => {
        setMandatiResidui((correnti) => correnti.filter((grant) => grant.grantId !== grantId));
        setErroreControllo(null);
      })
      .catch(() => {
        setErroreControllo("Il vecchio mandato non è stato revocato: usa Revoca mandati residui prima di continuare.");
      })
      .finally(() => setControlloInCorso(false));
  }, [azienda?.id, mandato]);

  useEffect(() => {
    fondo.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [bolle, inCorso]);

  const aggiungi = (bolla: Omit<Bolla, "id">) =>
    setBolle((prima) => [...prima, { ...bolla, id: prossimoId.current++ }]);

  const invia = async (testo: string) => {
    const messaggio = testo.trim();
    if (!messaggio || inCorso) return;
    const contestoDaInviare = contesto;
    aggiungi({
      chi: "utente",
      testo: messaggio,
      citazione: contestoDaInviare ? anteprimaContestoAurora(contestoDaInviare, 240) : undefined,
    });
    setBozza("");
    setInCorso(true);
    try {
      const r = await api.aiChat(
        messaggio,
        storiaRef.current,
        azienda?.id ?? null,
        contestoDaInviare
          ? { testo: contestoDaInviare.testo, sezione: contestoDaInviare.sezione }
          : null,
        sezione,
      );
      aggiungi({ chi: "aurora", testo: r.testo });
      if (r.ok) {
        setStoria(r.storia);
        if (contestoDaInviare) onRimuoviContesto();
      } else {
        // Il provider spento risponde in modo controllato: conserva testo e domanda per il retry.
        setBozza(messaggio);
      }
    } catch (e) {
      setBozza(messaggio);
      aggiungi({
        chi: "aurora",
        testo: `Non riesco a parlare con l'assistente: ${e instanceof Error ? e.message : "errore"}.`,
      });
    } finally {
      setInCorso(false);
    }
  };

  const rimuoviContesto = () => {
    setBozza((corrente) => corrente === BOZZA_TESTO_SELEZIONATO ? "" : corrente);
    onRimuoviContesto();
  };

  const suggerimenti = contesto ? SUGGERIMENTI_CON_TESTO : SUGGERIMENTI;
  const conteggi = esitoCedolini ? contaStatiCedolini(esitoCedolini.righe) : null;

  const mostraPreflight = async () => {
    if (!azienda || controlloInCorso) return;
    if (mandatiResidui.length > 0) {
      setErroreControllo("Revoca il mandato precedente prima di iniziare un nuovo controllo.");
      return;
    }
    setControlloInCorso(true);
    setErroreControllo(null);
    setPreflight(null);
    setAmbitoPreflight(null);
    setEsitoCedolini(null);
    setTrasparenza(null);
    const ambitoRichiesto = ambitoControlloCedolini(azienda.id, periodo);
    try {
      const esito = await api.aiPreflight(
        richiestaControlloCedolini(ambitoRichiesto.aziendaId, ambitoRichiesto.periodo),
      );
      setPreflight(esito);
      setAmbitoPreflight(ambitoRichiesto);
    } catch (e) {
      setErroreControllo(e instanceof Error ? e.message : "Preflight non disponibile.");
    } finally {
      setControlloInCorso(false);
    }
  };

  const avviaControllo = async () => {
    if (!azienda || !preflightCorrente?.consentito || controlloInCorso) return;
    const ambitoAvvio = ambitoControlloCedolini(azienda.id, periodo);
    setControlloInCorso(true);
    setErroreControllo(null);
    try {
      const nuovoMandato = await api.aiEmettiGrant(
        richiestaControlloCedolini(ambitoAvvio.aziendaId, ambitoAvvio.periodo),
      );
      if (!stessoAmbitoControlloCedolini(ambitoAvvio, ambitoCorrenteRef.current)) {
        try {
          await api.aiRevocaGrant(nuovoMandato.manifest.grantId);
        } catch {
          setMandatiResidui((correnti) => [
            nuovoMandato.manifest,
            ...correnti.filter((grant) => grant.grantId !== nuovoMandato.manifest.grantId),
          ]);
          throw new Error(
            "Azienda o mese cambiati durante l’avvio e mandato non revocato: usa Revoca mandati residui.",
          );
        }
        throw new Error("Azienda o mese cambiati durante l’avvio: mandato revocato. Ripeti il preflight.");
      }
      setMandato(nuovoMandato);
      setMandatiResidui([]);
      const esito = await api.aiQuery<RigaCedolinoMeseAi>(nuovoMandato.lease, "Cedolini");
      if (!esito.consentito) throw new Error(esito.motivo ?? "Accesso negato dal Governor.");
      setEsitoCedolini(esito);
      setMandato({
        ...nuovoMandato,
        manifest: {
          ...nuovoMandato.manifest,
          lettureUsate: nuovoMandato.manifest.maxLetture - esito.lettureRimanenti,
        },
      });
    } catch (e) {
      setErroreControllo(e instanceof Error ? e.message : "Controllo non riuscito.");
    } finally {
      setControlloInCorso(false);
    }
  };

  const mostraTrasparenza = async () => {
    if (!mandato || controlloInCorso) return;
    setControlloInCorso(true);
    setErroreControllo(null);
    try {
      setTrasparenza(await api.aiCosaVede(mandato.manifest.grantId));
    } catch (e) {
      setErroreControllo(e instanceof Error ? e.message : "Registro non disponibile.");
    } finally {
      setControlloInCorso(false);
    }
  };

  const arresta = async () => {
    if (controlloInCorso) return;
    setControlloInCorso(true);
    setErroreControllo(null);
    try {
      await api.aiArresta();
      setMandato(null);
      setMandatiResidui([]);
      setPreflight(null);
      setAmbitoPreflight(null);
      setEsitoCedolini(null);
      setTrasparenza(null);
    } catch (e) {
      setErroreControllo(e instanceof Error ? e.message : "Arresto non riuscito.");
    } finally {
      setControlloInCorso(false);
    }
  };

  const chiudiSicuro = async () => {
    if (chiusuraInCorso.current) return;
    if (controlloInCorso) {
      setErroreControllo("Attendi la conclusione del controllo prima di chiudere Aurora.");
      return;
    }
    if (!mandato && mandatiResidui.length === 0) {
      onChiudi();
      return;
    }

    chiusuraInCorso.current = true;
    setControlloInCorso(true);
    setErroreControllo("Sto revocando il mandato prima di chiudere Aurora.");
    try {
      if (mandato)
        await api.aiRevocaGrant(mandato.manifest.grantId);
      else
        await api.aiArresta();
      setMandato(null);
      setMandatiResidui([]);
      onChiudi();
    } catch (e) {
      setErroreControllo(
        `Non chiudo Aurora perché il mandato non è stato revocato: ${e instanceof Error ? e.message : "errore"}. Usa Revoca mandato e riprova.`,
      );
    } finally {
      chiusuraInCorso.current = false;
      setControlloInCorso(false);
    }
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") void chiudiSicuro(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  return (
    <aside id="aurora-panel" className="aurora-panel glass-panel" role="complementary" aria-label="Aurora PaghePro">
      <header className="cp-mandato">
        <div className="cp-mandato-riga">
          <span className="cp-titolo"><Sparkles size={16} aria-hidden="true" /> Aurora</span>
          <span className={`cp-badge${mandato ? " attivo" : ""}`}>
            {mandato ? "mandato attivo" : stato?.attiva
              ? `${stato.provider} · ${stato.modello}`
              : "controllo deterministico"}
          </span>
          <button className="cp-chiudi" type="button" aria-label="Chiudi Aurora" disabled={controlloInCorso} onClick={() => void chiudiSicuro()}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
        <p className="cp-scope">
          Pagina: <b>{sezione}</b>{azienda ? <> · Azienda: <b>{azienda.ragioneSociale}</b></> : null}<br />
          La chat spiega soltanto. Il controllo cedolini legge un lotto massimo di 10 righe
          solo dopo il preflight e non modifica nulla.<br />
          {stato?.attiva ? (
            <>Le domande scritte qui vengono inviate al provider <b>{stato.provider}</b> per
            generare la risposta: non incollare dati che non vuoi condividere.</>
          ) : (
            <>Nessun testo lascia il server: i provider AI sono spenti e il controllo
            cedolini resta interamente dentro PaghePro.</>
          )}
        </p>
      </header>

      {contesto && (
        <section className="cp-contesto" aria-label="Testo selezionato per Aurora">
          <div className="cp-contesto-testa">
            <strong>Testo selezionato · {contesto.sezione}</strong>
            <button type="button" onClick={rimuoviContesto} aria-label="Rimuovi testo selezionato">
              <X size={15} aria-hidden="true" />
            </button>
          </div>
          <blockquote>{anteprimaContestoAurora(contesto, 320)}</blockquote>
          <small>
            {contesto.troncato ? "Testo ridotto a 4.000 caratteri. " : ""}
            Sarà allegato una volta sola alla prossima domanda, anche se usi un pulsante rapido.
          </small>
        </section>
      )}

      <div className="cp-thread" role="log" aria-live="polite" aria-relevant="additions text">
        <section className="cp-preflight" aria-label="Controllo governato dei cedolini">
          <span className="cp-preflight-tag">Controllo cedolini · lotto 10</span>
          {!azienda ? (
            <div className="cp-esito avviso">
              <strong>Scegli prima un’azienda</strong>
              <span>Aurora non seleziona mai automaticamente il primo cliente.</span>
            </div>
          ) : (
            <>
              <label className="cp-mese">
                <span>Mese da controllare</span>
                <input
                  type="month"
                  value={periodo}
                  disabled={Boolean(mandato) || controlloInCorso}
                  onChange={(e) => {
                    setPeriodo(e.target.value);
                    setPreflight(null);
                    setEsitoCedolini(null);
                    setTrasparenza(null);
                  }}
                />
              </label>
              {!preflightCorrente && !mandato && (
                <button className="btn secondary" type="button" disabled={controlloInCorso || mandatiResidui.length > 0} onClick={mostraPreflight}>
                  Mostra cosa controllerà
                </button>
              )}
            </>
          )}

          {preflightCorrente && !mandato && (
            <div className={`cp-esito ${preflightCorrente.consentito ? "ok" : "urgente"}`}>
              <strong>{preflightCorrente.consentito ? "Pronto per la tua conferma" : "Controllo non consentito"}</strong>
              <span>{preflightCorrente.descrizione ?? preflightCorrente.motivo}</span>
              {preflightCorrente.consentito && (
                <button className="btn primary" type="button" disabled={controlloInCorso} onClick={avviaControllo}>
                  Avvia controllo in sola lettura
                </button>
              )}
            </div>
          )}

          {mandato && (
            <div className="cp-esito ok">
              <strong>{mandato.manifest.periodo} · sola lettura</strong>
              <span>
                Quota: {mandato.manifest.lettureUsate}/{mandato.manifest.maxLetture} righe ·
                scade {new Date(mandato.manifest.scadeUtc).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit" })}
              </span>
              <div className="cp-mandato-azioni">
                <button className="cp-chip" type="button" disabled={controlloInCorso} onClick={mostraTrasparenza}>
                  Dati letti dal controllo
                </button>
                <button className="cp-chip stop" type="button" disabled={controlloInCorso} onClick={arresta}>
                  Revoca mandato
                </button>
              </div>
            </div>
          )}

          {!mandato && mandatiResidui.length > 0 && (
            <div className="cp-esito avviso">
              <strong>Mandato precedente ancora attivo</strong>
              <span>
                La lease non è salvata nello storage, ma il mandato resta attivo sul server. Revoca
                i {mandatiResidui.length} mandati residui prima di iniziare un nuovo controllo.
              </span>
              <button className="btn secondary" type="button" disabled={controlloInCorso} onClick={arresta}>
                Revoca mandati residui
              </button>
            </div>
          )}

          {conteggi && esitoCedolini && (
            <div className="cp-lotto">
              <strong>
                {esitoCedolini.righe.length} di {esitoCedolini.totale} rapporti controllati
              </strong>
              <span className="cp-lotto-riepilogo">
                {conteggi.cartellino_presente} cartellini presenti · {conteggi.da_compilare} da compilare · {conteggi.gia_elaborato} già elaborati · {conteggi.bloccato} bloccati · {conteggi.protetto} protetti
              </span>
              <ul>
                {esitoCedolini.righe.map((riga) => (
                  <li key={riga.rapportoId}>
                    <div><b>{riga.nominativo}</b><span className={`cp-stato ${riga.stato}`}>{riga.stato.replaceAll("_", " ")}</span></div>
                    <small>{riga.motivo}</small>
                  </li>
                ))}
              </ul>
              {esitoCedolini.totale > esitoCedolini.righe.length && (
                <small>
                  Questo incremento mostra i primi 10. I successivi richiedono il prossimo
                  checkpoint con cursore verificabile e non vengono letti ora.
                </small>
              )}
            </div>
          )}

          {trasparenza && (
            <div className="cp-trasparenza">
              <div className="cp-trasparenza-testa">
                <strong>Cosa ha letto il controllo</strong>
                <span>{trasparenza.manifest.risorse.join(", ")}</span>
              </div>
              <ul>
                {trasparenza.eventi.map((evento) => (
                  <li key={evento.sequenza}>
                    <span className={`cp-evento${evento.motivo ? " nega" : ""}`}>{evento.evento}</span>
                    <span className="cp-evento-dettaglio">{evento.dettaglio}{evento.motivo ? ` · ${evento.motivo}` : ""}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {erroreControllo && (
            <div className="cp-esito urgente"><strong>Controllo interrotto</strong><span>{erroreControllo}</span></div>
          )}
        </section>

        <div className="cp-msg">
          <p>
            Ciao! Seleziona una frase, un errore o una voce della pagina e aprimi: te la spiego
            in modo semplice. Se non capisco che cosa intendi, ti faccio una domanda.
          </p>
        </div>

        {stato && !stato.attiva && (
          <div className="cp-esito avviso">
            <strong>Assistente non ancora configurato</strong>
            <span>{stato.messaggio}</span>
          </div>
        )}

        {bolle.map((b) => (
          <div className={`cp-msg${b.chi === "utente" ? " mio" : ""}`} key={b.id}>
            <p>{b.testo}</p>
            {b.citazione && <blockquote className="cp-citazione">“{b.citazione}”</blockquote>}

          </div>
        ))}

        {inCorso && <div className="cp-msg cp-attesa"><p>Sto pensando…</p></div>}
        <div ref={fondo} />
      </div>

      <div className="cp-rapidi" aria-label="Domande rapide">
        {suggerimenti.map((s) => (
          <button key={s.etichetta} type="button" disabled={inCorso} onClick={() => invia(s.richiesta)}>
            {s.etichetta}
          </button>
        ))}
      </div>

      <form
        className="cp-comporre"
        onSubmit={(e) => { e.preventDefault(); void invia(bozza); }}
      >
        <textarea
          ref={campoMessaggio}
          value={bozza}
          onChange={(e) => setBozza(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              void invia(bozza);
            }
          }}
          placeholder="Scrivi ad Aurora… (Invio per inviare)"
          rows={2}
          aria-label="Messaggio per Aurora"
        />
        <button className="btn primary" type="submit" disabled={inCorso || !bozza.trim()} aria-label="Manda">
          <Send size={15} aria-hidden="true" />
        </button>
      </form>

      <footer className="cp-nota">
        <ShieldCheck size={14} aria-hidden="true" />
        <span>
          La chat invia al server messaggio, sezione, ID dell’azienda scelta e fino a 12 tue domande
          precedenti; il testo selezionato è monouso. Il controllo deterministico usa azienda e mese
          confermati e restituisce, per massimo 10 rapporti, ID tecnici, nominativo, stato e motivo.
          Questi dati del controllo non vanno a un modello o provider AI e non vengono modificati.
        </span>
      </footer>
    </aside>
  );
}
