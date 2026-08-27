import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlarmClock,
  Bell,
  Building2,
  Check,
  ChevronDown,
  FileText,
  FolderOpen,
  HelpCircle,
  Landmark,
  LayoutDashboard,
  LogOut,
  Megaphone,
  Plus,
  ReceiptText,
  Search,
  Settings,
  Sparkles,
  Users,
  X,
  type LucideIcon,
} from "lucide-react";
import { api, type Azienda, type Promemoria } from "../api";
import { eAccountOspite, useAuth } from "../auth";
import { getRuolo, getToken } from "../api";
import {
  AZIENDA_CORRENTE_CAMBIATA,
  chiaveStorageTenant,
  leggiAziendaCorrenteId,
  leggiUltimaAziendaId,
  salvaAziendaCorrenteId,
} from "../aziendaCorrente";
import {
  aggiornaStrumentiRecenti,
  sezioneRichiedeAzienda,
  type SectionKey,
  type StrumentoRecente,
} from "../homeStudio";
import {
  MONDI,
  mondoDiSezione,
  cercaVoci,
  vociPerRuolo,
  type ChiaveMondo,
  type Mondo,
} from "../navigazione";
import {
  creaContestoAurora,
  leggiTestoSelezionatoAurora,
  type ContestoAurora,
} from "../auroraContext";

