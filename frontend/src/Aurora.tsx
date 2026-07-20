import { useEffect, useRef, useState } from "react";
import {
  AlarmClock, Building2, CalendarDays, Eye, ShieldCheck, Sparkles, Square, Users, X,
} from "lucide-react";
import {
  api,
  type Azienda,
  type CosaVedeAi,
  type GrantAiEmesso,
  type RigaAziendaAi,
  type RigaCartellinoAi,
  type RigaDipendenteAi,
  type RigaScadenzaAi,
} from "./api";
import { scegliAziendaPreferita } from "./aziendaCorrente";

// Aurora di PaghePro — pannello a fianco dell'app (stile "estensione Claude in Chrome").
// OGGI esegue SOLO controlli automatici deterministici, ma OGNI lettura passa dal
// Governatore (G2): prima si concede un mandato a tempo con quota, poi ogni accesso è
// autorizzato, contato e auditato; Arresta revoca tutto all'istante. Nessuna modifica,
// nessun invio, nessuna AI esterna: il modello arriverà solo dopo l'isolamento G1.

type SezioneAurora = "scadenzario" | "presenze" | "aziende" | "dipendenti";

type Gravita = "ok" | "info" | "avviso" | "urgente";

interface Segnalazione {
  gravita: Gravita;
  titolo: string;
  dettaglio: string;
  sezione?: SezioneAurora;
  azione?: string;
}

interface Messaggio {
  id: number;
  testo: string;
  segnalazioni?: Segnalazione[];
}

const GRAVITA_ORDINE: Record<Gravita, number> = { urgente: 0, avviso: 1, info: 2, ok: 3 };

/** Il Governatore ha negato l'accesso: il mandato non copre (più) la lettura richiesta. */
class MandatoNegatoError extends Error {}

/** Legge una risorsa attraverso il query broker: permesso + righe minimizzate in un passo. */
type Interroga = <T>(risorsa: string) => Promise<{ righe: T[]; totale: number }>;

function meseCorrente(): { anno: number; mese: number; etichetta: string; periodo: string } {
  const oggi = new Date();
  const anno = oggi.getFullYear();
  const mese = oggi.getMonth() + 1;
  return {
    anno,
    mese,
    etichetta: oggi.toLocaleDateString("it-IT", { month: "long", year: "numeric" }),
    periodo: `${anno}-${String(mese).padStart(2, "0")}`,
  };
}

// --- Controlli deterministici (regole fisse del programma, non AI) ---
// Ognuno legge SOLO attraverso il query broker: righe minimizzate, filtrate dal mandato.

async function controllaScadenze(interroga: Interroga): Promise<Segnalazione[]> {
  const { righe: promemoria } = await interroga<RigaScadenzaAi>("Scadenzario");
  const urgenti = promemoria.filter(
    (p) => p.urgenza === "scaduto" || p.urgenza === "oggi" || p.urgenza === "in_scadenza",
  );
  if (urgenti.length === 0) {
    return [{ gravita: "ok", titolo: "Scadenzario in ordine", dettaglio: "Nessuna scadenza urgente (DURC, F24, promemoria)." }];
  }
  const scaduti = urgenti.filter((p) => p.giorniAllaScadenza <= 0);
  return [{
    gravita: scaduti.length > 0 ? "urgente" : "avviso",
    titolo: scaduti.length > 0
      ? `${scaduti.length} scadenz${scaduti.length === 1 ? "a" : "e"} già superat${scaduti.length === 1 ? "a" : "e"}`
      : `${urgenti.length} scadenz${urgenti.length === 1 ? "a" : "e"} in arrivo`,
    dettaglio: urgenti.slice(0, 3).map((p) =>
      `${p.titolo} (${p.giorniAllaScadenza < 0
        ? `scaduta da ${Math.abs(p.giorniAllaScadenza)} gg`
        : p.giorniAllaScadenza === 0 ? "scade oggi" : `tra ${p.giorniAllaScadenza} gg`})`,
    ).join(" · ") + (urgenti.length > 3 ? ` · e altre ${urgenti.length - 3}` : ""),
    sezione: "scadenzario",
    azione: "Apri Scadenzario",
  }];
}

