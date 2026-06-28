import { useEffect, useMemo, useState } from "react";
import { api } from "../api";
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
  | "utenti";

interface NavItem {
  key: SectionKey;
  icon: string;
  label: string;
}

const primaryNav: NavItem[] = [
  { key: "dashboard", icon: "🏠", label: "Dashboard" },
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
  cedolini: ["Home", "Cedolini", "Giugno 2026", "Mario Bianchi"],
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
};

const kpis = [
  {
    icon: "📄",
    tone: "blue",
    label: "Buste paga",
    detail: "Cedolini",
    value: "38",
    unit: "/42",
    foot: "90,48% elaborati",
  },
  {
    icon: "📑",
    tone: "green",
    label: "CU / CUD",
    detail: "CU pronte",
    value: "42",
    unit: "",
    foot: "Pronte per invio",
  },
  {
    icon: "🏛️",
    tone: "amber",
    label: "F24",
    detail: "F24 da versare",
    value: "18.420",
    unit: ",00 €",
    foot: "Scadenza: 16/06/2026",
  },
  {
    icon: "🧾",
    tone: "cyan",
    label: "Fatture",
    detail: "Fatture SdI",
    value: "126",
    unit: "",
    foot: "12 in attesa di esito",
  },
];

const workflow = [
  { label: "Presenze chiuse", value: "42/42", state: "Completato", tone: "green", icon: "📅" },
  { label: "Calcolo paghe", value: "38/42", state: "In corso", tone: "blue", icon: "🧮" },
  { label: "Consegna buste", value: "—", state: "In attesa", tone: "slate", icon: "✉️" },
  { label: "Genera CU", value: "42", state: "In attesa", tone: "slate", icon: "📄" },
  { label: "Invio F24", value: "18.420,00", state: "In scadenza", tone: "amber", icon: "🏛" },
  { label: "UniEmens", value: "Maggio", state: "In attesa", tone: "slate", icon: "☁️" },
  { label: "Fatture", value: "126", state: "In corso", tone: "blue", icon: "€" },
];

const documentQueue = [
  {
    tag: "F24",
    tone: "amber",
    title: "F24 FEP - Ritenute lavoro dip.",
    ref: "Giugno 2026",
    due: "16/06/2026",
    state: "In scadenza",
    stateTone: "amber",
  },
  {
    tag: "CU",
    tone: "blue",
    title: "Certificazione Unica 2026",
    ref: "Anno d'imposta 2025",
    due: "16/03/2026",
    state: "In attesa",
    stateTone: "slate",
  },
  {
    tag: "UNI",
    tone: "violet",
    title: "UniEmens - Flusso mensile",
    ref: "Maggio 2026",
    due: "31/05/2026",
    state: "In lavorazione",
    stateTone: "blue",
  },
  {
    tag: "FAT",
    tone: "green",
    title: "Fattura elettronica n. 2026/045",
    ref: "Cliente Verdi S.r.l.",
    due: "28/05/2026",
    state: "In attesa SdI",
    stateTone: "slate",
  },
  {
    tag: "F24",
    tone: "amber",
    title: "F24 Accise e Contributi",
    ref: "Giugno 2026",
    due: "16/06/2026",
    state: "Bloccato",
    stateTone: "red",
  },
];

