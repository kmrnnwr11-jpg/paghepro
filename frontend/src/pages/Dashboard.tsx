import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlarmClock,
  Archive,
  BadgeEuro,
  Bell,
  BookOpen,
  Building2,
  Calculator,
  CalendarDays,
  CalendarRange,
  ChartNoAxesCombined,
  ChevronDown,
  CircleDot,
  ClipboardList,
  Clock,
  Factory,
  FileSignature,
  FileText,
  Files,
  FolderKanban,
  FolderOpen,
  Globe2,
  HelpCircle,
  House,
  History,
  Landmark,
  LayoutDashboard,
  LogOut,
  Mail,
  Megaphone,
  Menu,
  PlugZap,
  ReceiptText,
  Scale,
  Search,
  Send,
  Settings,
  ShieldCheck,
  Sparkles,
  UserRound,
  Users,
  X,
  type LucideIcon,
} from "lucide-react";
import { api, type Azienda, type Promemoria } from "../api";
import { useAuth } from "../auth";
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

interface NavItem {
  key: SectionKey;
  icon: LucideIcon;
  label: string;
}

const primaryNav: NavItem[] = [
  { key: "dashboard", icon: LayoutDashboard, label: "Home" },
  { key: "aziende", icon: Building2, label: "Aziende" },
  { key: "pratiche", icon: FolderKanban, label: "Pratiche ComUnica" },
  { key: "moduli", icon: ClipboardList, label: "Moduli ufficiali" },
  { key: "dipendenti", icon: Users, label: "Dipendenti" },
  { key: "presenze", icon: CalendarDays, label: "Presenze" },
  { key: "cedolini", icon: FileText, label: "Cedolini" },
  { key: "adempimenti", icon: Send, label: "Adempimenti" },
  { key: "scadenzario", icon: AlarmClock, label: "Scadenzario" },
  { key: "posta", icon: Mail, label: "Posta" },
  { key: "fatture", icon: ReceiptText, label: "Fatturazione" },
  { key: "pagamenti", icon: BadgeEuro, label: "Pagamenti clienti" },
  { key: "firma", icon: FileSignature, label: "Firma digitale" },
  { key: "contabilita", icon: BookOpen, label: "Contabilità" },
  { key: "lipe", icon: ChartNoAxesCombined, label: "LIPE / IVA" },
  { key: "iva-annuale", icon: ReceiptText, label: "IVA annuale" },
  { key: "bilancio", icon: Scale, label: "Bilancio" },
  { key: "documenti", icon: FolderOpen, label: "Documenti" },
];

const payrollNav: NavItem[] = [
  { key: "elaborazioni", icon: Calculator, label: "Elaborazioni" },
  { key: "f24", icon: Landmark, label: "F24" },
  { key: "fine-anno", icon: CalendarRange, label: "Fine anno" },
  { key: "cu", icon: Files, label: "CU / CUD" },
  { key: "mod770", icon: ReceiptText, label: "Modello 770" },
  { key: "redditi-pf", icon: UserRound, label: "Redditi PF / 730" },
  { key: "irap", icon: Factory, label: "IRAP" },
  { key: "ires", icon: Building2, label: "IRES società" },
  { key: "imu", icon: House, label: "IMU" },
  { key: "intrastat", icon: Globe2, label: "Intrastat" },
];

const adminNav: NavItem[] = [
  { key: "collega-sdi", icon: PlugZap, label: "Collega Fatturazione" },
  { key: "impostazioni", icon: Settings, label: "Impostazioni" },
  { key: "utenti", icon: ShieldCheck, label: "Utenti e permessi" },
  { key: "audit", icon: History, label: "Registro attività" },
  { key: "retention", icon: Archive, label: "Conservazione dati" },
];

const quickNav: SectionKey[] = ["dashboard", "aziende", "dipendenti", "documenti", "scadenzario", "f24"];
const allNavItems = [...primaryNav, ...payrollNav, ...adminNav, { key: "studi" as SectionKey, icon: Building2, label: "Studi" }];

// Voce di nav evidenziata quando si è in una schermata di dettaglio.
const navActiveFor: Partial<Record<SectionKey, SectionKey>> = {
  dipendente: "dipendenti",
  calcolo: "cedolini",
};

// Breadcrumb per ogni schermata (allineato a PaghePro Studio).
const crumbsFor: Partial<Record<SectionKey, string[]>> = {
  dashboard: ["Home studio"],
  dipendente: ["Home", "Dipendenti", "Scheda"],
  presenze: ["Home", "Presenze", "Cartellino mensile"],
  elaborazioni: ["Home", "Cedolini", "Elaborazione"],
  cedolini: ["Home", "Cedolini"],
  calcolo: ["Home", "Cedolini", "Calcolo cedolino"],
  pagamenti: ["Home", "Pagamenti clienti"],
};