async function controllaMese(azienda: Azienda, interroga: Interroga): Promise<Segnalazione[]> {
  const { etichetta } = meseCorrente();
  // Azienda e periodo li decide il MANDATO lato server, non questa pagina.
  const { righe, totale } = await interroga<RigaCartellinoAi>("Cartellini");
  const presenze = {
    totale,
    elaborati: righe.filter((r) => r.elaborato).length,
    righe,
  };
  const esiti: Segnalazione[] = [];
  if (presenze.totale === 0) {
    return [{
      gravita: "info",
      titolo: "Nessun dipendente in forza",
      dettaglio: `Per ${azienda.ragioneSociale} non ci sono rapporti attivi nel mese: nessun cedolino da fare.`,
      sezione: "dipendenti",
      azione: "Apri Dipendenti",
    }];
  }
  const daFare = presenze.totale - presenze.elaborati;
  if (daFare > 0) {
    esiti.push({
      gravita: "avviso",
      titolo: `${daFare} cedolin${daFare === 1 ? "o" : "i"} su ${presenze.totale} da elaborare (${etichetta})`,
      dettaglio: presenze.righe.filter((r) => !r.elaborato).slice(0, 4).map((r) => r.nominativo).join(", ")
        + (daFare > 4 ? ` e altri ${daFare - 4}` : "") + ".",
      sezione: "presenze",
      azione: "Apri Presenze",
    });
  } else {
    esiti.push({
      gravita: "ok",
      titolo: `Cedolini di ${etichetta} tutti elaborati`,
      dettaglio: `${presenze.elaborati} su ${presenze.totale} dipendenti hanno il cedolino del mese.`,
    });
  }
  // Il controllo del mockup, reso reale: malattia registrata senza numero evento
  // nell'anno — senza quel dato il motore rifiuta l'indennità INPS.
  const senzaEvento = presenze.righe.filter((r) => r.giorniMalattia > 0 && r.numeroEventoMalattiaAnno === 0);
  if (senzaEvento.length > 0) {
    esiti.push({
      gravita: "urgente",
      titolo: `Malattia senza n° evento: ${senzaEvento.map((r) => r.nominativo).join(", ")}`,
      dettaglio: "Il cartellino ha giorni di malattia ma manca il numero dell'evento nell'anno: completalo in Presenze, altrimenti l'indennità INPS non parte.",
      sezione: "presenze",
      azione: "Apri Presenze",
    });
  }
  return esiti;
}

async function controllaAnagrafica(interroga: Interroga): Promise<Segnalazione[]> {
  // Il broker restituisce solo flag di presenza: mai valori come l'IBAN.
  const { righe } = await interroga<RigaAziendaAi>("Aziende");
  const a = righe[0];
  if (!a) return [];
  const mancano: string[] = [];
  if (!a.matricolaInpsPresente) mancano.push("matricola INPS (serve per UniEmens)");
  if (!a.patInailPresente) mancano.push("P.A.T. INAIL (serve per l'autoliquidazione)");
  if (!a.codiceCatastalePresente) mancano.push("codice catastale del Comune (serve per le addizionali IRPEF)");
  if (!a.ibanPresente) mancano.push("IBAN aziendale (serve per F24/SEPA)");
  if (!a.recapitoFiscalePresente) mancano.push("PEC o codice destinatario SdI (serve per le fatture)");
  if (mancano.length === 0) {
    return [{ gravita: "ok", titolo: "Anagrafica azienda completa", dettaglio: "INPS, INAIL, Comune, IBAN e recapiti fiscali sono tutti presenti." }];
  }
  return [{
    gravita: "avviso",
    titolo: `Dati azienda incompleti (${mancano.length})`,
    dettaglio: `Manca: ${mancano.join("; ")}.`,
    sezione: "aziende",
    azione: "Apri Aziende",
  }];
}

async function controllaDipendenti(interroga: Interroga): Promise<Segnalazione[]> {
  const { righe: dipendenti } = await interroga<RigaDipendenteAi>("Dipendenti");
  if (dipendenti.length === 0) {
    return [{
      gravita: "info",
      titolo: "Nessun dipendente registrato",
      dettaglio: "Puoi aggiungerne uno a mano o caricare l'UNILAV: pensa lui a creare anagrafica e rapporto.",
      sezione: "dipendenti",
      azione: "Apri Dipendenti",
    }];
  }
  const incompleti = dipendenti.filter((d) => !d.livello || !d.codiceCcnl);
  if (incompleti.length > 0) {
    return [{
      gravita: "avviso",
      titolo: `${incompleti.length} dipendent${incompleti.length === 1 ? "e" : "i"} senza CCNL o livello`,
      dettaglio: incompleti.slice(0, 4).map((d) => d.nominativo).join(", ")
        + (incompleti.length > 4 ? ` e altri ${incompleti.length - 4}` : "")
        + ": senza inquadramento il cedolino non può usare le tabelle giuste.",
      sezione: "dipendenti",
      azione: "Apri Dipendenti",
    }];
  }
  return [{
    gravita: "ok",
    titolo: `${dipendenti.length} dipendent${dipendenti.length === 1 ? "e" : "i"} con inquadramento completo`,
    dettaglio: "Tutti hanno CCNL e livello: le tabelle retributive si applicano correttamente.",
  }];
}