export function Dashboard() {
  const [active, setActive] = useState<SectionKey>("dashboard");
  const [rapportoSel, setRapportoSel] = useState<string | null>(null);
  const [aiutoAperto, setAiutoAperto] = useState(false);
  const apriDipendente = (rapportoId: string) => { setRapportoSel(rapportoId); setActive("dipendente"); };

  const navActive = navActiveFor[active] ?? active;
  const crumbs = useMemo(() => crumbsFor[active] ?? ["Home", sectionTitle[active]], [active]);

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
          <NavGroup title="Amministrazione" items={adminNav} active={navActive} onChange={setActive} />
        </nav>

        <button className="company-switch" type="button" onClick={() => setActive("aziende")}>
          <span className="company-icon">🏢</span>
          <span>
            <strong>Rossi S.r.l.</strong>
            <small>P.IVA 01567890123</small>
          </span>
          <span className="chevron">›</span>
        </button>
      </aside>

      <div className="workspace">
        <Topbar onCompanyClick={() => setActive("aziende")} onAiuto={() => setAiutoAperto(true)} />

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

function Topbar({ onCompanyClick, onAiuto }: { onCompanyClick: () => void; onAiuto: () => void }) {
  return (
    <header className="topbar">
      <button className="soft-button company-top" type="button" onClick={onCompanyClick}>
        <span>🏢</span>
        <strong>Rossi S.r.l.</strong>
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
        <span>MR</span>
        <strong>
          Mario Rossi
          <small>m.rossi@rossisrl.it</small>
        </strong>
        <em>⌄</em>
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
          <div className="workflow">
            {workflow.map((step, index) => (
              <div key={step.label} className="workflow-step">
                {index < workflow.length - 1 && <span className="workflow-line" />}
                <span className={`workflow-dot ${step.tone}`}>{step.icon}</span>
                <strong>{step.label}</strong>
                <small>{step.value}</small>
                <em className={`status-pill ${step.tone}`}>{step.state}</em>
              </div>
            ))}
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
              Tutti (18)
            </button>
            <button type="button">In attesa (7)</button>
            <button type="button">In lavorazione (6)</button>
            <button type="button">Bloccati (2)</button>
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
                {documentQueue.map((row) => (
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
                ))}
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
  const deadlines = [
    ["16", "GIU", "F24 FEP - Ritenute", "Scade oggi"],
    ["20", "GIU", "UniEmens - Maggio 2026", "Scade tra 4 giorni"],
    ["30", "GIU", "Versamento ritenute", "Scade tra 14 giorni"],
  ];

  const receipts = [
    ["F24 EP - Maggio 2026", "Ricevuta n. 250000123456", "10:15"],
    ["UniEmens - Aprile 2026", "Protocollo n. 1234567890", "08:42"],
    ["Fattura n. 2026/040", "Esito: Consegnata", "ieri"],
  ];

  return (
    <aside className="dashboard-rail">
      <section className="glass-card side-card">
        <div className="side-card-head">
          <strong>Errori da correggere</strong>
          <span className="count red">2</span>
        </div>
        <button className="alert-row" type="button" onClick={() => onNavigate("presenze")}>
          <span>!</span>
          <strong>
            Cartellini incompleti
            <small>2 dipendenti con anomalie</small>
          </strong>
          <em>›</em>
        </button>
        <button className="alert-row" type="button" onClick={() => onNavigate("dipendenti")}>
          <span>!</span>
          <strong>
            Anagrafica fiscale mancante
            <small>1 dipendente senza codice fiscale</small>
          </strong>
          <em>›</em>
        </button>
        <button className="rail-cta" type="button">
          Vai agli errori
        </button>
      </section>

      <section className="glass-card side-card">
        <div className="side-card-head">
          <strong>Scadenze</strong>
        </div>
        {deadlines.map(([day, month, title, meta]) => (
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
        ))}
        <button className="rail-cta" type="button" onClick={() => onNavigate("adempimenti")}>
          Vedi tutte le scadenze
        </button>
      </section>

      <section className="glass-card side-card">
        <div className="side-card-head">
          <strong>Ricevute recenti</strong>
        </div>
        {receipts.map(([title, meta, time]) => (
          <div className="receipt-row" key={title}>
            <span>✓</span>
            <strong>
              {title}
              <small>{meta}</small>
            </strong>
            <em>{time}</em>
          </div>
        ))}
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
              <p>Anagrafiche, sedi e dati fiscali collegati al tenant demo</p>
            </div>
          </div>
          <Aziende />
        </div>
        <ModuleRail
          title="Stato anagrafiche"
          rows={[
            ["Aziende attive", "4"],
            ["Sedi incomplete", "1"],
            ["Ultimo aggiornamento", "oggi"],
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
  const rows = [
    ["F24", "Ritenute lavoro dipendente", "16/06/2026", "Studio", "In scadenza", "amber"],
    ["UNI", "UniEmens maggio 2026", "30/06/2026", "Paghe", "In lavorazione", "blue"],
    ["LUL", "Libro unico del lavoro", "30/06/2026", "Paghe", "In attesa", "slate"],
    ["SDI", "Controllo esiti fatture", "28/06/2026", "Amministrazione", "Bloccato", "red"],
  ];

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
        <div className="metric-strip">
          <Metric label="In scadenza" value="7" />
          <Metric label="Bloccati" value="2" />
          <Metric label="In lavorazione" value="6" />
          <Metric label="Completati" value="24" />
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
              {rows.map(([type, title, due, owner, state, tone]) => (
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

      <aside className="glass-card focus-panel">
        <h2>Timeline mese</h2>
        <TimelineRow day="16" label="Versamento F24" tone="amber" />
        <TimelineRow day="20" label="Controllo ricevute" tone="blue" />
        <TimelineRow day="30" label="Chiusura LUL" tone="slate" />
        <TimelineRow day="30" label="Invio UniEmens" tone="blue" />
      </aside>
    </div>
  );
}

function AccountingPanel() {
  const rows = [
    ["Stipendi lordi", "115.469,28 €", "Quadrato", "green"],
    ["Contributi azienda", "32.218,40 €", "Da versare", "amber"],
    ["TFR mese", "4.866,54 €", "Accantonato", "blue"],
    ["Ritenute fiscali", "18.420,00 €", "In F24", "amber"],
  ];

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
        <div className="accounting-total">
          <span>Costo azienda stimato</span>
          <strong>147.687,68 €</strong>
          <em>Giugno 2026 · Rossi S.r.l.</em>
        </div>
        <div className="ledger-grid">
          {rows.map(([label, value, state, tone]) => (
            <article className="ledger-card" key={label}>
              <span>{label}</span>
              <strong>{value}</strong>
              <em className={`status-pill ${tone}`}>{state}</em>
            </article>
          ))}
        </div>
      </section>

      <aside className="glass-card focus-panel">
        <h2>Controlli audit</h2>
        <CheckRow label="Totale cedolini = prima nota" ok />
        <CheckRow label="F24 coerente con trattenute" ok />
        <CheckRow label="2 movimenti senza centro costo" />
        <CheckRow label="Ricevute SdI da riconciliare" />
      </aside>
      </div>
    </>
  );
}

function DocumentsPanel({ onNavigate }: { onNavigate: (key: SectionKey) => void }) {
  const docs = [
    ["Busta paga", "Mario Bianchi · Giugno 2026", "PDF", "Generato", "green"],
    ["F24 EP", "Ritenute lavoro dipendente", "PDF", "Da firmare", "amber"],
    ["Certificazione Unica", "Anno d'imposta 2025", "PDF", "In bozza", "blue"],
    ["UniEmens", "Maggio 2026", "XML", "In lavorazione", "blue"],
    ["Fattura elettronica", "2026/045 · Cliente Verdi", "XML", "In attesa SdI", "slate"],
    ["Libro unico", "Giugno 2026", "PDF", "Da chiudere", "amber"],
  ];

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
    </section>
  );
}

function F24Panel() {
  const payments = [
    ["1001", "Ritenute lavoro dipendente", "18.420,00 €"],
    ["3802", "Addizionale regionale", "820,00 €"],
    ["3848", "Addizionale comunale", "420,00 €"],
    ["DM10", "Contributi INPS", "24.180,40 €"],
  ];

  return (
    <div className="document-workspace">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Modello F24</h1>
            <p>Riepilogo versamenti · scadenza 16/06/2026</p>
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
              {payments.map(([code, label, amount]) => (
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

      <aside className="glass-card pdf-preview">
        <div className="pdf-toolbar">
          <strong>Anteprima F24</strong>
          <span className="status-pill amber">Da firmare</span>
        </div>
        <div className="fiscal-sheet">
          <b>F24</b>
          <span>Rossi S.r.l.</span>
          <span>Periodo 06/2026</span>
          <strong>43.840,40 €</strong>
        </div>
      </aside>
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
  const users = [
    ["MR", "Mario Rossi", "m.rossi@rossisrl.it", "Admin", "Attivo"],
    ["SP", "Studio Paghe", "paghe@rossisrl.it", "Operatore", "Attivo"],
    ["RV", "Revisore", "revisore@rossisrl.it", "Lettura", "Invito pendente"],
  ];

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
            {users.map(([initials, name, email, role, state]) => (
              <tr key={email}>
                <td>
                  <div className="person-cell">
                    <span>{initials}</span>
                    <strong>{name}</strong>
                  </div>
                </td>
                <td>{email}</td>
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

function TimelineRow({ day, label, tone }: { day: string; label: string; tone: string }) {
  return (
    <div className="timeline-row">
      <span className={`tag ${tone}`}>{day}</span>
      <strong>{label}</strong>
    </div>
  );
}

function CheckRow({ label, ok }: { label: string; ok?: boolean }) {
  return (
    <div className="check-row">
      <span className={`status-pill ${ok ? "green" : "amber"}`}>{ok ? "OK" : "Check"}</span>
      <strong>{label}</strong>
    </div>
  );
}

function GenericPanel({
  active,
  onNavigate,
}: {
  active: SectionKey;
  onNavigate: (key: SectionKey) => void;
}) {
  const rowsBySection: Partial<Record<SectionKey, string[][]>> = {
    adempimenti: [
      ["F24 ritenute", "16/06/2026", "In scadenza"],
      ["UniEmens maggio", "30/06/2026", "In lavorazione"],
      ["Libro unico", "30/06/2026", "In attesa"],
    ],
    contabilita: [
      ["Liquidazione IVA", "Maggio 2026", "Bozza"],
      ["Prima nota stipendi", "Giugno 2026", "Pronta"],
      ["Quadratura contributi", "Giugno 2026", "Da verificare"],
    ],
    documenti: [
      ["Cedolino Mario Bianchi", "PDF", "Generato"],
      ["F24 EP", "PDF", "Firmato"],
      ["CU 2026", "PDF", "In bozza"],
    ],
    f24: [
      ["Ritenute lavoro dipendente", "18.420,00 €", "In scadenza"],
      ["Contributi INPS", "24.180,40 €", "Pronto"],
      ["Addizionali", "1.240,00 €", "Pronto"],
    ],
    cu: [
      ["CU lavoro dipendente", "42", "Pronte"],
      ["CU autonome", "3", "Da controllare"],
      ["Invio telematico", "2026", "In attesa"],
    ],
    impostazioni: [
      ["Parametri fiscali", "2026", "Aggiornati"],
      ["CCNL Commercio", "14 mensilita", "Attivo"],
      ["Brand documenti", "PaghePro", "Attivo"],
    ],
    utenti: [
      ["Mario Rossi", "Admin", "Attivo"],
      ["Studio Paghe", "Operatore", "Attivo"],
      ["Revisore", "Lettura", "Invito pendente"],
    ],
  };
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
            {rows.map(([title, ref, state]) => (
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

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
