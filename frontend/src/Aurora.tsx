import { useEffect, useRef, useState } from "react";
import { Check, CircleAlert, Send, ShieldCheck, Sparkles, X } from "lucide-react";
import {
  api,
  type Azienda,
  type AzionePropostaAi,
  type MessaggioChatAi,
  type StatoChatAi,
} from "./api";
import { scegliAziendaPreferita } from "./aziendaCorrente";

// Aurora di PaghePro — assistente a chat vera (modello DeepSeek o altro provider
// configurato sul server). Parla, legge l'archivio da sola con i suoi strumenti e,
// quando c'è da scrivere qualcosa, prepara l'operazione compilata: la conferma —
// sempre — resta all'utente, con l'elenco dei dati che mancano sotto gli occhi.

type SezioneAurora = "scadenzario" | "presenze" | "aziende" | "dipendenti";

const SEZIONI: SezioneAurora[] = ["scadenzario", "presenze", "aziende", "dipendenti"];

interface Bolla {
  id: number;
  chi: "utente" | "aurora";
  testo: string;
  azione?: AzionePropostaAi;
  azioneChiusa?: boolean;
  vaiA?: SezioneAurora;
}

const SUGGERIMENTI: { etichetta: string; richiesta: string; serveAzienda: boolean }[] = [
  { etichetta: "Cosa scade", richiesta: "Quali scadenze ho in arrivo? Dimmi le più urgenti.", serveAzienda: false },
  { etichetta: "Cosa manca all'azienda", richiesta: "Controlla l'anagrafica di questa azienda e dimmi cosa manca.", serveAzienda: true },
  { etichetta: "Nuova azienda", richiesta: "Voglio registrare una nuova azienda cliente: dimmi quali dati ti servono.", serveAzienda: false },
  { etichetta: "Dipendenti", richiesta: "Fammi il punto sui dipendenti di questa azienda.", serveAzienda: true },
];

function sezioneDi(azione: AzionePropostaAi): SezioneAurora | undefined {
  return SEZIONI.find((s) => s === azione.sezione);
}

