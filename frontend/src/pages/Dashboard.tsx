import { useEffect, useMemo, useState } from "react";
import { api } from "../api";
import { useAuth } from "../auth";
import { getRuolo, getToken } from "../api";

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
import { Scadenzario } from "./Scadenzario";
import { CollegaProvider } from "./CollegaProvider";
import { CaricaUnilav } from "./CaricaUnilav";
import { Studi } from "./Studi";
import { aiutoPerSezione } from "../guida";

type SectionKey =
  | "dashboard"
  | "aziende"
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
  icon: string;
  label: string;
}

const primaryNav: NavItem[] = [
  { key: "dashboard", icon: "🏠", label: "Dashboard" },
  { key: "aziende", icon: "🏢", label: "Aziende" },
  { key: "dipendenti", icon: "👥", label: "Dipendenti" },
  { key: "presenze", icon: "🗓️", label: "Presenze" },
  { key: "cedolini", icon: "📄", label: "Cedolini" },
  { key: "adempimenti", icon: "📨", label: "Adempimenti" },
  { key: "scadenzario", icon: "⏰", label: "Scadenzario" },
  { key: "fatture", icon: "🧾", label: "Fatturazione" },
  { key: "pagamenti", icon: "€", label: "Pagamenti clienti" },
  { key: "firma", icon: "🔏", label: "Firma digitale" },
  { key: "contabilita", icon: "📒", label: "Contabilità" },
  { key: "lipe", icon: "📊", label: "LIPE / IVA" },
  { key: "iva-annuale", icon: "🧾", label: "IVA annuale" },
  { key: "bilancio", icon: "⚖️", label: "Bilancio" },
  { key: "documenti", icon: "📁", label: "Documenti" },
];

const payrollNav: NavItem[] = [
  { key: "elaborazioni", icon: "🧮", label: "Elaborazioni" },
  { key: "f24", icon: "🏛️", label: "F24" },
  { key: "cu", icon: "📑", label: "CU / CUD" },
  { key: "mod770", icon: "🧾", label: "Modello 770" },
  { key: "redditi-pf", icon: "🧑‍💼", label: "Redditi PF / 730" },
  { key: "irap", icon: "🏭", label: "IRAP" },
  { key: "ires", icon: "🏢", label: "IRES società" },
  { key: "imu", icon: "🏠", label: "IMU" },
  { key: "intrastat", icon: "🌍", label: "Intrastat" },
];

const adminNav: NavItem[] = [
  { key: "collega-sdi", icon: "🔌", label: "Collega Fatturazione" },
  { key: "impostazioni", icon: "⚙️", label: "Impostazioni" },
  { key: "utenti", icon: "🔐", label: "Utenti e permessi" },
];

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

const kpis = [
  {
    icon: "📄",
    tone: "blue",
    label: "Buste paga",
    detail: "Cedolini",
    value: "0",
    unit: "",
    foot: "Nessun cedolino ancora",
  },
  {
    icon: "📑",
    tone: "green",
    label: "CU / CUD",
    detail: "CU pronte",
    value: "0",
    unit: "",
    foot: "Nessuna CU ancora",
  },
  {
    icon: "🏛️",
    tone: "amber",
    label: "F24",
    detail: "F24 da versare",
    value: "0",
    unit: ",00 €",
    foot: "Nessun F24 ancora",
  },
  {
    icon: "🧾",
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
  const [active, setActive] = useState<SectionKey>("dashboard");
  const [rapportoSel, setRapportoSel] = useState<string | null>(null);
  const [aiutoAperto, setAiutoAperto] = useState(false);
  const apriDipendente = (rapportoId: string) => { setRapportoSel(rapportoId); setActive("dipendente"); };

  const navActive = navActiveFor[active] ?? active;
  const crumbs = useMemo(() => crumbsFor[active] ?? ["Home", sectionTitle[active]], [active]);

  // Voce "Studi" visibile solo al super-admin di piattaforma.
  const adminNavVisibile = getRuolo() === "superadmin"
    ? [...adminNav, { key: "studi" as SectionKey, icon: "🏢", label: "Studi (admin)" }]
    : adminNav;

  return (
    <div className="app-shell">
      <aside className="sidebar glass-panel">
        <div className="brand">
          <div className="brand-mark">P</div>
          <div>
            <strong>PaghePro</strong>
            <span>Studio</span>
          </div>
        </div>

        <nav className="nav-scroll" aria-label="Navigazione principale">
          <NavGroup items={primaryNav} active={navActive} onChange={setActive} />
          <NavGroup title="Elaborazione paghe" items={payrollNav} active={navActive} onChange={setActive} />
          <NavGroup title="Amministrazione" items={adminNavVisibile} active={navActive} onChange={setActive} />
        </nav>

        <button className="company-switch" type="button" onClick={() => setActive("aziende")}>
          <span className="company-icon">🏢</span>
          <span>
            <strong>Il tuo studio</strong>
            <small>Cambia azienda</small>
          </span>
          <span className="chevron">›</span>
        </button>
      </aside>

      <div className="workspace">
        <Topbar onCompanyClick={() => setActive("aziende")} onAiuto={() => setAiutoAperto(true)} onEsci={esci} />

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
            <DashboardHome onNavigate={setActive} />
          ) : (
            <ModuleScreen active={active} onNavigate={setActive} rapportoSel={rapportoSel} onApriDipendente={apriDipendente} />
          )}
        </main>
      </div>

      {aiutoAperto && (
        <AiutoPanel sezione={navActive} onChiudi={() => setAiutoAperto(false)} />
      )}
    </div>
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
          <span>{item.icon}</span>
          {item.label}
        </button>
      ))}
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