const sectionTitle: Record<SectionKey, string> = {
  dashboard: "Home studio",
  aziende: "Aziende",
  pratiche: "Pratiche ComUnica",
  moduli: "Moduli ufficiali — AA7/10",
  dipendenti: "Dipendenti",
  dipendente: "Scheda dipendente",
  presenze: "Cartellino presenze",
  cedolini: "Cedolini",
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

const kpis = [
  {
    icon: FileText,
    tone: "blue",
    label: "Buste paga",
    detail: "Cedolini",
    value: "0",
    unit: "",
    foot: "Nessun cedolino ancora",
  },
  {
    icon: Files,
    tone: "green",
    label: "CU / CUD",
    detail: "CU pronte",
    value: "0",
    unit: "",
    foot: "Nessuna CU ancora",
  },
  {
    icon: Landmark,
    tone: "amber",
    label: "F24",
    detail: "F24 da versare",
    value: "0",
    unit: ",00 €",
    foot: "Nessun F24 ancora",
  },
  {
    icon: ReceiptText,
    tone: "cyan",
    label: "Fatture",
    detail: "Fatture SdI",
    value: "0",
    unit: "",
    foot: "Nessuna fattura ancora",
  },
];

export function Dashboard() {
  const { esci } = useAuth();
  const [active, setActive] = useState<SectionKey>(sezioneDaHash);
  const [rapportoSel, setRapportoSel] = useState<string | null>(null);
  const [aiutoAperto, setAiutoAperto] = useState(false);
  const [auroraAperta, setAuroraAperto] = useState(false);
  const [slidebarAperta, setSlidebarAperta] = useState(false);
  const [commandAperta, setCommandAperta] = useState(false);
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [aziendaCorrente, setAziendaCorrente] = useState<Azienda | null>(null);
  const [aziendeInCaricamento, setAziendeInCaricamento] = useState(true);
  const [erroreAziende, setErroreAziende] = useState<string | null>(null);
  const [sceltaAziendaRichiesta, setSceltaAziendaRichiesta] = useState(false);
  const [sezioneDopoScelta, setSezioneDopoScelta] = useState<SectionKey | null>(null);
  const [strumentiRecenti, setStrumentiRecenti] = useState<StrumentoRecente[]>(leggiStrumentiRecenti);
  const aziendeRef = useRef<Azienda[]>([]);
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

  const navActive = navActiveFor[active] ?? active;
  const crumbs = useMemo(() => crumbsFor[active] ?? ["Home", sectionTitle[active]], [active]);

  const scegliAzienda = (azienda: Azienda) => {
    salvaAziendaCorrenteId(azienda.id);
    setAziendaCorrente(azienda);
    setSceltaAziendaRichiesta(false);
  };

  const vaiA = (key: SectionKey) => {
    if (sezioneRichiedeAzienda(key) && !aziendaCorrente) {
      setSezioneDopoScelta(key);
      setSceltaAziendaRichiesta(true);
      setActive("dashboard");
      if (window.location.hash !== "#dashboard") window.location.hash = "dashboard";
      setSlidebarAperta(false);
      window.setTimeout(() => document.getElementById("cerca-azienda-home")?.focus(), 0);
      return;
    }
    if (key === "dashboard") setSezioneDopoScelta(null);
    setActive(key);
    if (window.location.hash !== `#${key}`) window.location.hash = key;
    setSlidebarAperta(false);
  };

  const apriSelettoreAzienda = () => {
    setSezioneDopoScelta(null);
    setActive("dashboard");
    if (window.location.hash !== "#dashboard") window.location.hash = "dashboard";
    setSlidebarAperta(false);
    window.setTimeout(() => document.getElementById("cerca-azienda-home")?.focus(), 0);
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
    if (aziendeInCaricamento || aziendaCorrente || !sezioneRichiedeAzienda(active)) return;
    setSezioneDopoScelta(active);
    setSceltaAziendaRichiesta(true);
    setActive("dashboard");
    if (window.location.hash !== "#dashboard") window.location.hash = "dashboard";
  }, [active, aziendaCorrente, aziendeInCaricamento]);

  useEffect(() => {
    if (!aziendaCorrente || !sezioneDopoScelta) return;
    const destinazione = sezioneDopoScelta;
    setSezioneDopoScelta(null);
    setSceltaAziendaRichiesta(false);
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
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setCommandAperta(true);
      }
      if (e.key === "Escape") {
        setSlidebarAperta(false);
        setCommandAperta(false);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  // Voce "Studi" visibile solo al super-admin di piattaforma.
  const ruolo = getRuolo();
  const amministrazionePerRuolo = adminNav.filter((item) =>
    (item.key !== "audit" || ruolo === "admin" || ruolo === "superadmin")
    // La conservazione dati è una responsabilità dell'admin dello studio: il backend
    // (RetentionEndpoints.SoloAdmin) respinge anche il super-admin di piattaforma.
    && (item.key !== "retention" || ruolo === "admin"));
  const adminNavVisibile = ruolo === "superadmin"
    ? [...amministrazionePerRuolo, { key: "studi" as SectionKey, icon: Building2, label: "Studi (admin)" }]
    : amministrazionePerRuolo;

  return (
    <div className={`app-shell ${auroraAperta ? "con-aurora" : ""}`}>
      <MiniRail active={navActive} onNavigate={vaiA} onOpenMenu={() => setSlidebarAperta(true)} />

      <div
        className={`slidebar-backdrop ${slidebarAperta ? "show" : ""}`}
        aria-hidden={!slidebarAperta}
        onClick={() => setSlidebarAperta(false)}
      />

      <aside className={`sidebar slidebar glass-panel ${slidebarAperta ? "open" : ""}`} aria-hidden={!slidebarAperta}>
        <div className="brand">
          <div className="brand-mark">P</div>
          <div>
            <strong>PaghePro</strong>
            <span>Studio</span>
          </div>
          <button className="slidebar-close" type="button" aria-label="Chiudi menu" onClick={() => setSlidebarAperta(false)}>
            <X size={17} aria-hidden="true" />
          </button>
        </div>

        <nav className="nav-scroll" aria-label="Navigazione principale">
          <NavGroup items={primaryNav} active={navActive} onChange={vaiA} />
          <NavGroup title="Elaborazione paghe" items={payrollNav} active={navActive} onChange={vaiA} />
          <NavGroup title="Amministrazione" items={adminNavVisibile} active={navActive} onChange={vaiA} />
        </nav>

        <button className="company-switch" type="button" onClick={apriSelettoreAzienda}>
          <span className="company-icon">{aziendaCorrente ? inizialiAzienda(aziendaCorrente.ragioneSociale) : <Building2 size={17} aria-hidden="true" />}</span>
          <span>
            <strong>{aziendaCorrente?.ragioneSociale ?? "Scegli azienda"}</strong>
            <small>{aziendaCorrente ? "Azienda di lavoro" : "Prima di iniziare"}</small>
          </span>
          <span className="chevron">›</span>
        </button>
      </aside>

      <div className="workspace">
        <Topbar
          onMenu={() => setSlidebarAperta(true)}
          onCommand={() => setCommandAperta(true)}
          onCompanyClick={apriSelettoreAzienda}
          onAiuto={() => setAiutoAperto(true)}
          onAurora={() => setAuroraAperto((aperto) => !aperto)}
          onEsci={esci}
          notifiche={urgenti.length}
          onNotifiche={() => vaiA("scadenzario")}
          aziendaCorrente={aziendaCorrente}
        />

        {avvisiDurc.length > 0 && !avvisoDurcNascosto && (
          <AvvisoDurc
            avvisi={avvisiDurc}
            onApriScadenzario={() => vaiA("scadenzario")}
            onChiudi={nascondiAvvisoDurc}
          />
        )}

        <main className="content-area">
          <div className="breadcrumbs">
            {crumbs.map((crumb, index) => (
              <span key={crumb} className={index === crumbs.length - 1 ? "current" : undefined}>
                {crumb}
                {index < crumbs.length - 1 && <b>/</b>}
              </span>
            ))}
          </div>

          {aziendeInCaricamento ? (
            <section className="glass-card home-loading" aria-live="polite">Caricamento aziende…</section>
          ) : active === "dashboard" || (!aziendaCorrente && sezioneRichiedeAzienda(active)) ? (
            <DashboardHome
              onNavigate={vaiA}
              aziende={aziende}
              aziendaCorrente={aziendaCorrente}
              onScegliAzienda={scegliAzienda}
              erroreAziende={erroreAziende}
              sceltaRichiesta={sceltaAziendaRichiesta}
              promemoria={promemoria}
              promemoriaInCaricamento={promemoriaInCaricamento}
              errorePromemoria={errorePromemoria}
              strumentiRecenti={strumentiRecenti}
              onRiprendi={riprendiLavoro}
            />
          ) : (
            <ModuleScreen active={active} onNavigate={vaiA} rapportoSel={rapportoSel} onApriDipendente={apriDipendente} />
          )}
        </main>
      </div>

      {auroraAperta && (
        <AuroraPanel
          azienda={aziendaCorrente}
          onNavigate={vaiA}
          onChiudi={() => setAuroraAperto(false)}
        />
      )}

      {aiutoAperto && (
        <AiutoPanel sezione={navActive} onChiudi={() => setAiutoAperto(false)} />
      )}

      {commandAperta && (
        <CommandPalette onClose={() => setCommandAperta(false)} onNavigate={vaiA} />
      )}

      <MobileBottomBar active={navActive} onNavigate={vaiA} onOpenMenu={() => setSlidebarAperta(true)} />
    </div>
  );
}

function itemPerKey(key: SectionKey): NavItem {
  return allNavItems.find((item) => item.key === key) ?? { key, icon: CircleDot, label: sectionTitle[key] };
}

function MiniRail({
  active,
  onNavigate,
  onOpenMenu,
}: {
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
  onOpenMenu: () => void;
}) {
  return (
    <aside className="mini-rail glass-panel" aria-label="Navigazione rapida">
      <button className="rail-brand" type="button" aria-label="Dashboard PaghePro" onClick={() => onNavigate("dashboard")}>
        <span className="brand-mark">P</span>
      </button>
      <nav className="rail-nav">
        {quickNav.map((key) => {
          const item = itemPerKey(key);
          return (
            <button
              key={key}
              className={`rail-item ${active === key ? "active" : ""}`}
              type="button"
              aria-label={item.label}
              title={item.label}
              onClick={() => onNavigate(key)}
            >
              <item.icon size={18} strokeWidth={2.1} aria-hidden="true" />
            </button>
          );
        })}
      </nav>
      <button className="rail-item rail-menu" type="button" aria-label="Apri menu completo" title="Menu" onClick={onOpenMenu}>
        <Menu size={19} aria-hidden="true" />
      </button>
    </aside>
  );
}

function NavGroup({
  title,
  items,
  active,
  onChange,
}: {
  title?: string;
  items: NavItem[];
  active: SectionKey;
  onChange: (key: SectionKey) => void;
}) {
  return (
    <div className="nav-group">
      {title && <div className="nav-title">{title}</div>}
      {items.map((item) => (
        <button
          key={item.key}
          type="button"
          className={`nav-item ${active === item.key ? "active" : ""}`}
          aria-pressed={active === item.key}
          onClick={() => onChange(item.key)}
        >
          <span><item.icon size={17} strokeWidth={2.1} aria-hidden="true" /></span>
          {item.label}
        </button>
      ))}
    </div>
  );
}

function MobileBottomBar({
  active,
  onNavigate,
  onOpenMenu,
}: {
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
  onOpenMenu: () => void;
}) {
  const items: Array<SectionKey | "menu"> = ["dashboard", "aziende", "documenti", "scadenzario", "menu"];
  return (
    <nav className="mobile-bottom glass-panel" aria-label="Navigazione mobile">
      {items.map((key) => {
        if (key === "menu") {
          return (
            <button key="menu" type="button" onClick={onOpenMenu}>
              <span><Menu size={18} aria-hidden="true" /></span>
              <small>Menu</small>
            </button>
          );
        }
        const item = itemPerKey(key);
        return (
          <button
            key={key}
            className={active === key ? "active" : ""}
            type="button"
            onClick={() => onNavigate(key)}
          >
            <span><item.icon size={18} strokeWidth={2.1} aria-hidden="true" /></span>
            <small>{item.label}</small>
          </button>
        );
      })}
    </nav>
  );
}

function CommandPalette({ onClose, onNavigate }: { onClose: () => void; onNavigate: (key: SectionKey) => void }) {
  const [query, setQuery] = useState("");
  const azioni: { icon: LucideIcon; label: string; detail: string; target: SectionKey; badge: string }[] = [
    { icon: Sparkles, label: "Carica documento", detail: "Inbox AI, fatture, F24, DURC, UNILAV", target: "documenti", badge: "AI" },
    { icon: Building2, label: "Aggiungi azienda", detail: "Anagrafica, P.IVA, PEC, dati contributivi", target: "aziende", badge: "360" },
    { icon: Users, label: "Nuovo dipendente", detail: "Aggancio tramite codice fiscale", target: "dipendenti", badge: "CF" },
    { icon: Landmark, label: "Prepara F24", detail: "Versamenti da cedolini e scadenze", target: "f24", badge: "F24" },
    { icon: ChartNoAxesCombined, label: "Controllo IVA", detail: "Liquidazioni, detraibilita, dati mancanti", target: "lipe", badge: "IVA" },
    { icon: AlarmClock, label: "Scadenze", detail: "DURC, F24, CU, UniEmens e promemoria", target: "scadenzario", badge: "Oggi" },
    { icon: BookOpen, label: "Consigli fiscali", detail: "Opportunita lecite da validare", target: "contabilita", badge: "Check" },
  ];
  const q = query.trim().toLowerCase();
  const filtrate = q
    ? azioni.filter((azione) => `${azione.label} ${azione.detail} ${azione.badge}`.toLowerCase().includes(q))
    : azioni;

  return (
    <div className="command-overlay" role="dialog" aria-modal="true" aria-label="Comandi rapidi" onClick={onClose}>
      <section className="command-dialog glass-panel" onClick={(e) => e.stopPropagation()}>
        <div className="command-search">
          <Search size={18} aria-hidden="true" />
          <input
            autoFocus
            id="command-search"
            name="command-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Cerca uno strumento o un'azione"
          />
          <button type="button" aria-label="Chiudi comandi" onClick={onClose}><X size={17} aria-hidden="true" /></button>
        </div>
        <div className="command-list">
          {filtrate.map((azione) => (
            <button
              key={azione.label}
              className="command-action"
              type="button"
              onClick={() => {
                onNavigate(azione.target);
                onClose();
              }}
            >
              <span className="command-icon"><azione.icon size={18} aria-hidden="true" /></span>
              <strong>
                {azione.label}
                <small>{azione.detail}</small>
              </strong>
              <em>{azione.badge}</em>
            </button>
          ))}
          {filtrate.length === 0 && <p className="command-empty">Nessun comando trovato.</p>}
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

function Topbar({
  onMenu,
  onCommand,
  onCompanyClick,
  onAiuto,
  onAurora,
  onEsci,
  notifiche,
  onNotifiche,
  aziendaCorrente,
}: {
  onMenu: () => void;
  onCommand: () => void;
  onCompanyClick: () => void;
  onAiuto: () => void;
  onAurora: () => void;
  onEsci: () => void;
  notifiche: number;
  onNotifiche: () => void;
  aziendaCorrente: Azienda | null;
}) {
  return (
    <header className="topbar">
      <button className="icon-button topbar-menu" type="button" aria-label="Apri menu" onClick={onMenu}>
        <Menu size={19} aria-hidden="true" />
      </button>
      <button className="soft-button company-top" type="button" onClick={onCompanyClick}>
        <span>{aziendaCorrente ? inizialiAzienda(aziendaCorrente.ragioneSociale) : <Building2 size={15} aria-hidden="true" />}</span>
        <strong>{aziendaCorrente?.ragioneSociale ?? "Scegli azienda"}</strong>
        <span className="muted">▾</span>
      </button>

      <button className="search-box" type="button" aria-label="Apri ricerca comandi" onClick={onCommand}>
        <Search size={18} aria-hidden="true" />
        <input
          id="global-search"
          name="global-search"
          placeholder="Cerca uno strumento o un'azione..."
          readOnly
          tabIndex={-1}
        />
        <kbd>⌘K</kbd>
      </button>

      <div className="topbar-spacer" />

      <button
        className="soft-button guide-top"
        type="button"
        onClick={onAurora}
        title="Aurora: controlla i tuoi dati e ti segnala cosa sistemare (in sola lettura)"
      >
        <Sparkles size={18} aria-hidden="true" />
        <strong>Aurora</strong>
      </button>
      <button className="soft-button guide-soft" type="button" onClick={onAiuto} title="Aiuto: spiega in modo semplice la pagina dove sei">
        <HelpCircle size={18} aria-hidden="true" />
        <strong>Aiuto</strong>
      </button>
      <button
        className="icon-button"
        type="button"
        aria-label="Notifiche"
        title="Scadenze da gestire (DURC, F24…)"
        onClick={onNotifiche}
      >
        <Bell size={18} aria-hidden="true" />
        {notifiche > 0 && <b>{notifiche}</b>}
      </button>
      <button className="icon-button" type="button" aria-label="Aiuto" title="Aiuto su questa pagina" onClick={onAiuto}>
        <HelpCircle size={18} aria-hidden="true" />
      </button>
      <button className="user-button" type="button">
        <span>{(emailDalToken().slice(0, 2) || "AC").toUpperCase()}</span>
        <strong>
          {emailDalToken().split("@")[0] || "Account"}
          <small>{emailDalToken() || "—"}</small>
        </strong>
        <em><ChevronDown size={15} aria-hidden="true" /></em>
      </button>
      <button className="icon-button" type="button" aria-label="Esci" title="Esci / cambia account" onClick={onEsci}>
        <LogOut size={18} aria-hidden="true" />
      </button>
    </header>
  );
}

// Riquadri colorati della home stile "sportello CAF": ogni modulo è una mattonella
// piatta con il nome del servizio; il colore aiuta a ritrovarlo a colpo d'occhio.
interface TileHome {
  key: SectionKey;
  label: string;
  colore: string;
}

const tilesPaghe: TileHome[] = [
  { key: "aziende", label: "Aziende", colore: "#43933f" },
  { key: "dipendenti", label: "Dipendenti", colore: "#33689c" },
  { key: "presenze", label: "Presenze", colore: "#279a8d" },
  { key: "cedolini", label: "Cedolini", colore: "#205e86" },
  { key: "elaborazioni", label: "Elaborazioni", colore: "#4a7fae" },
  { key: "f24", label: "F24", colore: "#dfa02b" },
  { key: "cu", label: "CU", colore: "#bd8d2a" },
  { key: "mod770", label: "770", colore: "#b03a2e" },
  { key: "redditi-pf", label: "Redditi 730", colore: "#cb4335" },
  { key: "adempimenti", label: "Adempimenti", colore: "#5d6d7e" },
  { key: "scadenzario", label: "Scadenze", colore: "#dd7726" },
  { key: "fine-anno", label: "Fine anno", colore: "#7d6b94" },
];

const tilesFisco: TileHome[] = [
  { key: "fatture", label: "Fatture", colore: "#8e2b21" },
  { key: "pagamenti", label: "Pagamenti", colore: "#1e8449" },
  { key: "firma", label: "Firma", colore: "#34495e" },
  { key: "lipe", label: "LIPE / IVA", colore: "#17a2b8" },
  { key: "iva-annuale", label: "IVA annuale", colore: "#11808f" },
  { key: "bilancio", label: "Bilancio", colore: "#7d3c98" },
  { key: "contabilita", label: "Contabilità", colore: "#796a3d" },
  { key: "irap", label: "IRAP", colore: "#56877c" },
  { key: "ires", label: "IRES", colore: "#4a69bd" },
  { key: "imu", label: "IMU", colore: "#b03a8e" },
  { key: "intrastat", label: "Intrastat", colore: "#6c7a89" },
  { key: "pratiche", label: "ComUnica", colore: "#6b8e23" },
  { key: "moduli", label: "Moduli AA7/10", colore: "#927e4a" },
  { key: "documenti", label: "Documenti", colore: "#7f8c8d" },
];

function TileGrid({ titolo, tiles, onNavigate }: {
  titolo: string;
  tiles: TileHome[];
  onNavigate: (key: SectionKey) => void;
}) {
  return (
    <section className="qw-card qw-tiles-card">
      <h2 className="qw-cluster-title">{titolo}</h2>
      <div className="qw-tile-grid">
        {tiles.map((tile) => (
          <button
            key={tile.key}
            className="qw-tile"
            type="button"
            style={{ background: tile.colore }}
            title={sectionTitle[tile.key]}
            onClick={() => onNavigate(tile.key)}
          >
            {tile.label}
          </button>
        ))}
      </div>
    </section>
  );
}

function DashboardHome({
  onNavigate,
  aziende,
  aziendaCorrente,
  onScegliAzienda,
  erroreAziende,
  sceltaRichiesta,
  promemoria,
  promemoriaInCaricamento,
  errorePromemoria,
  strumentiRecenti,
  onRiprendi,
}: {
  onNavigate: (key: SectionKey) => void;
  aziende: Azienda[];
  aziendaCorrente: Azienda | null;
  onScegliAzienda: (azienda: Azienda) => void;
  erroreAziende: string | null;
  sceltaRichiesta: boolean;
  promemoria: Promemoria[];
  promemoriaInCaricamento: boolean;
  errorePromemoria: string | null;
  strumentiRecenti: StrumentoRecente[];
  onRiprendi: (voce: StrumentoRecente) => void;
}) {
  const [ricerca, setRicerca] = useState("");
  const [kpiReali, setKpiReali] = useState<typeof kpis | null>(null);
  const [kpiInCaricamento, setKpiInCaricamento] = useState(false);
  const [erroreKpi, setErroreKpi] = useState<string | null>(null);
  const annoCorrente = new Date().getFullYear();
  const [annoSel, setAnnoSel] = useState(annoCorrente);
  const ultimaAziendaId = leggiUltimaAziendaId();

  // Anni selezionabili: dall'anno corrente indietro fino al più vecchio con
  // una scadenza registrata (minimo 3 anni, come un archivio di sportello).
  const anni = useMemo(() => {
    let minimo = annoCorrente - 3;
    for (const p of promemoria) {
      const anno = Number(p.dataScadenza.slice(0, 4));
      if (Number.isFinite(anno) && anno < minimo) minimo = anno;
    }
    const lista: number[] = [];
    for (let anno = annoCorrente; anno >= minimo; anno -= 1) lista.push(anno);
    return lista;
  }, [promemoria, annoCorrente]);

  const aziendeFiltrate = useMemo(() => {
    const testo = ricerca.trim().toLocaleLowerCase("it");
    const filtrate = testo
      ? aziende.filter((a) => [a.ragioneSociale, a.partitaIva, a.codiceFiscale, a.comune, a.provincia]
        .filter(Boolean)
        .some((valore) => String(valore).toLocaleLowerCase("it").includes(testo)))
      : aziende;
    return [...filtrate].sort((a, b) => {
      if (a.id === aziendaCorrente?.id) return -1;
      if (b.id === aziendaCorrente?.id) return 1;
      if (a.id === ultimaAziendaId) return -1;
      if (b.id === ultimaAziendaId) return 1;
      return a.ragioneSociale.localeCompare(b.ragioneSociale, "it");
    });
  }, [aziende, aziendaCorrente?.id, ricerca, ultimaAziendaId]);

  useEffect(() => {
    let valido = true;
    if (!aziendaCorrente) {
      setKpiReali(null);
      setErroreKpi(null);
      setKpiInCaricamento(false);
      return () => { valido = false; };
    }

    setKpiReali(null);
    setErroreKpi(null);
    setKpiInCaricamento(true);
    const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    void (async () => {
      try {
        const oggi = new Date();
        const anno = oggi.getFullYear();
        const mese = oggi.getMonth() + 1;
        const trimestre = Math.floor((mese - 1) / 3) + 1;
        const [dipendenti, presenze, fatture, lipe] = await Promise.all([
          api.dipendenti(aziendaCorrente.id),
          api.presenzeMese(aziendaCorrente.id, anno, mese),
          api.fatture(aziendaCorrente.id),
          api.lipeBozza(aziendaCorrente.id, anno, trimestre),
        ]);
        if (!valido) return;
        setKpiReali([
          { icon: Users, tone: "blue", label: "Dipendenti", detail: "In forza", value: String(dipendenti.length), unit: "", foot: "Anagrafiche dell'azienda" },
          { icon: FileText, tone: "green", label: "Cedolini", detail: `Elaborati ${mese}/${anno}`, value: String(presenze.elaborati), unit: `/${presenze.totale}`, foot: "Cedolini del mese" },
          { icon: ReceiptText, tone: "cyan", label: "Fatture", detail: "Emesse", value: String(fatture.length), unit: "", foot: "Fatture registrate" },
          { icon: Landmark, tone: "amber", label: "IVA dovuta", detail: `${trimestre}° trimestre`, value: euro(lipe.totaleDovuta), unit: " €", foot: "Dalle liquidazioni IVA" },
        ]);
      } catch {
        if (valido) setErroreKpi("Non riesco a caricare tutta la panoramica. I dati non vengono sostituiti con zeri finti.");
      } finally {
        if (valido) setKpiInCaricamento(false);
      }
    })();
    return () => { valido = false; };
  }, [aziendaCorrente]);

  const apriCreazioneAzienda = () => {
    onNavigate("aziende");
    window.setTimeout(() => document.getElementById("nuova-azienda-form")?.scrollIntoView({ behavior: "smooth", block: "start" }), 120);
  };

  const recenti = strumentiRecenti
    .map((voce) => ({ voce, azienda: aziende.find((a) => a.id === voce.aziendaId) }))
    .filter((riga): riga is { voce: StrumentoRecente; azienda: Azienda } => Boolean(riga.azienda));

  return (
    <div className="qw-home">
      <section className="qw-band" aria-label="Anno di lavoro">
        <span className="qw-band-label">Anno</span>
        <nav className="qw-anni">
          {anni.map((anno) => (
            <button
              key={anno}
              className={`qw-anno ${anno === annoSel ? "attivo" : ""}`}
              type="button"
              aria-pressed={anno === annoSel}
              onClick={() => setAnnoSel(anno)}
            >
              {anno}
            </button>
          ))}
        </nav>
        <span className="qw-band-azienda" title="Azienda su cui stai lavorando">
          <Building2 size={14} aria-hidden="true" />
          {aziendaCorrente?.ragioneSociale ?? "Nessuna azienda scelta"}
        </span>
      </section>

      <section className="qw-card company-start-card" aria-labelledby="titolo-scelta-azienda">
          <div className="company-start-head">
            <div>
              <span className="home-step-label">Azienda cliente</span>
              <h1 id="titolo-scelta-azienda">
                {aziende.length === 0
                  ? "Crea la prima azienda"
                  : aziendaCorrente
                    ? "Azienda di lavoro"
                    : "Scegli l'azienda su cui lavorare"}
              </h1>
              <p>
                {aziendaCorrente
                  ? `Ogni numero e azione qui sotto riguarda ${aziendaCorrente.ragioneSociale}.`
                  : "Prima scegli il cliente: così cedolini, fatture e adempimenti non finiscono sull'azienda sbagliata."}
              </p>
            </div>
            <button className={`btn ${aziende.length === 0 ? "primary" : "secondary"}`} type="button" onClick={apriCreazioneAzienda}>
              <Building2 size={17} aria-hidden="true" />
              {aziende.length === 0 ? "Crea azienda" : "Crea nuova azienda"}
            </button>
          </div>

          {sceltaRichiesta && !aziendaCorrente && (
            <p className="company-choice-alert" role="alert">
              Prima scegli un'azienda qui sotto; poi aprirò lo strumento richiesto.
            </p>
          )}
          {erroreAziende && <p className="company-choice-alert error" role="alert">{erroreAziende}</p>}

          {aziende.length === 0 ? (
            <div className="home-empty-company">
              <Building2 size={24} aria-hidden="true" />
              <div>
                <strong>Nessun cliente registrato</strong>
                <span>Premi “Crea azienda” e inserisci i dati reali. Nessuna azienda viene creata automaticamente.</span>
              </div>
            </div>
          ) : (
            <>
              <label className="home-company-search" htmlFor="cerca-azienda-home">
                <Search size={18} aria-hidden="true" />
                <input
                  id="cerca-azienda-home"
                  aria-label="Cerca azienda cliente"
                  value={ricerca}
                  onChange={(e) => setRicerca(e.target.value)}
                  placeholder="Cerca per nome, partita IVA, codice fiscale o comune"
                  autoComplete="off"
                />
              </label>
              {aziendeFiltrate.length === 0 ? (
                <div className="home-company-not-found">Nessuna azienda trovata. Controlla il testo oppure creane una nuova.</div>
              ) : (
                <div className="home-company-list" aria-label="Aziende disponibili">
                  {aziendeFiltrate.slice(0, 6).map((azienda) => {
                    const selezionata = azienda.id === aziendaCorrente?.id;
                    const ultima = !selezionata && azienda.id === ultimaAziendaId;
                    return (
                      <button
                        key={azienda.id}
                        className={`home-company-option ${selezionata ? "selected" : ""}`}
                        type="button"
                        aria-pressed={selezionata}
                        onClick={() => onScegliAzienda(azienda)}
                      >
                        <span className="home-company-avatar">{inizialiAzienda(azienda.ragioneSociale)}</span>
                        <span className="home-company-copy">
                          <strong>{azienda.ragioneSociale}</strong>
                          <small>{[
                            azienda.partitaIva ? `P.IVA ${azienda.partitaIva}` : null,
                            azienda.codiceFiscale ? `CF ${azienda.codiceFiscale}` : null,
                            azienda.comune ? `${azienda.comune}${azienda.provincia ? ` (${azienda.provincia})` : ""}` : null,
                          ].filter(Boolean).join(" · ") || "Anagrafica azienda"}</small>
                        </span>
                        <em>{selezionata ? "Scelta" : ultima ? "Ultima usata" : "Scegli"}</em>
                      </button>
                    );
                  })}
                </div>
              )}
              {aziendeFiltrate.length > 6 && (
                <p className="home-company-count">Mostrate 6 aziende su {aziendeFiltrate.length}. Scrivi nel campo per restringere la ricerca.</p>
              )}
            </>
          )}
        </section>

      <div className="qw-tiles-wrap">
        <TileGrid titolo="Paghe e personale" tiles={tilesPaghe} onNavigate={onNavigate} />
        <TileGrid titolo="Fisco e contabilità" tiles={tilesFisco} onNavigate={onNavigate} />
      </div>

      {recenti.length > 0 && (
        <div className="qw-recenti" aria-label="Riprendi il lavoro">
          <span className="qw-recenti-label">Riprendi:</span>
          {recenti.map(({ voce, azienda }) => {
            const item = itemPerKey(voce.sezione);
            return (
              <button
                className="qw-recente"
                type="button"
                key={`${voce.sezione}-${voce.aziendaId}`}
                onClick={() => onRiprendi(voce)}
              >
                <item.icon size={14} aria-hidden="true" />
                <strong>{item.label}</strong>
                <small>{azienda.ragioneSociale} · {tempoRecente(voce.usatoAlle)}</small>
              </button>
            );
          })}
        </div>
      )}

      <ScadenzePanel
        anno={annoSel}
        promemoria={promemoria}
        inCaricamento={promemoriaInCaricamento}
        errore={errorePromemoria}
        onNavigate={onNavigate}
      />

      <div className="qw-panels">
        <section className="qw-card qw-panel">
          <div className="qw-panel-head">
            <ClipboardList size={15} aria-hidden="true" />
            Sommario
          </div>
          {!aziendaCorrente ? (
            <p className="muto qw-panel-empty">Scegli un'azienda qui sopra per vedere i suoi numeri.</p>
          ) : kpiInCaricamento ? (
            <p className="muto qw-panel-empty" aria-live="polite">Aggiornamento dati di {aziendaCorrente.ragioneSociale}…</p>
          ) : erroreKpi ? (
            <p className="company-choice-alert error" role="alert">{erroreKpi}</p>
          ) : kpiReali ? (
            <div>
              <p className="qw-panel-scope">{aziendaCorrente.ragioneSociale}</p>
              {kpiReali.map((kpi) => (
                <div className="qw-som-riga" key={kpi.label}>
                  <span>{kpi.label}<small>{kpi.detail}</small></span>
                  <strong className="num">{kpi.value}{kpi.unit}</strong>
                </div>
              ))}
            </div>
          ) : null}
        </section>

        <OrologioCalendarioPanel promemoria={promemoria} />
        <PostaPanel onNavigate={onNavigate} />
      </div>
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
function OrologioCalendarioPanel({ promemoria }: { promemoria: Promemoria[] }) {
  const [adesso, setAdesso] = useState(() => new Date());
  useEffect(() => {
    const timer = window.setInterval(() => setAdesso(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  const anno = adesso.getFullYear();
  const mese = adesso.getMonth();
  const prefisso = `${anno}-${String(mese + 1).padStart(2, "0")}-`;
  const giorniConScadenze = new Set(
    promemoria
      .filter((p) => p.dataScadenza.startsWith(prefisso))
      .map((p) => Number(p.dataScadenza.slice(8, 10))),
  );
  const giorniNelMese = new Date(anno, mese + 1, 0).getDate();
  // Lunedì come primo giorno della settimana (uso italiano).
  const scarto = (new Date(anno, mese, 1).getDay() + 6) % 7;
  const celle: (number | null)[] = [
    ...Array.from({ length: scarto }, () => null),
    ...Array.from({ length: giorniNelMese }, (_, i) => i + 1),
  ];

  return (
    <section className="qw-card qw-panel">
      <div className="qw-panel-head">
        <Clock size={15} aria-hidden="true" />
        Orologio e calendario
      </div>
      <div className="qw-orologio">
        <div className="qw-ora num">{adesso.toLocaleTimeString("it-IT")}</div>
        <div className="qw-data">{adesso.toLocaleDateString("it-IT", { weekday: "long", day: "numeric", month: "long", year: "numeric" })}</div>
      </div>
      <div className="qw-cal-grid">
        {["Lu", "Ma", "Me", "Gi", "Ve", "Sa", "Do"].map((giorno) => (
          <span className="qw-cal-dow" key={giorno}>{giorno}</span>
        ))}
        {celle.map((giorno, indice) => (
          <span
            key={indice}
            className={`qw-cal-day ${giorno === adesso.getDate() ? "oggi" : ""}`}
          >
            {giorno ?? ""}
            {giorno !== null && giorniConScadenze.has(giorno) && <i className="qw-cal-dot" aria-label="Scadenze nel giorno" />}
          </span>
        ))}
      </div>
    </section>
  );
}

// Riepilogo Posta nella dashboard: non letti e proposte AI da confermare, con accesso rapido
// alla pagina completa. L'euristica/AI propone soltanto: qui si vede solo il conteggio.
function PostaPanel({ onNavigate }: { onNavigate: (key: SectionKey) => void }) {
  const [stato, setStato] = useState<"carico" | "senzaCaselle" | "pronto" | "errore">("carico");
  const [nonLetti, setNonLetti] = useState(0);
  const [daConfermare, setDaConfermare] = useState(0);

  useEffect(() => {
    let vivo = true;
    (async () => {
      try {
        const caselle = await api.caselleEmail();
        if (!vivo) return;
        if (caselle.length === 0) {
          setStato("senzaCaselle");
          return;
        }
        const [nonLettiMsg, proposte] = await Promise.all([
          api.messaggiEmail({ letto: false }),
          api.messaggiEmail({ soloProposte: true }),
        ]);
        if (!vivo) return;
        setNonLetti(nonLettiMsg.length);
        setDaConfermare(proposte.length);
        setStato("pronto");
      } catch {
        if (vivo) setStato("errore");
      }
    })();
    return () => { vivo = false; };
  }, []);

  return (
    <section className="qw-card qw-panel">
      <div className="qw-panel-head">
        <Mail size={15} aria-hidden="true" />
        Posta
      </div>
      {stato === "carico" && <p className="muto qw-panel-empty" aria-live="polite">Aggiornamento posta…</p>}
      {stato === "errore" && <p className="company-choice-alert error" role="alert">Non riesco a caricare la posta.</p>}
      {stato === "senzaCaselle" && (
        <>
          <p className="muto qw-panel-empty">Nessuna casella email o PEC collegata.</p>
          <button className="btn secondary" type="button" onClick={() => onNavigate("posta")}>
            Collega una casella
          </button>
        </>
      )}
      {stato === "pronto" && (
        <>
          <div className="qw-som-riga">
            <span>Non letti</span>
            <strong className="num">{nonLetti}</strong>
          </div>
          <div className="qw-som-riga">
            <span>Da confermare<small>Proposte AI in attesa</small></span>
            <strong className="num">{daConfermare}</strong>
          </div>
          <button className="btn secondary" type="button" onClick={() => onNavigate("posta")}>
            Apri Posta
          </button>
        </>
      )}
    </section>
  );
}

function ModuleScreen({
  active,
  onNavigate,
  rapportoSel,
  onApriDipendente,
}: {
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
  rapportoSel: string | null;
  onApriDipendente: (rapportoId: string) => void;
}) {
  if (active === "aziende") {
    return (
      <div className="module-layout wide aziende-page-layout">
        <div>
          <Aziende />
        </div>
      </div>
    );
  }

  if (active === "cedolini") return <Cedolini onTornaPresenze={() => onNavigate("presenze")} />;

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