// --- Pannello ---

export function AuroraPanel({
  azienda: aziendaDallaDashboard,
  onNavigate,
  onChiudi,
}: {
  azienda: Azienda | null;
  onNavigate: (sezione: SezioneAurora) => void;
  onChiudi: () => void;
}) {
  const [messaggi, setMessaggi] = useState<Messaggio[]>([]);
  const [inCorso, setInCorso] = useState(false);
  const [mandato, setMandato] = useState<GrantAiEmesso | null>(null);
  const [rimanenti, setRimanenti] = useState<number | null>(null);
  const [trasparenza, setTrasparenza] = useState<CosaVedeAi | null>(null);
  // Se la dashboard non ha ancora un'azienda corrente (es. appena creata), il
  // pannello se la cerca da solo nell'archivio: lo scope resta sempre aggiornato.
  const [azienda, setAzienda] = useState<Azienda | null>(aziendaDallaDashboard);
  const prossimoId = useRef(1);
  const fondo = useRef<HTMLDivElement>(null);
  const { etichetta, periodo } = meseCorrente();

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
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onChiudi(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onChiudi]);

  useEffect(() => {
    fondo.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messaggi, inCorso, trasparenza]);

  const aggiungi = (testo: string, segnalazioni?: Segnalazione[]) => {
    setMessaggi((prima) => [...prima, { id: prossimoId.current++, testo, segnalazioni }]);
  };

  // Il mandato serve dentro callback async avviati prima dell'aggiornamento di stato.
  const mandatoRef = useRef<GrantAiEmesso | null>(null);
  useEffect(() => { mandatoRef.current = mandato; }, [mandato]);

  /** Ogni lettura passa dal query broker: permesso, righe minimizzate e quota in un passo. */
  const interroga: Interroga = async <T,>(risorsa: string) => {
    if (!mandatoRef.current) throw new MandatoNegatoError("Nessun mandato attivo.");
    const esito = await api.aiQuery<T>(mandatoRef.current.lease, risorsa);
    if (!esito.consentito) {
      setMandato(null);
      setRimanenti(null);
      throw new MandatoNegatoError(esito.motivo ?? "Accesso negato dal Governatore.");
    }
    setRimanenti(esito.lettureRimanenti);
    return { righe: esito.righe, totale: esito.totale };
  };

  const esegui = async (nome: string, lavoro: () => Promise<Segnalazione[]>) => {
    if (inCorso) return;
    setInCorso(true);
    try {
      const esiti = await lavoro();
      esiti.sort((a, b) => GRAVITA_ORDINE[a.gravita] - GRAVITA_ORDINE[b.gravita]);
      const problemi = esiti.filter((s) => s.gravita === "urgente" || s.gravita === "avviso").length;
      aggiungi(
        problemi === 0
          ? `${nome}: tutto in ordine. Nessuna modifica fatta.`
          : `${nome}: ${problemi} segnalazion${problemi === 1 ? "e" : "i"}. Nessuna modifica fatta — decidere resta a te.`,
        esiti,
      );
    } catch (e) {
      if (e instanceof MandatoNegatoError) {
        aggiungi(`${nome}: fermato dal Governatore — ${e.message} Concedi un nuovo mandato per continuare.`);
      } else {
        aggiungi(`${nome}: non sono riuscito a leggere i dati (${e instanceof Error ? e.message : "errore"}). Il motore è acceso?`);
      }
    } finally {
      setInCorso(false);
    }
  };

  const controlliDelMandato = async (): Promise<Segnalazione[]> => {
    const esiti: Segnalazione[] = [];
    const tollera = (p: Promise<Segnalazione[]>, fallback: Segnalazione[]) =>
      p.catch((e) => { if (e instanceof MandatoNegatoError) throw e; return fallback; });

    esiti.push(...await tollera(controllaScadenze(interroga), [{
      gravita: "info", titolo: "Scadenzario non raggiungibile", dettaglio: "Riprova quando il motore è acceso.",
    }]));
    const a = azienda;
    if (a) {
      esiti.push(...await tollera(controllaAnagrafica(interroga), []));
      esiti.push(...await tollera(controllaMese(a, interroga), []));
      esiti.push(...await tollera(controllaDipendenti(interroga), []));
    } else {
      esiti.push({
        gravita: "info",
        titolo: "Nessuna azienda in archivio",
        dettaglio: "Crea la prima azienda (anche da visura camerale): poi controllo mese, dipendenti e anagrafica.",
        sezione: "aziende",
        azione: "Apri Aziende",
      });
    }
    return esiti;
  };

  /** Preflight → mandato dal Governatore → controlli. Un solo clic, come da piano. */
  const avviaMandatoEControlla = async () => {
    if (inCorso) return;
    setInCorso(true);
    try {
      const grant = await api.aiEmettiGrant(azienda
        ? {
            purposeCode: "CONTROLLO_ANOMALIE",
            modalita: "Guidata",
            scopeLevel: "S2",
            aziendaId: azienda.id,
            periodo,
            risorse: ["Scadenzario", "Aziende", "Dipendenti", "Rapporti", "Cartellini", "Cedolini"],
            operazioni: ["read", "search"],
          }
        : {
            purposeCode: "RIEPILOGO_SCADENZE",
            modalita: "Guidata",
            scopeLevel: "S0",
            risorse: ["Scadenzario"],
            operazioni: ["read", "search"],
          });
      setMandato(grant);
      mandatoRef.current = grant;
      setRimanenti(grant.manifest.maxLetture - grant.manifest.lettureUsate);
      setTrasparenza(null);
      aggiungi(`Mandato concesso. ${grant.manifest.descrizione}`);
    } catch (e) {
      aggiungi(`Il Governatore non ha concesso il mandato: ${e instanceof Error ? e.message : "errore"}.`);
      setInCorso(false);
      return;
    }
    setInCorso(false);
    await esegui("Controllo completo", controlliDelMandato);
  };

  const arresta = async () => {
    const attivo = mandatoRef.current;
    if (!attivo) return;
    try {
      await api.aiRevocaGrant(attivo.manifest.grantId);
    } catch { /* già scaduto o motore spento: localmente si arresta comunque */ }
    setMandato(null);
    setRimanenti(null);
    setTrasparenza(null);
    aggiungi("Mandato revocato: non leggo più nulla finché non me ne concedi uno nuovo.");
  };

  const mostraCosaVedo = async () => {
    const attivo = mandatoRef.current;
    if (!attivo) return;
    try {
      setTrasparenza(await api.aiCosaVede(attivo.manifest.grantId));
    } catch (e) {
      aggiungi(`Non riesco a leggere il registro del mandato (${e instanceof Error ? e.message : "errore"}).`);
    }
  };

  const scadeAlle = mandato
    ? new Date(mandato.manifest.scadeUtc).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit" })
    : null;

  return (
    <aside className="aurora-panel glass-panel" role="complementary" aria-label="Aurora PaghePro">
      <header className="cp-mandato">
        <div className="cp-mandato-riga">
          <span className="cp-titolo"><Sparkles size={16} aria-hidden="true" /> Aurora</span>
          <span className={`cp-badge${mandato ? " attivo" : ""}`}>
            {mandato ? "mandato attivo" : "in attesa di mandato"}
          </span>
          <button className="cp-chiudi" type="button" aria-label="Chiudi Aurora" onClick={onChiudi}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
        <p className="cp-scope">
          Scope: <b>{azienda?.ragioneSociale ?? "Il tuo studio"}</b> · {etichetta}<br />
          {mandato
            ? <>Quota letture: <b>{rimanenti ?? "—"}</b> di {mandato.manifest.maxLetture} · Scade alle <b>{scadeAlle}</b></>
            : <>Può: <b>leggere e segnalare</b> · Non modifica niente senza di te</>}
        </p>
        {mandato && (
          <div className="cp-mandato-azioni">
            <button className="cp-chip" type="button" onClick={mostraCosaVedo}>
              <Eye size={13} aria-hidden="true" /> Cosa vede l'AI
            </button>
            <button className="cp-chip stop" type="button" onClick={arresta}>
              <Square size={12} aria-hidden="true" /> Arresta
            </button>
          </div>
        )}
      </header>

      <div className="cp-thread">
        <div className="cp-msg">
          <p>
            Ciao! Leggo i tuoi dati con le regole fisse del programma, ti segnalo i problemi
            e ti porto sulla pagina giusta per sistemarli. Ogni mia lettura passa dal
            Governatore: mandato a tempo, quota e revoca immediata.
          </p>
        </div>

        {!mandato && (
          <div className="cp-preflight">
            <span className="cp-preflight-tag">Prima di iniziare</span>
            <dl>
              <dt>Leggerò:</dt>
              <dd>
                {azienda
                  ? `scadenzario, anagrafica, dipendenti e presenze di ${azienda.ragioneSociale} (${etichetta})`
                  : "solo lo scadenzario dello studio"}
              </dd>
              <dt>Farò:</dt><dd>controlli e segnalazioni</dd>
              <dt>Non farò:</dt><dd>modifiche, invii o pagamenti</dd>
            </dl>
            <button className="btn primary" type="button" disabled={inCorso} onClick={avviaMandatoEControlla}>
              {inCorso ? "Sto controllando…" : "Concedi il mandato e controlla"}
            </button>
          </div>
        )}

        {messaggi.map((m) => (
          <div className="cp-msg" key={m.id}>
            <p>{m.testo}</p>
            {m.segnalazioni?.map((s, i) => (
              <div className={`cp-esito ${s.gravita}`} key={i}>
                <strong>{s.titolo}</strong>
                <span>{s.dettaglio}</span>
                {s.sezione && s.azione && (
                  <button className="btn secondary cp-vai" type="button" onClick={() => onNavigate(s.sezione!)}>
                    {s.azione} →
                  </button>
                )}
              </div>
            ))}
          </div>
        ))}

        {trasparenza && (
          <div className="cp-trasparenza">
            <div className="cp-trasparenza-testa">
              <strong>Cosa vede l'AI — registro del mandato</strong>
              <button className="cp-chiudi" type="button" aria-label="Chiudi registro" onClick={() => setTrasparenza(null)}>
                <X size={14} aria-hidden="true" />
              </button>
            </div>
            <ul>
              {trasparenza.eventi.map((e) => (
                <li key={e.sequenza}>
                  <span className={`cp-evento ${e.evento === "Diniego" ? "nega" : ""}`}>{e.evento}</span>
                  <span className="cp-evento-dettaglio">{e.dettaglio}{e.motivo ? ` — ${e.motivo}` : ""}</span>
                  <span className="cp-evento-quando">
                    {new Date(e.quandoUtc).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit", second: "2-digit" })}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
        <div ref={fondo} />
      </div>

      <div className="cp-rapidi" aria-label="Controlli rapidi">
        <button
          type="button"
          disabled={inCorso || !mandato}
          title={mandato ? undefined : "Prima concedi il mandato"}
          onClick={() => esegui("Scadenze", () => controllaScadenze(interroga))}
        >
          <AlarmClock size={14} aria-hidden="true" /> Scadenze
        </button>
        <button
          type="button"
          disabled={inCorso || !mandato || !azienda}
          title={mandato ? undefined : "Prima concedi il mandato"}
          onClick={() => azienda && esegui(`Cedolini di ${etichetta}`, () => controllaMese(azienda, interroga))}
        >
          <CalendarDays size={14} aria-hidden="true" /> Mese paghe
        </button>
        <button
          type="button"
          disabled={inCorso || !mandato || !azienda}
          title={mandato ? undefined : "Prima concedi il mandato"}
          onClick={() => azienda && esegui("Dati azienda", () => controllaAnagrafica(interroga))}
        >
          <Building2 size={14} aria-hidden="true" /> Dati azienda
        </button>
        <button
          type="button"
          disabled={inCorso || !mandato || !azienda}
          title={mandato ? undefined : "Prima concedi il mandato"}
          onClick={() => azienda && esegui("Dipendenti", () => controllaDipendenti(interroga))}
        >
          <Users size={14} aria-hidden="true" /> Dipendenti
        </button>
      </div>

      <footer className="cp-nota">
        <ShieldCheck size={14} aria-hidden="true" />
        <span>
          Niente AI esterna: controlli fissi in sola lettura, governati da un mandato a tempo
          con quota e audit. La chat con l'AI arriverà dopo i cancelli di sicurezza (G0–G3).
        </span>
      </footer>
    </aside>
  );
}