function Topbar({ onCompanyClick, onAiuto, onEsci }: { onCompanyClick: () => void; onAiuto: () => void; onEsci: () => void }) {
  return (
    <header className="topbar">
      <button className="soft-button company-top" type="button" onClick={onCompanyClick}>
        <span>🏢</span>
        <strong>Il tuo studio</strong>
        <span className="muted">▾</span>
      </button>

      <label className="search-box" aria-label="Cerca">
        <span>⌕</span>
        <input placeholder="Cerca dipendenti, cedolini, documenti, adempimenti..." />
        <kbd>⌘K</kbd>
      </label>

      <div className="topbar-spacer" />

      <button className="soft-button guide-top" type="button" onClick={onAiuto} title="Aiuto: spiega in modo semplice la pagina dove sei">
        <span>❓</span>
        <strong>Aiuto</strong>
      </button>
      <button className="icon-button" type="button" aria-label="Notifiche">
        <span>🔔</span>
        <b>3</b>
      </button>
      <button className="icon-button" type="button" aria-label="Aiuto" title="Aiuto su questa pagina" onClick={onAiuto}>
        ?
      </button>
      <button className="user-button" type="button">
        <span>{(emailDalToken().slice(0, 2) || "AC").toUpperCase()}</span>
        <strong>
          {emailDalToken().split("@")[0] || "Account"}
          <small>{emailDalToken() || "—"}</small>
        </strong>
        <em>⌄</em>
      </button>
      <button className="icon-button" type="button" aria-label="Esci" title="Esci / cambia account" onClick={onEsci}>
        ⎋
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
          { icon: "👥", tone: "blue", label: "Dipendenti", detail: "In forza", value: String(dip.length), unit: "", foot: "Anagrafiche reali" },
          { icon: "📄", tone: "green", label: "Cedolini", detail: `Elaborati ${mese}/${anno}`, value: String(presenze.elaborati), unit: `/${presenze.totale}`, foot: "Cedolini del mese" },
          { icon: "🧾", tone: "cyan", label: "Fatture", detail: "Emesse", value: String(fatture.length), unit: "", foot: "Fatture registrate" },
          { icon: "🏛️", tone: "amber", label: "IVA dovuta", detail: `${trimestre}° trimestre`, value: euro(lipe.totaleDovuta), unit: " €", foot: "Dalle liquidazioni IVA" },
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
            <p>Panoramica attività e adempimenti · dati reali</p>
          </div>
          <button className="btn primary" type="button" onClick={() => onNavigate("elaborazioni")}>
            + Nuova attività
          </button>
        </div>

        <div className="kpi-grid">
          {kpiReali.map((kpi) => (
            <article key={kpi.label} className="glass-card kpi-card">
              <div className="kpi-head">
                <span className={`tone-icon ${kpi.tone}`}>{kpi.icon}</span>
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
          <Aziende />
        </div>
        <ModuleRail
          title="Come iniziare"
          rows={[
            ["1", "Carica una visura (PDF o foto)"],
            ["2", "Controlla i dati estratti"],
            ["3", "Aggiungi l'azienda"],
          ]}
        />
      </div>
    );
  }

  if (active === "cedolini") return <Cedolini />;

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
  if (active === "elaborazioni") return <Elaborazione />;
  if (active === "adempimenti") return <DeadlinesPanel onNavigate={onNavigate} />;
  if (active === "fatture") return <Fatture />;
  if (active === "scadenzario") return <Scadenzario />;
  if (active === "pagamenti") return <Pagamenti />;
  if (active === "firma") return <Firma />;
  if (active === "contabilita") return <AccountingPanel />;
  if (active === "lipe") return <Lipe />;
  if (active === "iva-annuale") return <IvaAnnuale />;
  if (active === "bilancio") return <Bilancio />;
  if (active === "documenti") return <DocumentsPanel onNavigate={onNavigate} />;
  if (active === "f24") return <F24Panel />;
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