export function AuroraPanel({
  azienda: aziendaDallaDashboard,
  onNavigate,
  onChiudi,
}: {
  azienda: Azienda | null;
  onNavigate: (sezione: SezioneAurora) => void;
  onChiudi: () => void;
}) {
  const [bolle, setBolle] = useState<Bolla[]>([]);
  const [storia, setStoria] = useState<MessaggioChatAi[]>([]);
  const [bozza, setBozza] = useState("");
  const [inCorso, setInCorso] = useState(false);
  const [stato, setStato] = useState<StatoChatAi | null>(null);
  // Se la dashboard non ha ancora un'azienda corrente (es. appena creata), il
  // pannello se la cerca da solo nell'archivio: lo scope resta sempre aggiornato.
  const [azienda, setAzienda] = useState<Azienda | null>(aziendaDallaDashboard);
  const prossimoId = useRef(1);
  const fondo = useRef<HTMLDivElement>(null);
  const storiaRef = useRef<MessaggioChatAi[]>([]);

  useEffect(() => { storiaRef.current = storia; }, [storia]);

  useEffect(() => {
    if (aziendaDallaDashboard) {
      setAzienda(aziendaDallaDashboard);
      return;
    }
    api.aziende()
      .then((elenco) => setAzienda(scegliAziendaPreferita(elenco)))
      .catch(() => { /* motore spento: lo scope resta "Il tuo studio" */ });
  }, [aziendaDallaDashboard]);

  useEffect(() => {
    api.aiChatStato().then(setStato).catch(() => setStato(null));
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onChiudi(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onChiudi]);

  useEffect(() => {
    fondo.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [bolle, inCorso]);

  const aggiungi = (bolla: Omit<Bolla, "id">) =>
    setBolle((prima) => [...prima, { ...bolla, id: prossimoId.current++ }]);

  const invia = async (testo: string) => {
    const messaggio = testo.trim();
    if (!messaggio || inCorso) return;
    aggiungi({ chi: "utente", testo: messaggio });
    setBozza("");
    setInCorso(true);
    try {
      const r = await api.aiChat(messaggio, storiaRef.current, azienda?.id ?? null);
      setStoria(r.storia);
      aggiungi({ chi: "aurora", testo: r.testo, azione: r.azione ?? undefined });
    } catch (e) {
      aggiungi({
        chi: "aurora",
        testo: `Non riesco a parlare con l'assistente: ${e instanceof Error ? e.message : "errore"}.`,
      });
    } finally {
      setInCorso(false);
    }
  };

  const conferma = async (bollaId: number, azione: AzionePropostaAi) => {
    if (inCorso) return;
    setInCorso(true);
    try {
      const r = await api.aiChatConferma(azione, storiaRef.current);
      setStoria(r.storia);
      setBolle((prima) => prima.map((b) => (b.id === bollaId ? { ...b, azioneChiusa: true } : b)));
      aggiungi({
        chi: "aurora",
        testo: r.messaggio,
        vaiA: r.riuscita ? sezioneDi(azione) : undefined,
      });
    } catch (e) {
      aggiungi({
        chi: "aurora",
        testo: `Non sono riuscita a eseguire l'operazione: ${e instanceof Error ? e.message : "errore"}.`,
      });
    } finally {
      setInCorso(false);
    }
  };

  const annulla = (bollaId: number) =>
    setBolle((prima) => prima.map((b) => (b.id === bollaId ? { ...b, azioneChiusa: true } : b)));

  return (
    <aside className="aurora-panel glass-panel" role="complementary" aria-label="Aurora PaghePro">
      <header className="cp-mandato">
        <div className="cp-mandato-riga">
          <span className="cp-titolo"><Sparkles size={16} aria-hidden="true" /> Aurora</span>
          <span className={`cp-badge${stato?.attiva ? " attivo" : ""}`}>
            {stato === null ? "…" : stato.attiva ? stato.modello : "da configurare"}
          </span>
          <button className="cp-chiudi" type="button" aria-label="Chiudi Aurora" onClick={onChiudi}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
        <p className="cp-scope">
          Azienda: <b>{azienda?.ragioneSociale ?? "nessuna scelta"}</b><br />
          Chiedi quello che ti serve: legge l'archivio da sola, scrive solo se confermi tu.
        </p>
      </header>

      <div className="cp-thread">
        <div className="cp-msg">
          <p>
            Ciao! Scrivimi come parleresti a un collega: «registra questa azienda», poi incolla
            i dati che hai. Preparo tutto compilato, ti dico cosa manca e aspetto il tuo «Conferma».
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

            {b.azione && !b.azioneChiusa && (
              <div className="cp-azione">
                <strong>{b.azione.titolo}</strong>

                {b.azione.campi.length > 0 && (
                  <dl className="cp-azione-campi">
                    {b.azione.campi.map((c) => (
                      <div key={c.etichetta}>
                        <dt>{c.etichetta}</dt>
                        <dd>{c.valore}</dd>
                      </div>
                    ))}
                  </dl>
                )}

                {b.azione.mancanti.length > 0 && (
                  <div className="cp-azione-mancanti">
                    <span className="cp-azione-tag">
                      <CircleAlert size={13} aria-hidden="true" /> Manca
                    </span>
                    <ul>
                      {b.azione.mancanti.map((m) => (
                        <li key={m.etichetta} className={m.bloccante ? "bloccante" : undefined}>
                          <b>{m.etichetta}</b> — {m.perche}
                          {m.bloccante ? " (necessario)" : ""}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                <div className="cp-azione-bottoni">
                  <button
                    className="btn primary"
                    type="button"
                    disabled={inCorso || !b.azione.puoProcedere}
                    title={b.azione.puoProcedere ? undefined : "Completa prima i dati necessari"}
                    onClick={() => conferma(b.id, b.azione!)}
                  >
                    <Check size={14} aria-hidden="true" /> Conferma ed esegui
                  </button>
                  <button className="btn secondary" type="button" disabled={inCorso} onClick={() => annulla(b.id)}>
                    Annulla
                  </button>
                </div>
              </div>
            )}

            {b.vaiA && (
              <button className="btn secondary cp-vai" type="button" onClick={() => onNavigate(b.vaiA!)}>
                Apri {b.vaiA} →
              </button>
            )}
          </div>
        ))}

        {inCorso && <div className="cp-msg cp-attesa"><p>Sto pensando…</p></div>}
        <div ref={fondo} />
      </div>

      <div className="cp-rapidi" aria-label="Domande rapide">
        {SUGGERIMENTI.filter((s) => !s.serveAzienda || azienda).map((s) => (
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
          value={bozza}
          onChange={(e) => setBozza(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              void invia(bozza);
            }
          }}
          placeholder="Scrivi o incolla i dati… (Invio manda, Maiusc+Invio va a capo)"
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
          Aurora vede solo i dati del tuo studio e non esegue nulla da sola: registrazioni e
          correzioni partono soltanto quando premi «Conferma».
        </span>
      </footer>
    </aside>
  );
}