// Email dell'utente loggato, letta dal token (per mostrare il vero account, non un nome finto).
function emailDalToken(): string {
  try {
    const t = getToken();
    if (!t) return "";
    const payload = JSON.parse(atob(t.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    return (payload.email as string) ?? "";
  } catch {
    return "";
  }
}

// Come si presenta l'account nella barra in alto. Lo spazio ospite ha un'email tecnica
// (demo-<codice>@paghe.local) che non ha senso mostrare: lì si scrive semplicemente "Ospite".
function etichettaAccount(): { iniziali: string; nome: string; dettaglio: string } {
  const email = emailDalToken();
  if (!email) return { iniziali: "AC", nome: "Account", dettaglio: "—" };
  if (eAccountOspite(email)) return { iniziali: "OS", nome: "Ospite", dettaglio: "Senza registrazione" };
  return { iniziali: email.slice(0, 2).toUpperCase(), nome: email.split("@")[0], dettaglio: email };
}

function inizialiAzienda(nome: string): string {
  const parole = nome.trim().split(/\s+/).filter(Boolean);
  if (parole.length === 0) return "AZ";
  return parole.slice(0, 2).map((p) => p[0]).join("").toUpperCase();
}
import { Aziende } from "./Aziende";
import { Cedolino } from "./Cedolino";
import { Dipendente } from "./Dipendente";
import { Dipendenti } from "./Dipendenti";
import { CertificazioneUnica } from "./CertificazioneUnica";
import { Lipe } from "./Lipe";
import { Modello770 } from "./Modello770";
import { RedditiPf } from "./RedditiPf";
import { Irap } from "./Irap";
import { Ires } from "./Ires";
import { Imu } from "./Imu";
import { Intrastat } from "./Intrastat";
import { IvaAnnuale } from "./IvaAnnuale";
import { Presenze } from "./Presenze";
import { Cedolini } from "./Cedolini";
import { CedolinoAgricolo } from "./CedolinoAgricolo";
import { Elaborazione } from "./Elaborazione";
import { Fatture } from "./Fatture";
import { Firma } from "./Firma";
import { Pagamenti } from "./Pagamenti";
import { Bilancio } from "./Bilancio";
import { FineAnno } from "./FineAnno";
import { Scadenzario, DURC_ONLINE_URL } from "./Scadenzario";
import { Pratiche } from "./Pratiche";
import { Moduli } from "./Moduli";
import { CollegaProvider } from "./CollegaProvider";
import { CaricaUnilav } from "./CaricaUnilav";
import { Studi } from "./Studi";
import { RegistroAttivita } from "./RegistroAttivita";
import { Retention } from "./Retention";
import { Posta } from "./Posta";
import { aiutoPerSezione } from "../guida";
import { AuroraPanel } from "../Aurora";
import { apriLinkEsterno } from "../mobile/piattaforma";


const sectionTitle: Record<SectionKey, string> = {
  dashboard: "Home studio",
  aziende: "Aziende",
  azienda: "Postazione azienda",
  pratiche: "Pratiche ComUnica",
  moduli: "Moduli ufficiali — AA7/10",
  dipendenti: "Dipendenti",
  dipendente: "Scheda dipendente",
  presenze: "Cartellino presenze",
  cedolini: "Cedolini",
  agricoltura: "Anteprima busta agricola",
  calcolo: "Calcolo cedolino",
  adempimenti: "Adempimenti",
  scadenzario: "Scadenzario",
  posta: "Posta",
  fatture: "Fatturazione",
  pagamenti: "Pagamenti clienti",
  firma: "Firma digitale",
  contabilita: "Contabilità",
  lipe: "LIPE / IVA",
  "iva-annuale": "Dichiarazione IVA annuale",
  bilancio: "Bilancio d'esercizio",
  documenti: "Documenti",
  elaborazioni: "Elaborazione cedolini",
  f24: "Modello F24",
  "fine-anno": "Fine anno — conguagli, TFR e INAIL",
  cu: "Certificazione Unica",
  mod770: "Modello 770",
  "redditi-pf": "Redditi PF / 730",
  irap: "IRAP",
  ires: "IRES — Redditi società",
  imu: "IMU — immobili",
  intrastat: "Intrastat — cessioni intra-UE",
  impostazioni: "Impostazioni",
  "collega-sdi": "Collega Fatturazione Elettronica",
  utenti: "Utenti e permessi",
  audit: "Registro attività",
  retention: "Conservazione dati (GDPR)",
  studi: "Studi — super-admin",
};

const CHIAVE_STRUMENTI_RECENTI = "paghepro:strumenti-recenti";

function leggiStrumentiRecenti(): StrumentoRecente[] {
  try {
    const valore = JSON.parse(localStorage.getItem(chiaveStorageTenant(CHIAVE_STRUMENTI_RECENTI)) ?? "[]") as unknown;
    if (!Array.isArray(valore)) return [];
    return valore.filter((voce): voce is StrumentoRecente => {
      if (!voce || typeof voce !== "object") return false;
      const v = voce as Partial<StrumentoRecente>;
      return typeof v.sezione === "string"
        && Object.hasOwn(sectionTitle, v.sezione)
        && typeof v.aziendaId === "string"
        && typeof v.usatoAlle === "number";
    }).slice(0, 4);
  } catch {
    return [];
  }
}

function salvaStrumentiRecenti(voci: StrumentoRecente[]): void {
  try {
    localStorage.setItem(chiaveStorageTenant(CHIAVE_STRUMENTI_RECENTI), JSON.stringify(voci));
  } catch {
    /* storage non disponibile: le scorciatoie restano facoltative */
  }
}

function sezioneDaHash(): SectionKey {
  const candidata = decodeURIComponent(window.location.hash.slice(1)).trim().toLowerCase();
  if (candidata === "audit" && !["admin", "superadmin"].includes(getRuolo() ?? ""))
    return "dashboard";
  if (candidata === "retention" && getRuolo() !== "admin")
    return "dashboard";
  if (candidata === "studi" && getRuolo() !== "superadmin")
    return "dashboard";
  return candidata && Object.hasOwn(sectionTitle, candidata)
    ? candidata as SectionKey
    : "dashboard";
}

export function Dashboard() {
  const { esci } = useAuth();
  const [active, setActive] = useState<SectionKey>(sezioneDaHash);
  const [rapportoSel, setRapportoSel] = useState<string | null>(null);
  const [aiutoAperto, setAiutoAperto] = useState(false);
  const [auroraAperta, setAuroraAperto] = useState(false);
  const [contestoAurora, setContestoAurora] = useState<ContestoAurora | null>(null);
  const [tendinaAperta, setTendinaAperta] = useState<ChiaveMondo | "azienda" | null>(null);
  const [commandAperta, setCommandAperta] = useState(false);
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [aziendaCorrente, setAziendaCorrente] = useState<Azienda | null>(null);
  const [aziendeInCaricamento, setAziendeInCaricamento] = useState(true);
  const [erroreAziende, setErroreAziende] = useState<string | null>(null);
  const [sezioneDopoScelta, setSezioneDopoScelta] = useState<SectionKey | null>(null);
  // Azienda appena registrata dalla chat di Aurora: la pagina Aziende ne apre la scheda
  // con i campi mancanti evidenziati in rosso, poi azzera la richiesta.
  const [aziendaDaCompletareId, setAziendaDaCompletareId] = useState<string | null>(null);
  const [strumentiRecenti, setStrumentiRecenti] = useState<StrumentoRecente[]>(leggiStrumentiRecenti);
  const aziendeRef = useRef<Azienda[]>([]);
  const contestoCatturato = useRef<ContestoAurora | null>(null);
  const triggerAurora = useRef<HTMLElement | null>(null);
  const auroraFab = useRef<HTMLButtonElement>(null);
  const apriDipendente = (rapportoId: string) => {
    setRapportoSel(rapportoId);
    setActive("dipendente");
    window.location.hash = "dipendente";
  };

  useEffect(() => {
    let vivo = true;
    let primaCarica = true;
    async function caricaAziendaCorrente() {
      if (primaCarica) setAziendeInCaricamento(true);
      try {
        const elenco = await api.aziende();
        if (!vivo) return;
        aziendeRef.current = elenco;
        setAziende(elenco);
        setErroreAziende(null);
        const idCorrente = leggiAziendaCorrenteId();
        const scelta = idCorrente ? elenco.find((a) => a.id === idCorrente) ?? null : null;
        setAziendaCorrente(scelta);
      } catch (e) {
        if (!vivo) return;
        aziendeRef.current = [];
        setAziende([]);
        setAziendaCorrente(null);
        setErroreAziende(e instanceof Error ? e.message : "Non riesco a caricare le aziende.");
      } finally {
        if (vivo) {
          setAziendeInCaricamento(false);
          primaCarica = false;
        }
      }
    }

    const aggiorna = (evento: Event) => {
      const id = (evento as CustomEvent<{ id?: unknown }>).detail?.id;
      if (typeof id === "string") {
        const scelta = aziendeRef.current.find((azienda) => azienda.id === id);
        // L'evento è sincrono: aggiorna subito la shell prima che un modulo
        // navighi, evitando di mostrare o registrare il cliente precedente.
        if (scelta) setAziendaCorrente(scelta);
      }
      void caricaAziendaCorrente();
    };
    void caricaAziendaCorrente();
    window.addEventListener(AZIENDA_CORRENTE_CAMBIATA, aggiorna);
    window.addEventListener("storage", aggiorna);
    return () => {
      vivo = false;
      window.removeEventListener(AZIENDA_CORRENTE_CAMBIATA, aggiorna);
      window.removeEventListener("storage", aggiorna);
    };
  }, []);

  // Promemoria reali (DURC, F24…): il GET rigenera anche le scadenze lato server,
  // quindi l'avviso compare da solo appena un DURC entra nei 30 giorni di preavviso.
  const [promemoria, setPromemoria] = useState<Promemoria[]>([]);
  const [promemoriaInCaricamento, setPromemoriaInCaricamento] = useState(true);
  const [errorePromemoria, setErrorePromemoria] = useState<string | null>(null);
  const [avvisoDurcNascosto, setAvvisoDurcNascosto] = useState(
    () => sessionStorage.getItem("avvisoDurcNascosto") === "1",
  );
  useEffect(() => {
    let vivo = true;
    setPromemoriaInCaricamento(true);
    setErrorePromemoria(null);
    api.scadenzario()
      .then((voci) => {
        if (!vivo) return;
        setPromemoria(voci);
      })
      .catch(() => {
        if (vivo) setErrorePromemoria("Scadenze non disponibili: riprova dallo scadenzario.");
      })
      .finally(() => {
        if (vivo) setPromemoriaInCaricamento(false);
      });
    return () => { vivo = false; };
  }, [active]);
  const urgenti = promemoria.filter(
    (p) => p.urgenza === "scaduto" || p.urgenza === "oggi" || p.urgenza === "in_scadenza",
  );
  const avvisiDurc = urgenti.filter((p) => p.tipo === "Durc");
  const nascondiAvvisoDurc = () => {
    sessionStorage.setItem("avvisoDurcNascosto", "1");
    setAvvisoDurcNascosto(true);
  };

  const navActive = active;

  const catturaSelezionePerAurora = () => {
    contestoCatturato.current = creaContestoAurora(
      leggiTestoSelezionatoAurora(),
      sectionTitle[active],
    );
  };

  const chiudiAurora = () => {
    const trigger = triggerAurora.current;
    setAuroraAperto(false);
    setContestoAurora(null);
    contestoCatturato.current = null;
    window.setTimeout(() => {
      (trigger?.isConnected ? trigger : auroraFab.current)?.focus();
      triggerAurora.current = null;
    }, 0);
  };

  const commutaAurora = (trigger?: HTMLElement) => {
    if (auroraAperta) {
      // La chiusura deve passare dal pannello: lì un eventuale mandato viene
      // revocato prima dello smontaggio. Il toggle della topbar si limita quindi
      // a riportare il focus su Aurora quando è già aperta.
      document.querySelector<HTMLButtonElement>("#aurora-panel .au-icona")?.focus();
      return;
    }

    if (trigger)
      triggerAurora.current = trigger;
    else if (document.activeElement instanceof HTMLElement)
      triggerAurora.current = document.activeElement;
    const selezionato = contestoCatturato.current ?? creaContestoAurora(
      leggiTestoSelezionatoAurora(),
      sectionTitle[active],
    );
    setContestoAurora(selezionato);
    setAuroraAperto(true);
    contestoCatturato.current = null;
  };

  const scegliAzienda = (azienda: Azienda) => {
    salvaAziendaCorrenteId(azienda.id);
    setAziendaCorrente(azienda);
    setTendinaAperta(null);
    // Se stavamo già su una schermata che aspettava il cliente, quella schermata
    // si monta da sola: nessuna navigazione, nessun clic in più.
  };

  const vaiA = (key: SectionKey) => {
    // Il gate resta: senza cliente scelto la schermata non viene montata. Cambia
    // il rimedio — la scelta compare al posto della schermata, senza rimbalzare
    // alla Home e senza far perdere la destinazione.
    if (key === "dashboard") setSezioneDopoScelta(null);
    setActive(key);
    if (window.location.hash !== `#${key}`) window.location.hash = key;
    setTendinaAperta(null);
  };

  const creaAzienda = () => {
    setTendinaAperta(null);
    setActive("aziende");
    if (window.location.hash !== "#aziende") window.location.hash = "aziende";
  };

  const riprendiLavoro = (voce: StrumentoRecente) => {
    const azienda = aziende.find((a) => a.id === voce.aziendaId);
    if (!azienda) return;
    setSezioneDopoScelta(null);
    scegliAzienda(azienda);
    setActive(voce.sezione);
    if (window.location.hash !== `#${voce.sezione}`) window.location.hash = voce.sezione;
  };

  useEffect(() => {
    const aggiornaDaHash = () => setActive(sezioneDaHash());
    window.addEventListener("hashchange", aggiornaDaHash);
    return () => window.removeEventListener("hashchange", aggiornaDaHash);
  }, []);

  useEffect(() => {
    if (!aziendaCorrente || !sezioneDopoScelta) return;
    const destinazione = sezioneDopoScelta;
    setSezioneDopoScelta(null);
    setActive(destinazione);
    if (window.location.hash !== `#${destinazione}`) window.location.hash = destinazione;
  }, [aziendaCorrente, sezioneDopoScelta]);

  useEffect(() => {
    if (!aziendaCorrente || !sezioneRichiedeAzienda(active) || active === "dipendente" || active === "calcolo") return;
    setStrumentiRecenti((correnti) => {
      const nuove = aggiornaStrumentiRecenti(
        correnti,
        active,
        aziendaCorrente.id,
        new Set(aziende.map((azienda) => azienda.id)),
      );
      salvaStrumentiRecenti(nuove);
      return nuove;
    });
  }, [active, aziendaCorrente, aziende]);

  useEffect(() => {
    // Il testo selezionato non attraversa schermate diverse e non viene mai persistito.
    setContestoAurora(null);
    contestoCatturato.current = null;
  }, [active]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setCommandAperta(true);
      }
      if (e.key === "Escape") {
        setTendinaAperta(null);
        setCommandAperta(false);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const ruolo = getRuolo();

  return (
    <div className={`app-shell ${auroraAperta ? "con-aurora" : ""}`}>
      <BarraAlta
        active={navActive}
        ruolo={ruolo}
        aziende={aziende}
        aziendaCorrente={aziendaCorrente}
        aperto={tendinaAperta}
        onApri={setTendinaAperta}
        onNavigate={vaiA}
        onScegliAzienda={scegliAzienda}
        onCreaAzienda={creaAzienda}
        onCerca={() => setCommandAperta(true)}
        onAiuto={() => setAiutoAperto(true)}
        onAurora={commutaAurora}
        onAuroraPointerDown={catturaSelezionePerAurora}
        auroraAperta={auroraAperta}
        notifiche={urgenti.length}
        onNotifiche={() => vaiA("scadenzario")}
        onEsci={esci}
      />

      <div className="workspace">
        {avvisiDurc.length > 0 && !avvisoDurcNascosto && (
          <AvvisoDurc
            avvisi={avvisiDurc}
            onApriScadenzario={() => vaiA("scadenzario")}
            onChiudi={nascondiAvvisoDurc}
          />
        )}

        <main className="content-area" data-aurora-selectable>
          {aziendeInCaricamento ? (
            <section className="glass-card home-loading" aria-live="polite">Caricamento aziende…</section>
          ) : !aziendaCorrente && sezioneRichiedeAzienda(active) ? (
            <SceltaClienteRichiesta
              sezione={sectionTitle[active]}
              aziende={aziende}
              erroreAziende={erroreAziende}
              onScegli={scegliAzienda}
              onCrea={creaAzienda}
            />
          ) : active === "dashboard" ? (
            <DashboardHome
              onNavigate={vaiA}
              aziendaCorrente={aziendaCorrente}
              onScegliCliente={() => setTendinaAperta("azienda")}
              onCreaAzienda={creaAzienda}
              promemoria={promemoria}
              promemoriaInCaricamento={promemoriaInCaricamento}
              errorePromemoria={errorePromemoria}
              strumentiRecenti={strumentiRecenti}
              aziende={aziende}
              onRiprendi={riprendiLavoro}
            />
          ) : (
            <ModuleScreen
              active={active}
              onNavigate={vaiA}
              rapportoSel={rapportoSel}
              onApriDipendente={apriDipendente}
              aziendaDaCompletareId={aziendaDaCompletareId}
              onAziendaDaCompletareGestita={() => setAziendaDaCompletareId(null)}
            />
          )}
        </main>
      </div>

      {auroraAperta && (
        <AuroraPanel
          azienda={aziendaCorrente}
          sezione={sectionTitle[active]}
          contesto={contestoAurora}
          onRimuoviContesto={() => setContestoAurora(null)}
          onChiudi={chiudiAurora}
          onAziendaCreata={(id) => {
            setAziendaDaCompletareId(id);
            vaiA("aziende");
          }}
          onApriDipendente={apriDipendente}
        />
      )}

      {!auroraAperta && (
        <button
          ref={auroraFab}
          className="aurora-fab"
          type="button"
          aria-label="Apri Aurora; se hai selezionato un testo, chiedi una spiegazione"
          aria-controls="aurora-panel"
          aria-expanded="false"
          title="Aurora — seleziona un testo e premi qui per fartelo spiegare"
          onPointerDown={catturaSelezionePerAurora}
          onClick={(evento) => commutaAurora(evento.currentTarget)}
        >
          <Sparkles size={21} aria-hidden="true" />
          <span>Aurora</span>
        </button>
      )}

      {aiutoAperto && (
        <AiutoPanel sezione={navActive} onChiudi={() => setAiutoAperto(false)} />
      )}

      {commandAperta && (
        <CommandPalette onClose={() => setCommandAperta(false)} onNavigate={vaiA} />
      )}

      <BarraMobile
        active={navActive}
        aperto={tendinaAperta}
        onApri={setTendinaAperta}
        onHome={() => vaiA("dashboard")}
      />
    </div>
  );
}

// ============================================================
// GUSCIO — una sola navigazione: cinque mondi nella barra alta,
// l'azienda sempre raggiungibile, nessun pannello da aprire prima.
// ============================================================

/** Menu a tendina di un mondo: si apre sotto la barra, si chiude con Esc o clic fuori. */
function TendinaMondo({
  mondo,
  ruolo,
  active,
  onNavigate,
  onChiudi,
}: {
  mondo: Mondo;
  ruolo: string | null;
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
  onChiudi: () => void;
}) {
  const voci = vociPerRuolo(mondo, ruolo);
  return (
    <div className="tendina-mondo" role="menu" aria-label={mondo.titolo}>
      {voci.map((voce) => (
        <button
          key={voce.key}
          className={`tendina-voce ${active === voce.key ? "attiva" : ""}`}
          type="button"
          role="menuitem"
          onClick={() => { onNavigate(voce.key); onChiudi(); }}
        >
          <strong>{voce.label}</strong>
          <small>{voce.detail}</small>
        </button>
      ))}
    </div>
  );
}

/** Scelta del cliente senza lasciare la pagina: ricerca e primi risultati. */
function TendinaAzienda({
  aziende,
  aziendaCorrente,
  onScegli,
  onCrea,
  onChiudi,
}: {
  aziende: Azienda[];
  aziendaCorrente: Azienda | null;
  onScegli: (azienda: Azienda) => void;
  onCrea: () => void;
  onChiudi: () => void;
}) {
  const [ricerca, setRicerca] = useState("");
  const ultimaAziendaId = leggiUltimaAziendaId();
  const filtrate = useMemo(() => {
    const testo = ricerca.trim().toLocaleLowerCase("it");
    const base = testo
      ? aziende.filter((a) => [a.ragioneSociale, a.partitaIva, a.codiceFiscale, a.comune, a.provincia]
        .filter(Boolean)
        .some((valore) => String(valore).toLocaleLowerCase("it").includes(testo)))
      : aziende;
    return [...base].sort((a, b) => {
      if (a.id === aziendaCorrente?.id) return -1;
      if (b.id === aziendaCorrente?.id) return 1;
      if (a.id === ultimaAziendaId) return -1;
      if (b.id === ultimaAziendaId) return 1;
      return a.ragioneSociale.localeCompare(b.ragioneSociale, "it");
    });
  }, [aziende, aziendaCorrente?.id, ricerca, ultimaAziendaId]);

  return (
    <div className="tendina-azienda" role="dialog" aria-label="Scegli il cliente">
      <label className="tendina-cerca" htmlFor="cerca-azienda-barra">
        <Search size={16} aria-hidden="true" />
        <input
          id="cerca-azienda-barra"
          autoFocus
          value={ricerca}
          onChange={(e) => setRicerca(e.target.value)}
          placeholder="Cerca per nome, P.IVA, codice fiscale o comune"
          autoComplete="off"
          aria-label="Cerca azienda cliente"
        />
      </label>

      <div className="tendina-elenco">
        {aziende.length === 0 ? (
          <p className="tendina-vuoto">Nessun cliente registrato. Nessuna azienda viene creata da sola.</p>
        ) : filtrate.length === 0 ? (
          <p className="tendina-vuoto">Nessuna azienda trovata con questo testo.</p>
        ) : (
          filtrate.slice(0, 8).map((azienda) => {
            const scelta = azienda.id === aziendaCorrente?.id;
            return (
              <button
                key={azienda.id}
                className={`tendina-cliente ${scelta ? "scelta" : ""}`}
                type="button"
                aria-pressed={scelta}
                onClick={() => { onScegli(azienda); onChiudi(); }}
              >
                <span className="tendina-sigla">{inizialiAzienda(azienda.ragioneSociale)}</span>
                <span className="tendina-nome">
                  <strong>{azienda.ragioneSociale}</strong>
                  <small>{[
                    azienda.partitaIva ? `P.IVA ${azienda.partitaIva}` : null,
                    azienda.comune ? `${azienda.comune}${azienda.provincia ? ` (${azienda.provincia})` : ""}` : null,
                  ].filter(Boolean).join(" · ") || "Anagrafica azienda"}</small>
                </span>
                {scelta && <Check size={16} aria-hidden="true" />}
              </button>
            );
          })
        )}
      </div>

      {filtrate.length > 8 && (
        <p className="tendina-conteggio">Mostrate 8 su {filtrate.length}: scrivi per restringere.</p>
      )}

      <button className="tendina-nuovo" type="button" onClick={() => { onCrea(); onChiudi(); }}>
        <Plus size={16} aria-hidden="true" /> Nuovo cliente
      </button>
    </div>
  );
}

/** L'unica barra dell'app: marchio, cliente, i cinque mondi, ricerca e strumenti. */
function BarraAlta({
  active,
  ruolo,
  aziende,
  aziendaCorrente,
  aperto,
  onApri,
  onNavigate,
  onScegliAzienda,
  onCreaAzienda,
  onCerca,
  onAiuto,
  onAurora,
  onAuroraPointerDown,
  auroraAperta,
  notifiche,
  onNotifiche,
  onEsci,
}: {
  active: SectionKey;
  ruolo: string | null;
  aziende: Azienda[];
  aziendaCorrente: Azienda | null;
  aperto: ChiaveMondo | "azienda" | null;
  onApri: (che: ChiaveMondo | "azienda" | null) => void;
  onNavigate: (key: SectionKey) => void;
  onScegliAzienda: (azienda: Azienda) => void;
  onCreaAzienda: () => void;
  onCerca: () => void;
  onAiuto: () => void;
  onAurora: (trigger: HTMLButtonElement) => void;
  onAuroraPointerDown: () => void;
  auroraAperta: boolean;
  notifiche: number;
  onNotifiche: () => void;
  onEsci: () => void;
}) {
  const account = etichettaAccount();
  const mondoAttivo = mondoDiSezione(active);
  const barra = useRef<HTMLDivElement>(null);

  // Un clic fuori o Esc chiudono la tendina aperta: nessun pannello resta appeso.
  useEffect(() => {
    if (!aperto) return;
    const fuori = (e: PointerEvent) => {
      if (!barra.current?.contains(e.target as Node)) onApri(null);
    };
    const tasto = (e: KeyboardEvent) => { if (e.key === "Escape") onApri(null); };
    document.addEventListener("pointerdown", fuori);
    window.addEventListener("keydown", tasto);
    return () => {
      document.removeEventListener("pointerdown", fuori);
      window.removeEventListener("keydown", tasto);
    };
  }, [aperto, onApri]);

  return (
    <div className="barra-alta-wrap" ref={barra}>
      <header className="barra-alta">
        <button className="barra-marchio" type="button" onClick={() => onNavigate("dashboard")} title="Home">
          <span className="brand-mark">P</span>
          <strong>PaghePro</strong>
        </button>

        <div className="barra-ancora">
          <button
            className={`barra-cliente ${aperto === "azienda" ? "aperto" : ""} ${aziendaCorrente ? "" : "vuoto"}`}
            type="button"
            aria-expanded={aperto === "azienda"}
            aria-haspopup="dialog"
            onClick={() => onApri(aperto === "azienda" ? null : "azienda")}
          >
            <span className="barra-sigla">
              {aziendaCorrente ? inizialiAzienda(aziendaCorrente.ragioneSociale) : <Building2 size={15} aria-hidden="true" />}
            </span>
            <strong>{aziendaCorrente?.ragioneSociale ?? "Scegli il cliente"}</strong>
            <ChevronDown size={15} aria-hidden="true" />
          </button>
          {aperto === "azienda" && (
            <TendinaAzienda
              aziende={aziende}
              aziendaCorrente={aziendaCorrente}
              onScegli={onScegliAzienda}
              onCrea={onCreaAzienda}
              onChiudi={() => onApri(null)}
            />
          )}
        </div>

        <nav className="barra-mondi" aria-label="Sezioni">
          {MONDI.map((mondo) => (
            <div className="barra-ancora" key={mondo.chiave}>
              <button
                className={`barra-mondo ${aperto === mondo.chiave ? "aperto" : ""} ${mondoAttivo === mondo.chiave ? "attivo" : ""}`}
                type="button"
                aria-expanded={aperto === mondo.chiave}
                aria-haspopup="menu"
                onClick={() => onApri(aperto === mondo.chiave ? null : mondo.chiave)}
              >
                {mondo.titolo}
                <ChevronDown size={14} aria-hidden="true" />
              </button>
              {aperto === mondo.chiave && (
                <TendinaMondo
                  mondo={mondo}
                  ruolo={ruolo}
                  active={active}
                  onNavigate={onNavigate}
                  onChiudi={() => onApri(null)}
                />
              )}
            </div>
          ))}
        </nav>

        <button className="barra-cerca" type="button" aria-label="Cerca uno strumento" onClick={onCerca}>
          <Search size={17} aria-hidden="true" />
          <span>Cerca</span>
          <kbd>⌘K</kbd>
        </button>

        <button
          className="barra-icona aurora"
          type="button"
          aria-label={auroraAperta ? "Porta in primo piano Aurora" : "Apri Aurora"}
          aria-controls="aurora-panel"
          aria-expanded={auroraAperta}
          onPointerDown={onAuroraPointerDown}
          onClick={(evento) => onAurora(evento.currentTarget)}
          title="Aurora: seleziona un testo e premi qui per fartelo spiegare"
        >
          <Sparkles size={18} aria-hidden="true" />
        </button>
        <button className="barra-icona" type="button" aria-label="Scadenze da gestire" title="Scadenze da gestire" onClick={onNotifiche}>
          <Bell size={18} aria-hidden="true" />
          {notifiche > 0 && <b>{notifiche}</b>}
        </button>
        <button className="barra-icona" type="button" aria-label="Aiuto su questa pagina" title="Aiuto su questa pagina" onClick={onAiuto}>
          <HelpCircle size={18} aria-hidden="true" />
        </button>
        <button className="barra-conto" type="button" title={`${account.nome} — ${account.dettaglio}`} onClick={onEsci}>
          <span>{account.iniziali}</span>
          <LogOut size={15} aria-hidden="true" />
        </button>
      </header>

    </div>
  );
}

/** Sul telefono i cinque mondi stanno in fondo, dove arriva il pollice. */
function BarraMobile({
  active,
  aperto,
  onApri,
  onHome,
}: {
  active: SectionKey;
  aperto: ChiaveMondo | "azienda" | null;
  onApri: (che: ChiaveMondo | "azienda" | null) => void;
  onHome: () => void;
}) {
  const mondoAttivo = mondoDiSezione(active);
  return (
    <nav className="barra-mobile" aria-label="Sezioni">
      <button
        className={active === "dashboard" ? "attivo" : ""}
        type="button"
        onClick={onHome}
      >
        <LayoutDashboard size={19} aria-hidden="true" />
        <small>Home</small>
      </button>
      {MONDI.map((mondo) => (
        <button
          key={mondo.chiave}
          className={`${mondoAttivo === mondo.chiave ? "attivo" : ""} ${aperto === mondo.chiave ? "aperto" : ""}`}
          type="button"
          aria-expanded={aperto === mondo.chiave}
          onClick={() => onApri(aperto === mondo.chiave ? null : mondo.chiave)}
        >
          <MondoIcona chiave={mondo.chiave} />
          <small>{mondo.titolo}</small>
        </button>
      ))}
    </nav>
  );
}

function MondoIcona({ chiave }: { chiave: ChiaveMondo }) {
  const Icona = chiave === "paghe" ? Users
    : chiave === "fisco" ? Landmark
    : chiave === "clienti" ? Building2
    : chiave === "documenti" ? FolderOpen
    : Settings;
  return <Icona size={19} aria-hidden="true" />;
}

function CommandPalette({ onClose, onNavigate }: { onClose: () => void; onNavigate: (key: SectionKey) => void }) {
  const [query, setQuery] = useState("");
  const ruolo = getRuolo();
  // Cerca fra tutti gli strumenti dell'app, non fra una manciata scelta a mano:
  // «cud» trova la Certificazione Unica, «busta» le buste paga.
  const risultati = cercaVoci(query, ruolo).slice(0, 12);

  return (
    <div className="command-overlay" role="dialog" aria-modal="true" aria-label="Cerca uno strumento" onClick={onClose}>
      <section className="command-dialog glass-panel" onClick={(e) => e.stopPropagation()}>
        <div className="command-search">
          <Search size={18} aria-hidden="true" />
          <input
            autoFocus
            id="command-search"
            name="command-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Scrivi cosa ti serve: busta, F24, DURC, visura…"
          />
          <button type="button" aria-label="Chiudi ricerca" onClick={onClose}><X size={17} aria-hidden="true" /></button>
        </div>
        <div className="command-list">
          {risultati.map((voce) => (
            <button
              key={voce.key}
              className="command-action"
              type="button"
              onClick={() => { onNavigate(voce.key); onClose(); }}
            >
              <strong>
                {voce.label}
                <small>{voce.detail}</small>
              </strong>
              <em>{voce.titoloMondo}</em>
            </button>
          ))}
          {risultati.length === 0 && <p className="command-empty">Nessuno strumento trovato con questo testo.</p>}
        </div>
      </section>
    </div>
  );
}

function apriGuida() {
  // La guida è servita come file statico da public/guida (vedi cartella guida).
  window.open(`${import.meta.env.BASE_URL}guida/index.html`, "_blank", "noopener");
}

// Pannello di aiuto: spiega in modo semplice SOLO la sezione dove sei.
function AiutoPanel({ sezione, onChiudi }: { sezione: SectionKey; onChiudi: () => void }) {
  // Chiudi con il tasto Esc.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onChiudi(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onChiudi]);

  const scheda = aiutoPerSezione[sezione] ?? aiutoPerSezione.dashboard;

  return (
    <div className="aiuto-overlay" role="dialog" aria-modal="true" aria-label="Aiuto" onClick={onChiudi}>
      <aside className="aiuto-panel glass-panel" onClick={(e) => e.stopPropagation()}>
        <header className="aiuto-head">
          <div>
            <span className="aiuto-tag">❓ Aiuto</span>
            <h2>{scheda.titolo}</h2>
          </div>
          <button className="aiuto-close" type="button" aria-label="Chiudi" onClick={onChiudi}>✕</button>
        </header>

        <div className="aiuto-body">
          <div className="aiuto-cosa">
            <strong>A cosa serve</strong>
            <p>{scheda.aCosaServe}</p>
          </div>

          <div className="aiuto-passi">
            <strong>Come si fa, passo per passo</strong>
            <ol>
              {scheda.passi.map((passo, i) => (
                <li key={i}>{passo}</li>
              ))}
            </ol>
          </div>

          {scheda.faq && scheda.faq.length > 0 && (
            <div className="aiuto-faq">
              <strong>Domande frequenti</strong>
              {scheda.faq.map((f, i) => (
                <details key={i}>
                  <summary>{f.domanda}</summary>
                  <p>{f.risposta}</p>
                </details>
              ))}
            </div>
          )}
        </div>

        <footer className="aiuto-foot">
          <span>Vuoi la guida completa con la voce?</span>
          <button className="link-button" type="button" onClick={apriGuida}>📘 Apri la guida animata</button>
        </footer>
      </aside>
    </div>
  );
}

// Avviso fisso DURC: resta in cima a ogni pagina finché c'è un DURC scaduto o in scadenza.
// La richiesta vera si fa sul portale ufficiale (SPID/CIE/CNS): il pulsante porta lì.
function AvvisoDurc({
  avvisi,
  onApriScadenzario,
  onChiudi,
}: {
  avvisi: Promemoria[];
  onApriScadenzario: () => void;
  onChiudi: () => void;
}) {
  const scaduti = avvisi.some((p) => p.urgenza === "scaduto" || p.urgenza === "oggi");
  const quando = (p: Promemoria) =>
    p.giorniAllaScadenza < 0
      ? `scaduto da ${Math.abs(p.giorniAllaScadenza)} gg`
      : p.giorniAllaScadenza === 0
        ? "scade oggi"
        : `scade tra ${p.giorniAllaScadenza} gg`;
  return (
    <div className={`avviso-durc ${scaduti ? "rosso" : ""}`} role="alert">
      <span className="avviso-icona">{scaduti ? "🚨" : "⏰"}</span>
      <div className="avviso-testo">
        <strong>{scaduti ? "DURC scaduto: richiedi subito il rinnovo" : "DURC in scadenza: richiedi il rinnovo"}</strong>
        <span>
          {avvisi.slice(0, 3).map((p) => `${p.titolo.replace("Rinnovo DURC ", "")} (${quando(p)})`).join(" · ")}
          {avvisi.length > 3 ? ` · e altri ${avvisi.length - 3}` : ""}
          {" — richiesta gratuita su Durc On Line (INPS/INAIL) con SPID, CIE o CNS; poi registra qui il nuovo DURC."}
        </span>
      </div>
      <div className="avviso-azioni">
        <a className="btn primary" href={DURC_ONLINE_URL} onClick={(e) => {
          e.preventDefault();
          void apriLinkEsterno(DURC_ONLINE_URL);
        }}>
          🌐 Richiedi Durc On Line
        </a>
        <button className="btn secondary" type="button" onClick={onApriScadenzario}>
          Apri Scadenzario
        </button>
        <button
          className="avviso-chiudi"
          type="button"
          aria-label="Nascondi avviso"
          title="Nascondi (ricompare alla prossima apertura)"
          onClick={onChiudi}
        >
          ✕
        </button>
      </div>
    </div>
  );
}

/**
 * Il cliente serve per questa schermata e non è ancora scelto: la scelta appare
 * al posto della schermata, non su un'altra pagina. Scelto il cliente, la
 * schermata richiesta si monta da sola.
 */
function SceltaClienteRichiesta({
  sezione,
  aziende,
  erroreAziende,
  onScegli,
  onCrea,
}: {
  sezione: string;
  aziende: Azienda[];
  erroreAziende: string | null;
  onScegli: (azienda: Azienda) => void;
  onCrea: () => void;
}) {
  const [ricerca, setRicerca] = useState("");
  const ultimaAziendaId = leggiUltimaAziendaId();
  const filtrate = useMemo(() => {
    const testo = ricerca.trim().toLocaleLowerCase("it");
    const base = testo
      ? aziende.filter((a) => [a.ragioneSociale, a.partitaIva, a.codiceFiscale, a.comune, a.provincia]
        .filter(Boolean)
        .some((valore) => String(valore).toLocaleLowerCase("it").includes(testo)))
      : aziende;
    return [...base].sort((a, b) => {
      if (a.id === ultimaAziendaId) return -1;
      if (b.id === ultimaAziendaId) return 1;
      return a.ragioneSociale.localeCompare(b.ragioneSociale, "it");
    });
  }, [aziende, ricerca, ultimaAziendaId]);

  return (
    <section className="scelta-cliente glass-card" aria-labelledby="titolo-scelta-cliente">
      <h1 id="titolo-scelta-cliente">Per «{sezione}» scegli il cliente</h1>
      <p>Nessuna azienda viene scelta al posto tuo: così una busta paga non finisce mai sul cliente sbagliato.</p>

      {erroreAziende && <p className="scelta-errore" role="alert">{erroreAziende}</p>}

      {aziende.length === 0 ? (
        <div className="scelta-vuota">
          <Building2 size={26} aria-hidden="true" />
          <div>
            <strong>Non hai ancora clienti</strong>
            <span>Registra il primo cliente: puoi partire da una visura, da un testo incollato o a mano.</span>
          </div>
          <button className="btn primary" type="button" onClick={onCrea}>
            <Plus size={17} aria-hidden="true" /> Crea il primo cliente
          </button>
        </div>
      ) : (
        <>
          <label className="scelta-cerca" htmlFor="cerca-azienda-home">
            <Search size={18} aria-hidden="true" />
            <input
              id="cerca-azienda-home"
              autoFocus
              value={ricerca}
              onChange={(e) => setRicerca(e.target.value)}
              placeholder="Cerca per nome, P.IVA, codice fiscale o comune"
              autoComplete="off"
              aria-label="Cerca azienda cliente"
            />
          </label>

          {filtrate.length === 0 ? (
            <p className="scelta-nessuno">Nessuna azienda trovata con questo testo.</p>
          ) : (
            <div className="scelta-elenco">
              {filtrate.slice(0, 9).map((azienda) => (
                <button
                  key={azienda.id}
                  className="scelta-cliente-voce"
                  type="button"
                  onClick={() => onScegli(azienda)}
                >
                  <span className="scelta-sigla">{inizialiAzienda(azienda.ragioneSociale)}</span>
                  <span className="scelta-nome">
                    <strong>{azienda.ragioneSociale}</strong>
                    <small>{[
                      azienda.partitaIva ? `P.IVA ${azienda.partitaIva}` : null,
                      azienda.comune ? `${azienda.comune}${azienda.provincia ? ` (${azienda.provincia})` : ""}` : null,
                    ].filter(Boolean).join(" · ") || "Anagrafica azienda"}</small>
                  </span>
                  {azienda.id === ultimaAziendaId && <em>Ultimo usato</em>}
                </button>
              ))}
            </div>
          )}
          {filtrate.length > 9 && (
            <p className="scelta-conteggio">Mostrati 9 clienti su {filtrate.length}: scrivi per restringere.</p>
          )}
          <button className="scelta-nuovo" type="button" onClick={onCrea}>
            <Plus size={16} aria-hidden="true" /> Oppure registra un nuovo cliente
          </button>
        </>
      )}
    </section>
  );
}

/** Un lavoro della Home: cosa c'è da fare, a che punto è, e il pulsante per farlo. */
interface Lavoro {
  chiave: string;
  icona: LucideIcon;
  titolo: string;
  stato: string;
  urgenza?: "ok" | "attesa" | "urgente";
  azione: string;
  vaiA: SectionKey;
}

function nomeMese(mese: number): string {
  return ["gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio",
    "agosto", "settembre", "ottobre", "novembre", "dicembre"][mese - 1] ?? "";
}

/**
 * Home fatta di lavori, non di strumenti. Ogni riquadro dice a che punto sei e
 * porta dentro con un clic solo, con il cliente e il mese già impostati.
 */
function DashboardHome({
  onNavigate,
  aziendaCorrente,
  onScegliCliente,
  onCreaAzienda,
  promemoria,
  promemoriaInCaricamento,
  errorePromemoria,
  strumentiRecenti,
  aziende,
  onRiprendi,
}: {
  onNavigate: (key: SectionKey) => void;
  aziendaCorrente: Azienda | null;
  onScegliCliente: () => void;
  onCreaAzienda: () => void;
  promemoria: Promemoria[];
  promemoriaInCaricamento: boolean;
  errorePromemoria: string | null;
  strumentiRecenti: StrumentoRecente[];
  aziende: Azienda[];
  onRiprendi: (voce: StrumentoRecente) => void;
}) {
  const oggi = new Date();
  const anno = oggi.getFullYear();
  const mese = oggi.getMonth() + 1;

  const [presenze, setPresenze] = useState<{ elaborati: number; totale: number } | null>(null);
  const [presenzeErrore, setPresenzeErrore] = useState<string | null>(null);
  const [presenzeInCorso, setPresenzeInCorso] = useState(false);

  useEffect(() => {
    if (!aziendaCorrente) { setPresenze(null); setPresenzeErrore(null); return; }
    let vivo = true;
    setPresenze(null);
    setPresenzeErrore(null);
    setPresenzeInCorso(true);
    api.presenzeMese(aziendaCorrente.id, anno, mese)
      .then((r) => { if (vivo) setPresenze({ elaborati: r.elaborati, totale: r.totale }); })
      .catch(() => { if (vivo) setPresenzeErrore("Non riesco a leggere lo stato del mese."); })
      .finally(() => { if (vivo) setPresenzeInCorso(false); });
    return () => { vivo = false; };
  }, [aziendaCorrente, anno, mese]);

  const urgenti = promemoria.filter((p) => p.urgenza === "scaduto" || p.urgenza === "oggi");
  const inScadenza = promemoria.filter((p) => p.urgenza === "in_scadenza");

  const statoBuste = !aziendaCorrente ? "Scegli prima il cliente"
    : presenzeErrore ? presenzeErrore
    : presenzeInCorso ? "Controllo in corso…"
    : presenze ? presenze.totale === 0 ? "Nessun dipendente in forza"
      : presenze.elaborati >= presenze.totale ? `Tutte fatte: ${presenze.totale} su ${presenze.totale}`
        : `${presenze.elaborati} fatte su ${presenze.totale}`
    : "—";

  const lavori: Lavoro[] = [
    {
      chiave: "buste",
      icona: FileText,
      titolo: `Buste paga di ${nomeMese(mese)}`,
      stato: statoBuste,
      urgenza: presenze && presenze.totale > 0 && presenze.elaborati >= presenze.totale ? "ok" : "attesa",
      azione: presenze && presenze.elaborati > 0 ? "Continua" : "Comincia",
      vaiA: "presenze",
    },
    {
      chiave: "f24",
      icona: Landmark,
      titolo: "F24 del mese",
      stato: "Versamenti da cedolini e scadenze",
      urgenza: "attesa",
      azione: "Prepara",
      vaiA: "f24",
    },
    {
      chiave: "scadenze",
      icona: AlarmClock,
      titolo: "Scadenze",
      stato: promemoriaInCaricamento ? "Controllo in corso…"
        : errorePromemoria ? errorePromemoria
        : urgenti.length > 0 ? `${urgenti.length} da fare subito`
        : inScadenza.length > 0 ? `${inScadenza.length} in arrivo`
        : "Nessuna scadenza aperta",
      urgenza: urgenti.length > 0 ? "urgente" : inScadenza.length > 0 ? "attesa" : "ok",
      azione: "Vedi",
      vaiA: "scadenzario",
    },
    {
      chiave: "dipendente",
      icona: Users,
      titolo: "Nuovo dipendente",
      stato: "Da UNILAV, oppure a mano",
      azione: "Aggiungi",
      vaiA: "dipendenti",
    },
    {
      chiave: "fatture",
      icona: ReceiptText,
      titolo: "Fatture",
      stato: "Emissione e invio allo SdI",
      azione: "Apri",
      vaiA: "fatture",
    },
    {
      chiave: "documenti",
      icona: FolderOpen,
      titolo: "Carica un documento",
      stato: "Visura, UNILAV, DURC, ricevute",
      azione: "Carica",
      vaiA: "documenti",
    },
  ];

  const recenti = strumentiRecenti
    .map((voce) => ({ voce, azienda: aziende.find((a) => a.id === voce.aziendaId) }))
    .filter((r): r is { voce: StrumentoRecente; azienda: Azienda } => Boolean(r.azienda));

  return (
    <div className="home">
      <header className="home-testa">
        <div>
          <h1>{aziendaCorrente ? aziendaCorrente.ragioneSociale : "Scegli il cliente per cominciare"}</h1>
          <p>
            {aziendaCorrente
              ? `${nomeMese(mese)} ${anno} · tutto quello che vedi qui sotto riguarda questo cliente.`
              : "Nessun cliente viene scelto al posto tuo: prima il cliente, poi il lavoro."}
          </p>
        </div>
        <button
          className="btn secondary"
          type="button"
          onClick={aziendaCorrente || aziende.length > 0 ? onScegliCliente : onCreaAzienda}
        >
          <Building2 size={17} aria-hidden="true" />
          {aziendaCorrente ? "Cambia cliente" : aziende.length > 0 ? "Scegli cliente" : "Crea il primo cliente"}
        </button>
      </header>

      {!aziendaCorrente && aziende.length > 0 && (
        <button className="home-scegli" type="button" onClick={onScegliCliente}>
          <Search size={18} aria-hidden="true" />
          <span><strong>Scegli fra i tuoi {aziende.length} clienti</strong><small>Si apre l'elenco con la ricerca</small></span>
          <ChevronDown size={17} aria-hidden="true" />
        </button>
      )}

      {recenti.length > 0 && (
        <section className="home-riprendi" aria-label="Riprendi il lavoro">
          <span className="home-riprendi-etichetta">Riprendi</span>
          {recenti.map(({ voce, azienda }) => (
            <button
              className="home-riprendi-voce"
              type="button"
              key={`${voce.sezione}-${voce.aziendaId}`}
              onClick={() => onRiprendi(voce)}
            >
              <strong>{sectionTitle[voce.sezione]}</strong>
              <small>{azienda.ragioneSociale} · {tempoRecente(voce.usatoAlle)}</small>
            </button>
          ))}
        </section>
      )}

      <h2 className="home-titolo-sezione">Da fare</h2>
      <div className="home-lavori">
        {lavori.map((lavoro) => (
          <button
            key={lavoro.chiave}
            className={`home-lavoro ${lavoro.urgenza ?? ""}`}
            type="button"
            onClick={() => onNavigate(lavoro.vaiA)}
          >
            <span className="home-lavoro-icona"><lavoro.icona size={20} aria-hidden="true" /></span>
            <span className="home-lavoro-testo">
              <strong>{lavoro.titolo}</strong>
              <small>{lavoro.stato}</small>
            </span>
            <span className="home-lavoro-azione">{lavoro.azione}</span>
          </button>
        ))}
      </div>

      <ScadenzePanel
        anno={anno}
        promemoria={promemoria}
        inCaricamento={promemoriaInCaricamento}
        errore={errorePromemoria}
        onNavigate={onNavigate}
      />
    </div>
  );
}

function tempoRecente(timestamp: number): string {
  const minuti = Math.max(0, Math.floor((Date.now() - timestamp) / 60_000));
  if (minuti < 1) return "adesso";
  if (minuti < 60) return `${minuti} min fa`;
  const ore = Math.floor(minuti / 60);
  if (ore < 24) return `${ore} ${ore === 1 ? "ora" : "ore"} fa`;
  const giorni = Math.floor(ore / 24);
  return giorni === 1 ? "ieri" : `${giorni} giorni fa`;
}

// Tabella scadenze stile "Comunicazioni" di sportello: tab con contatori,
// righe reali dallo scadenzario, filtro per anno dalla banda in alto.
type TabScadenze = "tutte" | "urgenti" | "durc" | "f24";

function ScadenzePanel({
  anno,
  promemoria,
  inCaricamento,
  errore,
  onNavigate,
}: {
  anno: number;
  promemoria: Promemoria[];
  inCaricamento: boolean;
  errore: string | null;
  onNavigate: (key: SectionKey) => void;
}) {
  const [tab, setTab] = useState<TabScadenze>("tutte");

  const delAnno = promemoria.filter((p) => Number(p.dataScadenza.slice(0, 4)) === anno);
  const urgenti = delAnno.filter((p) => ["scaduto", "oggi", "in_scadenza"].includes(p.urgenza));
  const durc = delAnno.filter((p) => p.tipo === "Durc");
  const f24 = delAnno.filter((p) => p.tipo === "F24");
  const perTab: Record<TabScadenze, Promemoria[]> = { tutte: delAnno, urgenti, durc, f24 };
  const tabs: { key: TabScadenze; label: string; conteggio: number }[] = [
    { key: "tutte", label: "Tutte", conteggio: delAnno.length },
    { key: "urgenti", label: "Urgenti", conteggio: urgenti.length },
    { key: "durc", label: "DURC", conteggio: durc.length },
    { key: "f24", label: "F24", conteggio: f24.length },
  ];
  const righe = [...perTab[tab]]
    .sort((a, b) => a.dataScadenza.localeCompare(b.dataScadenza))
    .slice(0, 8);
  const totale = perTab[tab].length;

  const badge = (p: Promemoria): { testo: string; tono: string } => {
    if (p.urgenza === "scaduto") return { testo: "Scaduto", tono: "red" };
    if (p.urgenza === "oggi") return { testo: "Oggi", tono: "red" };
    if (p.urgenza === "in_scadenza") return { testo: "In scadenza", tono: "amber" };
    return { testo: p.stato, tono: p.stato === "Completato" ? "green" : "blue" };
  };
  const dataBreve = (iso: string) => {
    const data = new Date(`${iso.slice(0, 10)}T12:00:00`);
    return Number.isNaN(data.getTime()) ? "—" : data.toLocaleDateString("it-IT");
  };

  return (
    <section className="qw-card qw-comunica" aria-label="Scadenze e promemoria">
      <div className="qw-com-head">
        <Megaphone size={16} aria-hidden="true" />
        <strong>Scadenze e promemoria {anno}</strong>
        <button className="btn secondary qw-com-apri" type="button" onClick={() => onNavigate("scadenzario")}>
          Apri scadenzario
        </button>
      </div>
      <div className="qw-tabs" role="tablist">
        {tabs.map((voce) => (
          <button
            key={voce.key}
            className={`qw-tab ${tab === voce.key ? "attiva" : ""}`}
            type="button"
            role="tab"
            aria-selected={tab === voce.key}
            onClick={() => setTab(voce.key)}
          >
            {voce.label}
            {voce.conteggio > 0 && <span className="qw-count">{voce.conteggio}</span>}
          </button>
        ))}
      </div>
      {inCaricamento ? (
        <p className="muto qw-panel-empty" aria-live="polite">Aggiornamento scadenze…</p>
      ) : errore ? (
        <p className="company-choice-alert error" role="alert">{errore}</p>
      ) : righe.length === 0 ? (
        <p className="muto qw-panel-empty">Nessuna scadenza registrata per il {anno} in questa vista.</p>
      ) : (
        <>
          <div className="table-wrap">
            <table className="data-table qw-com-table">
              <thead>
                <tr>
                  <th>Anno</th>
                  <th>Scadenza</th>
                  <th>Promemoria</th>
                  <th>Tipo</th>
                  <th>Stato</th>
                </tr>
              </thead>
              <tbody>
                {righe.map((p) => {
                  const stato = badge(p);
                  return (
                    <tr key={p.id} className="qw-com-riga" onClick={() => onNavigate("scadenzario")}>
                      <td className="num">{p.dataScadenza.slice(0, 4)}</td>
                      <td className="num">{dataBreve(p.dataScadenza)}</td>
                      <td><strong>{p.titolo}</strong>{p.descrizione && <small className="qw-com-desc">{p.descrizione}</small>}</td>
                      <td>{p.tipo}</td>
                      <td><span className={`status-pill ${stato.tono}`}>{stato.testo}</span></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <p className="qw-com-foot">Visualizzati 1 - {righe.length} di {totale}</p>
        </>
      )}
    </section>
  );
}

// Orologio + calendario del mese corrente in un solo riquadro: ora/data aggiornate ogni
// secondo, oggi evidenziato nella griglia, un puntino sui giorni con scadenze.
function ModuleScreen({
  active,
  onNavigate,
  rapportoSel,
  onApriDipendente,
  aziendaDaCompletareId,
  onAziendaDaCompletareGestita,
}: {
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
  rapportoSel: string | null;
  onApriDipendente: (rapportoId: string) => void;
  aziendaDaCompletareId: string | null;
  onAziendaDaCompletareGestita: () => void;
}) {
  if (active === "azienda" || active === "aziende") {
    return (
      <div className="module-layout wide aziende-page-layout">
        <div>
          <Aziende
            apriAziendaId={aziendaDaCompletareId}
            onApriGestita={onAziendaDaCompletareGestita}
          />
        </div>
      </div>
    );
  }

  if (active === "cedolini") return <Cedolini onTornaPresenze={() => onNavigate("presenze")} />;
  if (active === "agricoltura") return <CedolinoAgricolo />;

  if (active === "calcolo") {
    return (
      <div className="module-layout wide">
        <div>
          <div className="page-heading">
            <div>
              <h1>Calcolo busta paga</h1>
              <p>Motore CCNL Commercio, contributi e netto in busta</p>
            </div>
            <div className="actions-row">
              <button className="btn secondary" type="button" onClick={() => onNavigate("cedolini")}>
                Anteprima cedolino
              </button>
              <button className="btn secondary" type="button" onClick={() => onNavigate("elaborazioni")}>
                Elaborazione massiva
              </button>
            </div>
          </div>
          <Cedolino />
        </div>
        <ModuleRail
          title="Controlli calcolo"
          rows={[
            ["CCNL", "Commercio"],
            ["Mensilità", "14"],
            ["Anno fiscale", "2026"],
            ["Modalita", "Demo API"],
          ]}
        />
      </div>
    );
  }

  if (active === "dipendenti") return <Dipendenti onApri={onApriDipendente} />;
  if (active === "dipendente") return <Dipendente rapportoId={rapportoSel} onPayslip={() => onNavigate("cedolini")} onBack={() => onNavigate("dipendenti")} />;
  if (active === "presenze") return <Presenze />;
  if (active === "elaborazioni") return <Elaborazione onApriSezione={onNavigate} />;
  if (active === "adempimenti") return <DeadlinesPanel onNavigate={onNavigate} />;
  if (active === "fatture") return <Fatture />;
  if (active === "pratiche") return <Pratiche />;
  if (active === "moduli") return <Moduli />;
  if (active === "scadenzario") return <Scadenzario />;
  if (active === "posta") return <Posta />;
  if (active === "pagamenti") return <Pagamenti />;
  if (active === "firma") return <Firma />;
  if (active === "contabilita") return <AccountingPanel />;
  if (active === "lipe") return <Lipe />;
  if (active === "iva-annuale") return <IvaAnnuale />;
  if (active === "bilancio") return <Bilancio />;
  if (active === "documenti") return <DocumentsPanel onNavigate={onNavigate} />;
  if (active === "f24") return <F24Panel />;
  if (active === "fine-anno") return <FineAnno />;
  if (active === "cu") return <CertificazioneUnica />;
  if (active === "mod770") return <Modello770 />;
  if (active === "redditi-pf") return <RedditiPf />;
  if (active === "irap") return <Irap />;
  if (active === "ires") return <Ires />;
  if (active === "imu") return <Imu />;
  if (active === "intrastat") return <Intrastat />;
  if (active === "impostazioni") return <SettingsPanel />;
  if (active === "collega-sdi") return <CollegaProvider />;
  if (active === "utenti") return <UsersPanel />;
  if (active === "audit") return <RegistroAttivita />;
  if (active === "retention") return <Retention />;
  if (active === "studi") return <Studi />;

  return <GenericPanel active={active} onNavigate={onNavigate} />;
}

function ModuleRail({ title, rows }: { title: string; rows: string[][] }) {
  return (
    <aside className="glass-card module-rail">
      <h2>{title}</h2>
      {rows.map(([label, value]) => (
        <div className="rail-metric" key={label}>
          <span>{label}</span>
          <strong>{value}</strong>
        </div>
      ))}
    </aside>
  );
}

function DeadlinesPanel({ onNavigate }: { onNavigate: (key: SectionKey) => void }) {
  const rows: string[][] = [];

  return (
    <div className="workbench-grid">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Adempimenti</h1>
            <p>Scadenze fiscali, previdenziali e invii telematici</p>
          </div>
          <button className="btn primary" type="button" onClick={() => onNavigate("f24")}>
            Prepara F24
          </button>
        </div>
        <div className="table-wrap">
          <table className="data-table spacious">
            <thead>
              <tr>
                <th>Tipo</th>
                <th>Adempimento</th>
                <th>Scadenza</th>
                <th>Responsabile</th>
                <th>Stato</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr><td colSpan={5} className="muto" style={{ padding: 16 }}>Nessun adempimento ancora. Le tue scadenze reali compariranno qui.</td></tr>
              ) : rows.map(([type, title, due, owner, state, tone]) => (
                <tr key={`${type}-${title}`}>
                  <td><span className={`tag ${tone}`}>{type}</span></td>
                  <td><strong>{title}</strong></td>
                  <td className="num">{due}</td>
                  <td>{owner}</td>
                  <td><span className={`status-pill ${tone}`}>{state}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

function AccountingPanel() {
  return (
    <>
      <CaricaUnilav />
      <div className="workbench-grid">
        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h1>Contabilità</h1>
              <p>Quadratura costo del lavoro e scritture mensili</p>
            </div>
            <button className="btn primary" type="button">Esporta prima nota</button>
          </div>
          <p className="muto" style={{ padding: "6px 2px", lineHeight: 1.6 }}>
            Nessun movimento ancora. Il costo del lavoro e le scritture compariranno qui man mano che
            elabori i cedolini e registri le fatture.
          </p>
        </section>
      </div>
    </>
  );
}

function DocumentsPanel({ onNavigate }: { onNavigate: (key: SectionKey) => void }) {
  const docs: string[][] = [];

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Archivio documenti</h1>
          <p>Cedolini, modelli fiscali, flussi XML e ricevute</p>
        </div>
        <button className="btn primary" type="button" onClick={() => onNavigate("cedolini")}>
          Genera cedolino
        </button>
      </div>
      {docs.length === 0 ? (
        <p className="muto" style={{ padding: "6px 2px", lineHeight: 1.6 }}>
          Nessun documento ancora. Cedolini, F24, CU e fatture che generi compariranno qui.
        </p>
      ) : (
        <div className="document-grid">
          {docs.map(([kind, title, format, state, tone]) => (
            <article className="document-card" key={`${kind}-${title}`}>
              <div className="doc-icon">{format}</div>
              <div>
                <strong>{kind}</strong>
                <span>{title}</span>
              </div>
              <em className={`status-pill ${tone}`}>{state}</em>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function F24Panel() {
  const payments: string[][] = [];

  return (
    <div className="document-workspace">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Modello F24</h1>
            <p>Riepilogo versamenti dai cedolini elaborati</p>
          </div>
          <button className="btn primary" type="button">Genera PDF</button>
        </div>
        <div className="table-wrap">
          <table className="data-table spacious">
            <thead>
              <tr>
                <th>Codice tributo</th>
                <th>Descrizione</th>
                <th className="right">Importo</th>
              </tr>
            </thead>
            <tbody>
              {payments.length === 0 ? (
                <tr><td colSpan={3} className="muto" style={{ padding: 16 }}>Nessun tributo ancora. L'F24 si compila dai cedolini elaborati.</td></tr>
              ) : payments.map(([code, label, amount]) => (
                <tr key={code}>
                  <td className="num"><strong>{code}</strong></td>
                  <td>{label}</td>
                  <td className="right num"><strong>{amount}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

function SettingsPanel() {
  const settings = [
    ["Parametri fiscali", "Aliquote IRPEF e addizionali 2026", "Aggiornati", "green"],
    ["CCNL Commercio", "Livelli e minimi tabellari", "Attivo", "blue"],
    ["Brand documenti", "Logo, colori e intestazioni PDF", "Configurato", "green"],
    ["Notifiche scadenze", "Promemoria F24, CU, UniEmens", "Attivo", "blue"],
  ];

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Impostazioni</h1>
          <p>Configurazione studio, parametri e automazioni operative</p>
        </div>
        <button className="btn secondary" type="button">Salva modifiche</button>
      </div>
      <div className="settings-grid">
        {settings.map(([title, desc, state, tone]) => (
          <article className="settings-card" key={title}>
            <div>
              <strong>{title}</strong>
              <span>{desc}</span>
            </div>
            <em className={`status-pill ${tone}`}>{state}</em>
          </article>
        ))}
      </div>
    </section>
  );
}

function UsersPanel() {
  const email = emailDalToken();
  const iniziali = (email.slice(0, 2) || "AC").toUpperCase();
  const nome = email.split("@")[0] || "Account";
  const ruolo = getRuolo() === "superadmin" ? "Super-admin" : "Admin";
  // Solo l'utente realmente collegato: niente utenti di esempio.
  const users = email ? [[iniziali, nome, email, ruolo, "Attivo"]] : [];

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Utenti e permessi</h1>
          <p>Accessi, ruoli e permessi per lo studio</p>
        </div>
        <button className="btn primary" type="button">Invita utente</button>
      </div>
      <div className="table-wrap">
        <table className="data-table spacious">
          <thead>
            <tr>
              <th>Utente</th>
              <th>Email</th>
              <th>Ruolo</th>
              <th>Stato</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr><td colSpan={4} className="muto" style={{ padding: 16 }}>Nessun utente collegato.</td></tr>
            ) : users.map(([initials, name, mail, role, state]) => (
              <tr key={mail}>
                <td>
                  <div className="person-cell">
                    <span>{initials}</span>
                    <strong>{name}</strong>
                  </div>
                </td>
                <td>{mail}</td>
                <td>{role}</td>
                <td><span className={`status-pill ${state === "Attivo" ? "green" : "amber"}`}>{state}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}


function GenericPanel({
  active,
  onNavigate,
}: {
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
}) {
  // Nessun dato di esempio: le voci reali compariranno man mano che lavori.
  const rowsBySection: Partial<Record<SectionKey, string[][]>> = {};
  const rows = rowsBySection[active] ?? [];

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>{sectionTitle[active]}</h1>
          <p>Area operativa allineata al nuovo layout PaghePro</p>
        </div>
        <button className="btn secondary" type="button" onClick={() => onNavigate("dashboard")}>
          Torna al centro operativo
        </button>
      </div>
      <div className="table-wrap">
        <table className="data-table spacious">
          <thead>
            <tr>
              <th>Voce</th>
              <th>Riferimento</th>
              <th>Stato</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr><td colSpan={3} className="muto" style={{ padding: 16 }}>Nessun dato ancora in questa sezione.</td></tr>
            ) : rows.map(([title, ref, state]) => (
              <tr key={`${title}-${ref}`}>
                <td>
                  <strong>{title}</strong>
                </td>
                <td>{ref}</td>
                <td>
                  <span className="status-pill blue">{state}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
