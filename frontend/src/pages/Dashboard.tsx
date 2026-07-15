import { useEffect, useMemo, useState } from "react";
import {
  AlarmClock,
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
  Factory,
  FileSignature,
  FileText,
  Files,
  FolderKanban,
  FolderOpen,
  Globe2,
  HelpCircle,
  House,
  Landmark,
  LayoutDashboard,
  LogOut,
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
import { AZIENDA_CORRENTE_CAMBIATA, leggiAziendaCorrenteId } from "../aziendaCorrente";

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
import { Aziende, AziendeLaterale } from "./Aziende";
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
import { aiutoPerSezione } from "../guida";

type SectionKey =
  | "dashboard"
  | "aziende"
  | "pratiche"
  | "moduli"
  | "dipendenti"
  | "dipendente"
  | "presenze"
  | "cedolini"
  | "calcolo"
  | "adempimenti"
  | "scadenzario"
  | "fatture"
  | "pagamenti"
  | "firma"
  | "contabilita"
  | "lipe"
  | "iva-annuale"
  | "bilancio"
  | "documenti"
  | "elaborazioni"
  | "f24"
  | "fine-anno"
  | "cu"
  | "mod770"
  | "redditi-pf"
  | "irap"
  | "ires"
  | "imu"
  | "intrastat"
  | "impostazioni"
  | "collega-sdi"
  | "utenti"
  | "studi";

interface NavItem {
  key: SectionKey;
  icon: LucideIcon;
  label: string;
}

const primaryNav: NavItem[] = [
  { key: "dashboard", icon: LayoutDashboard, label: "Dashboard" },
  { key: "aziende", icon: Building2, label: "Aziende" },
  { key: "pratiche", icon: FolderKanban, label: "Pratiche ComUnica" },
  { key: "moduli", icon: ClipboardList, label: "Moduli ufficiali" },
  { key: "dipendenti", icon: Users, label: "Dipendenti" },
  { key: "presenze", icon: CalendarDays, label: "Presenze" },
  { key: "cedolini", icon: FileText, label: "Cedolini" },
  { key: "adempimenti", icon: Send, label: "Adempimenti" },
  { key: "scadenzario", icon: AlarmClock, label: "Scadenzario" },
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
  dashboard: ["Home", "Dashboard"],
  dipendente: ["Home", "Dipendenti", "Scheda"],
  presenze: ["Home", "Presenze", "Cartellino mensile"],
  elaborazioni: ["Home", "Cedolini", "Elaborazione"],
  cedolini: ["Home", "Cedolini"],
  calcolo: ["Home", "Cedolini", "Calcolo cedolino"],
  pagamenti: ["Home", "Pagamenti clienti"],
};

const sectionTitle: Record<SectionKey, string> = {
  dashboard: "Dashboard",
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
  studi: "Studi — super-admin",
};

function sezioneDaHash(): SectionKey {
  const candidata = decodeURIComponent(window.location.hash.slice(1)).trim().toLowerCase();
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

// Vuoto: i dati reali del flusso compariranno qui man mano che lavori (niente esempi finti).
const workflow: { label: string; value: string; state: string; tone: string; icon: string }[] = [];

// Vuoto: i documenti/adempimenti reali compariranno qui (niente esempi finti).
const documentQueue: {
  tag: string; tone: string; title: string; ref: string; due: string; state: string; stateTone: string;
}[] = [];

export function Dashboard() {
  const { esci } = useAuth();
  const [active, setActive] = useState<SectionKey>(sezioneDaHash);
  const [rapportoSel, setRapportoSel] = useState<string | null>(null);
  const [aiutoAperto, setAiutoAperto] = useState(false);
  const [slidebarAperta, setSlidebarAperta] = useState(false);
  const [commandAperta, setCommandAperta] = useState(false);
  const [aziendaCorrente, setAziendaCorrente] = useState<Azienda | null>(null);
  const apriDipendente = (rapportoId: string) => { setRapportoSel(rapportoId); setActive("dipendente"); };

  useEffect(() => {
    let vivo = true;
    async function caricaAziendaCorrente() {
      try {
        const elenco = await api.aziende();
        if (!vivo) return;
        const idCorrente = leggiAziendaCorrenteId();
        const scelta = (idCorrente ? elenco.find((a) => a.id === idCorrente) : undefined) ?? elenco[0] ?? null;
        setAziendaCorrente(scelta);
      } catch {
        if (vivo) setAziendaCorrente(null);
      }
    }

    const aggiorna = () => { void caricaAziendaCorrente(); };
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
  const [avvisoDurcNascosto, setAvvisoDurcNascosto] = useState(
    () => sessionStorage.getItem("avvisoDurcNascosto") === "1",
  );
  useEffect(() => {
    api.scadenzario().then(setPromemoria).catch(() => { /* offline o non pronto: nessun avviso */ });
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
  const vaiA = (key: SectionKey) => {
    setActive(key);
    if (window.location.hash !== `#${key}`) window.location.hash = key;
    setSlidebarAperta(false);
  };

  useEffect(() => {
    const aggiornaDaHash = () => setActive(sezioneDaHash());
    window.addEventListener("hashchange", aggiornaDaHash);
    return () => window.removeEventListener("hashchange", aggiornaDaHash);
  }, []);

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
  const adminNavVisibile = getRuolo() === "superadmin"
    ? [...adminNav, { key: "studi" as SectionKey, icon: Building2, label: "Studi (admin)" }]
    : adminNav;

  return (
    <div className="app-shell">
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

        <button className="company-switch" type="button" onClick={() => vaiA("aziende")}>
          <span className="company-icon">{aziendaCorrente ? inizialiAzienda(aziendaCorrente.ragioneSociale) : "🏢"}</span>
          <span>
            <strong>{aziendaCorrente?.ragioneSociale ?? "Il tuo studio"}</strong>
            <small>{aziendaCorrente ? "Azienda selezionata" : "Cambia azienda"}</small>
          </span>
          <span className="chevron">›</span>
        </button>
      </aside>

      <div className="workspace">
        <Topbar
          onMenu={() => setSlidebarAperta(true)}
          onCommand={() => setCommandAperta(true)}
          onCompanyClick={() => vaiA("aziende")}
          onAiuto={() => setAiutoAperto(true)}
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

          {active === "dashboard" ? (
            <DashboardHome onNavigate={vaiA} />
          ) : (
            <ModuleScreen active={active} onNavigate={vaiA} rapportoSel={rapportoSel} onApriDipendente={apriDipendente} />
          )}
        </main>
      </div>

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
            placeholder="Cerca comando, azienda, dipendente o scadenza"
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
        <a className="btn primary" href={DURC_ONLINE_URL} target="_blank" rel="noreferrer">
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
  onEsci,
  notifiche,
  onNotifiche,
  aziendaCorrente,
}: {
  onMenu: () => void;
  onCommand: () => void;
  onCompanyClick: () => void;
  onAiuto: () => void;
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
        <span>{aziendaCorrente ? inizialiAzienda(aziendaCorrente.ragioneSociale) : "🏢"}</span>
        <strong>{aziendaCorrente?.ragioneSociale ?? "Il tuo studio"}</strong>
        <span className="muted">▾</span>
      </button>

      <button className="search-box" type="button" aria-label="Apri ricerca comandi" onClick={onCommand}>
        <Search size={18} aria-hidden="true" />
        <input
          id="global-search"
          name="global-search"
          placeholder="Cerca dipendenti, cedolini, documenti, adempimenti..."
          readOnly
          tabIndex={-1}
        />
        <kbd>⌘K</kbd>
      </button>

      <div className="topbar-spacer" />

      <button className="soft-button guide-top" type="button" onClick={onAiuto} title="Aiuto: spiega in modo semplice la pagina dove sei">
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

function DashboardHome({ onNavigate }: { onNavigate: (key: SectionKey) => void }) {
  const [kpiReali, setKpiReali] = useState(kpis);

  useEffect(() => {
    const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    (async () => {
      try {
        const aziende = await api.aziende();
        const az = aziende[0];
        if (!az) return;
        const oggi = new Date();
        const anno = oggi.getFullYear();
        const mese = oggi.getMonth() + 1;
        const trimestre = Math.floor((mese - 1) / 3) + 1;
        const [dip, presenze, fatture, lipe] = await Promise.all([
          api.dipendenti(az.id),
          api.presenzeMese(az.id, anno, mese),
          api.fatture(az.id),
          api.lipeBozza(az.id, anno, trimestre),
        ]);
        setKpiReali([
          { icon: Users, tone: "blue", label: "Dipendenti", detail: "In forza", value: String(dip.length), unit: "", foot: "Anagrafiche reali" },
          { icon: FileText, tone: "green", label: "Cedolini", detail: `Elaborati ${mese}/${anno}`, value: String(presenze.elaborati), unit: `/${presenze.totale}`, foot: "Cedolini del mese" },
          { icon: ReceiptText, tone: "cyan", label: "Fatture", detail: "Emesse", value: String(fatture.length), unit: "", foot: "Fatture registrate" },
          { icon: Landmark, tone: "amber", label: "IVA dovuta", detail: `${trimestre}° trimestre`, value: euro(lipe.totaleDovuta), unit: " €", foot: "Dalle liquidazioni IVA" },
        ]);
      } catch { /* mantiene i valori iniziali se l'API non risponde */ }
    })();
  }, []);

  return (
    <div className="dashboard-grid">
      <section className="dashboard-main">
        <div className="page-heading">
          <div>
            <h1>Centro operativo</h1>
            <p>Panoramica lavoro paghe e adempimenti · dati reali</p>
          </div>
          <button className="btn primary" type="button" onClick={() => onNavigate("elaborazioni")}>
            Elabora cedolini
          </button>
        </div>

        <section className="ai-strip glass-card">
          <div>
            <span className="ai-chip">AI-first</span>
            <h2>Controllo risparmio fiscale</h2>
            <p>IVA, costi deducibili, documenti mancanti e scadenze da validare.</p>
          </div>
          <div className="ai-actions">
            <button className="btn primary" type="button" onClick={() => onNavigate("documenti")}>Carica documento</button>
            <button className="btn secondary" type="button" onClick={() => onNavigate("contabilita")}>Consigli fiscali</button>
          </div>
        </section>

        <div className="kpi-grid">
          {kpiReali.map((kpi) => (
            <article key={kpi.label} className="glass-card kpi-card">
              <div className="kpi-head">
                <span className={`tone-icon ${kpi.tone}`}><kpi.icon size={19} strokeWidth={2.1} aria-hidden="true" /></span>
                <div>
                  <strong>{kpi.label}</strong>
                  <small>{kpi.detail}</small>
                </div>
              </div>
              <div className={`kpi-value ${kpi.tone}`}>
                {kpi.value}
                <span>{kpi.unit}</span>
              </div>
              <p>{kpi.foot}</p>
            </article>
          ))}
        </div>

        <section className="glass-card work-card">
          <div className="card-title-row">
            <div>
              <h2>Flusso di lavoro mensile</h2>
              <p>Stato avanzamento processi · Giugno 2026</p>
            </div>
            <button className="link-button" type="button" onClick={() => onNavigate("presenze")}>
              Calendario
            </button>
          </div>
          {workflow.length === 0 && (
            <p className="muto" style={{ marginTop: 14 }}>
              Nessun processo in corso. Il flusso si popolerà man mano che lavori (presenze, cedolini, F24…).
            </p>
          )}
          <div className="workflow">
            {workflow.length === 0 ? null : (
              workflow.map((step, index) => (
                <div key={step.label} className="workflow-step">
                  {index < workflow.length - 1 && <span className="workflow-line" />}
                  <span className={`workflow-dot ${step.tone}`}>{step.icon}</span>
                  <strong>{step.label}</strong>
                  <small>{step.value}</small>
                  <em className={`status-pill ${step.tone}`}>{step.state}</em>
                </div>
              ))
            )}
          </div>
        </section>

        <section className="glass-card queue-card">
          <div className="card-title-row">
            <h2>Coda documenti e adempimenti</h2>
            <button className="link-button" type="button" onClick={() => onNavigate("documenti")}>
              Vai a Documenti
            </button>
          </div>
          <div className="tabs-line">
            <button className="active" type="button">
              Tutti
            </button>
            <button type="button">In attesa</button>
            <button type="button">In lavorazione</button>
            <button type="button">Bloccati</button>
          </div>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Tipo</th>
                  <th>Descrizione</th>
                  <th>Riferimento</th>
                  <th>Scadenza</th>
                  <th>Stato</th>
                  <th className="right">Azioni</th>
                </tr>
              </thead>
              <tbody>
                {documentQueue.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="muto" style={{ padding: 14 }}>
                      Nessun documento ancora. I tuoi adempimenti reali compariranno qui.
                    </td>
                  </tr>
                ) : (
                  documentQueue.map((row) => (
                    <tr key={`${row.tag}-${row.title}`}>
                      <td>
                        <span className={`tag ${row.tone}`}>{row.tag}</span>
                      </td>
                      <td>
                        <strong>{row.title}</strong>
                      </td>
                      <td>{row.ref}</td>
                      <td className="num">{row.due}</td>
                      <td>
                        <span className={`status-pill ${row.stateTone}`}>{row.state}</span>
                      </td>
                      <td className="right">
                        <button className="mini-action" type="button">
                          Apri
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      </section>

      <DashboardRail onNavigate={onNavigate} />
    </div>
  );
}

function DashboardRail({ onNavigate }: { onNavigate: (key: SectionKey) => void }) {
  // Vuoti: compariranno i dati reali (niente esempi finti).
  const deadlines: string[][] = [];
  const receipts: string[][] = [];

  return (
    <aside className="dashboard-rail">
      <section className="glass-card side-card">
        <div className="side-card-head">
          <strong>Errori da correggere</strong>
          <span className="count">0</span>
        </div>
        <p className="muto" style={{ fontSize: 13, margin: "6px 2px" }}>
          Nessun errore. I controlli sui tuoi dati compariranno qui.
        </p>
      </section>

      <section className="glass-card side-card">
        <div className="side-card-head">
          <strong>Scadenze</strong>
        </div>
        {deadlines.length === 0 ? (
          <p className="muto" style={{ fontSize: 13, margin: "6px 2px" }}>Nessuna scadenza imminente.</p>
        ) : (
          deadlines.map(([day, month, title, meta]) => (
            <div className="deadline-row" key={title}>
              <span>
                <b>{day}</b>
                <small>{month}</small>
              </span>
              <strong>
                {title}
                <small>{meta}</small>
              </strong>
            </div>
          ))
        )}
        <button className="rail-cta" type="button" onClick={() => onNavigate("adempimenti")}>
          Vedi tutte le scadenze
        </button>
      </section>

      <section className="glass-card side-card">
        <div className="side-card-head">
          <strong>Ricevute recenti</strong>
        </div>
        {receipts.length === 0 ? (
          <p className="muto" style={{ fontSize: 13, margin: "6px 2px" }}>Ancora nessuna ricevuta.</p>
        ) : (
          receipts.map(([title, meta, time]) => (
            <div className="receipt-row" key={title}>
              <span>✓</span>
              <strong>
                {title}
                <small>{meta}</small>
              </strong>
              <em>{time}</em>
            </div>
          ))
        )}
      </section>
    </aside>
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
  const [aziendeRefreshKey, setAziendeRefreshKey] = useState(0);

  if (active === "aziende") {
    return (
      <div className="module-layout">
        <div>
          <div className="page-heading">
            <div>
              <h1>Aziende clienti</h1>
              <p>Anagrafiche, sedi e dati fiscali del tuo studio</p>
            </div>
          </div>
          <Aziende onAziendeCambiate={() => setAziendeRefreshKey((v) => v + 1)} />
        </div>
        <AziendeLaterale refreshKey={aziendeRefreshKey} onNavigate={onNavigate} />
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
  if (active === "dipendente") return <Dipendente rapportoId={rapportoSel} onPayslip={() => onNavigate("cedolini")} />;
  if (active === "presenze") return <Presenze />;
  if (active === "elaborazioni") return <Elaborazione onApriSezione={onNavigate} />;
  if (active === "adempimenti") return <DeadlinesPanel onNavigate={onNavigate} />;
  if (active === "fatture") return <Fatture />;
  if (active === "pratiche") return <Pratiche />;
  if (active === "moduli") return <Moduli />;
  if (active === "scadenzario") return <Scadenzario />;
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
