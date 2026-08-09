import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { AlertTriangle, Building2, Check, ClipboardPaste, Edit3, FileText, Landmark, MapPin, Plus, Search, Trash2, UserRound, Users, WalletCards, X } from "lucide-react";
import { api, type Azienda, type DipendenteRapporto, type NuovaAzienda } from "../api";
import {
  applicaDatiVisura, datiContributiviMancanti, parsaTestoAzienda, validaAzienda,
  type ErroriAzienda,
} from "../aziendaForm";
import { ComuneAutocomplete } from "../ComuneAutocomplete";
import {
  AZIENDA_CORRENTE_CAMBIATA,
  dimenticaAziendaCorrente,
  leggiAziendaCorrenteId,
  salvaAziendaCorrenteId,
} from "../aziendaCorrente";

// ---------- tipi locali ----------
type Errori = ErroriAzienda;

const vuota: NuovaAzienda = {
  ragioneSociale: "", partitaIva: "", codiceFiscale: "", indirizzo: "", cap: "", comune: "", provincia: "", codiceCatastale: "",
  telefono: "", email: "", pec: "", codiceDestinatarioSdi: "", settore: "", codiceAteco: "",
  matricolaInps: "", codiceSedeInps: "", cscInps: "", patInail: "", ccnlDefault: "", livelloDefault: "",
  iban: "", legaleRappresentanteNome: "", legaleRappresentanteCognome: "", legaleRappresentanteCodiceFiscale: "",
  canalePredefinito: "", haDipendenti: true, tipoContribuente: "", assoggettataIva: true,
  oltreQuindiciDipendenti: false, contributiFondiSettore: 0,
};

// Fondi di settore a carico del dipendente, verificati su buste reali. Il valore è una frazione:
// finisce direttamente nel calcolo della quota INPS, quindi meglio sceglierlo che digitarlo.
const FONDI_SETTORE = [
  { valore: 0, label: "Nessuno" },
  { valore: 0.002667, label: "FIS 0,2667% — Fondo Integrazione Salariale (es. Multiservizi)" },
  { valore: 0.0015, label: "FSBS 0,15% — somministrazione" },
];

const SETTORI_AZIENDA = [
  { codice: "COMMERCIO_SERVIZI", label: "Commercio / servizi", tipoContribuente: "Societa", codiceAteco: "47.91.10", ccnlDefault: "COMMERCIO_TERZIARIO", livelloDefault: "3" },
  { codice: "LOGISTICA_TRASPORTI", label: "Logistica / trasporti", tipoContribuente: "Societa", codiceAteco: "49.41.00", ccnlDefault: "TRASPORTI_LOGISTICA", livelloDefault: "3" },
  { codice: "EDILIZIA", label: "Edilizia", tipoContribuente: "Societa", codiceAteco: "41.20.00", ccnlDefault: "EDILIZIA_INDUSTRIA", livelloDefault: "2" },
  { codice: "METALMECCANICA", label: "Metalmeccanica", tipoContribuente: "Societa", codiceAteco: "25.62.00", ccnlDefault: "METALMECCANICI_INDUSTRIA", livelloDefault: "C3" },
  { codice: "TURISMO_RISTORAZIONE", label: "Turismo / ristorazione", tipoContribuente: "Societa", codiceAteco: "56.10.11", ccnlDefault: "TURISMO_PUBBLICI_ESERCIZI", livelloDefault: "4" },
  { codice: "PULIZIE_MULTISERVIZI", label: "Pulizie / multiservizi", tipoContribuente: "Societa", codiceAteco: "81.21.00", ccnlDefault: "MULTISERVIZI_PULIZIE", livelloDefault: "3" },
  { codice: "STUDIO_PROFESSIONALE", label: "Studio professionale", tipoContribuente: "Professionista", codiceAteco: "69.20.11", ccnlDefault: "STUDI_PROFESSIONALI", livelloDefault: "3" },
  { codice: "ALIMENTARE", label: "Alimentare", tipoContribuente: "Societa", codiceAteco: "10.71.10", ccnlDefault: "ALIMENTARE_INDUSTRIA", livelloDefault: "3" },
  { codice: "AGRICOLTURA", label: "Agricoltura", tipoContribuente: "Societa", codiceAteco: "01.61.00", ccnlDefault: "AGRICOLTURA_OPERAI", livelloDefault: "Area 2" },
] as const;

const CCNL_OPZIONI = [
  { codice: "COMMERCIO_TERZIARIO", label: "Commercio / terziario" },
  { codice: "TRASPORTI_LOGISTICA", label: "Logistica / trasporti" },
  { codice: "EDILIZIA_INDUSTRIA", label: "Edilizia industria" },
  { codice: "METALMECCANICI_INDUSTRIA", label: "Metalmeccanici industria" },
  { codice: "TURISMO_PUBBLICI_ESERCIZI", label: "Turismo / pubblici esercizi" },
  { codice: "MULTISERVIZI_PULIZIE", label: "Multiservizi / pulizie" },
  { codice: "STUDI_PROFESSIONALI", label: "Studi professionali" },
  { codice: "ALIMENTARE_INDUSTRIA", label: "Alimentare industria" },
  { codice: "AGRICOLTURA_OPERAI", label: "Agricoltura operai" },
];

function labelSettore(codice?: string | null): string {
  if (!codice) return "";
  if (codice === "ALTRO") return "Altro / da verificare";
  return SETTORI_AZIENDA.find(s => s.codice === codice)?.label ?? codice;
}

const SEZIONI = [
  { id: "identita", titolo: "1. Dati identificativi & Fatturazione", campi: ["ragioneSociale", "partitaIva", "codiceFiscale", "codiceDestinatarioSdi", "settore", "codiceAteco"] as (keyof NuovaAzienda)[] },
  { id: "sede", titolo: "2. Sede legale & Territorio", campi: ["indirizzo", "comune", "cap", "provincia", "codiceCatastale"] as (keyof NuovaAzienda)[] },
  { id: "contatti", titolo: "3. Contatti e PEC", campi: ["telefono", "email", "pec", "canalePredefinito"] as (keyof NuovaAzienda)[] },
  { id: "fiscale", titolo: "4. Profilo fiscale & Tesoreria (IBAN)", campi: ["tipoContribuente", "iban", "haDipendenti", "assoggettataIva"] as (keyof NuovaAzienda)[] },
  { id: "contributivo", titolo: "5. Dati INPS, INAIL, CCNL & Legale Rappresentante", campi: ["matricolaInps", "codiceSedeInps", "cscInps", "patInail", "ccnlDefault", "livelloDefault", "legaleRappresentanteNome", "legaleRappresentanteCognome", "legaleRappresentanteCodiceFiscale"] as (keyof NuovaAzienda)[] },
];

// ---------- componenti piccoli ----------
function Campo({ label, errore, children, facoltativo }: {
  label: string; errore?: string; children: React.ReactNode; facoltativo?: boolean;
}) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <span style={{ fontSize: 13, fontWeight: 600, color: errore ? "#e74c3c" : "inherit" }}>
        {label}{facoltativo && <small style={{ fontWeight: 400, marginLeft: 4, opacity: 0.7 }}>(facoltativo)</small>}
      </span>
      {children}
      {errore && (
        <span style={{ fontSize: 12, color: "#e74c3c", display: "flex", alignItems: "center", gap: 4 }}>
          ⚠ {errore}
        </span>
      )}
    </label>
  );
}

function inputStyle(errore?: string): React.CSSProperties {
  return {
    border: errore ? "1.5px solid #e74c3c" : "1px solid rgba(95, 125, 120, 0.45)",
    borderRadius: 8, padding: "8px 10px", fontSize: 14,
    background: "#fbfdfc", boxShadow: "var(--incavo)",
    color: "var(--text)", width: "100%", boxSizing: "border-box",
  };
}

function selectStyle(errore?: string): React.CSSProperties {
  return { ...inputStyle(errore) };
}

// ---------- componente principale ----------
export function Aziende({
  onAziendeCambiate,
}: {
  onAziendeCambiate?: () => void;
} = {}) {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [dipendentiPerAzienda, setDipendentiPerAzienda] = useState<Record<string, DipendenteRapporto[]>>({});
  const [correnteId, setCorrenteId] = useState<string | null>(() => leggiAziendaCorrenteId());
  const [erroreApi, setErroreApi] = useState<string | null>(null);
  const [form, setForm] = useState<NuovaAzienda>(vuota);
  const [errori, setErrori] = useState<Errori>({});
  const [tentato, setTentato] = useState(false);
  const [daConfermare, setDaConfermare] = useState<NuovaAzienda | null>(null);
  const [notaVisura, setNotaVisura] = useState<string | null>(null);
  const [visuraInCorso, setVisuraInCorso] = useState(false);
  const [inModificaId, setInModificaId] = useState<string | null>(null);
  const [mostraIncolla, setMostraIncolla] = useState(false);
  const [testoIncollato, setTestoIncollato] = useState("");
  const [selezionate, setSelezionate] = useState<Set<string>>(new Set());
  const [eliminazioneInCorso, setEliminazioneInCorso] = useState(false);
  const [confermaInCorso, setConfermaInCorso] = useState(false);
  const [successoBanner, setSuccessoBanner] = useState<string | null>(null);
  const [idAppenaAggiunta, setIdAppenaAggiunta] = useState<string | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | null>(null);
  const [logoInCorso, setLogoInCorso] = useState(false);
  const [datiContributiviProvvisori, setDatiContributiviProvvisori] = useState(false);
  const [ricerca, setRicerca] = useState("");
  const [vista, setVista] = useState<"archivio" | "scheda">("archivio");
  const [sezioneAttiva, setSezioneAttiva] = useState("identita");

  const refSezioni = useRef<Record<string, HTMLDivElement | null>>({});
  const refForm = useRef<HTMLFormElement | null>(null);
  const refLista = useRef<HTMLDivElement | null>(null);

  // ---------- carica ----------
  async function carica() {
    try {
      const elenco = await api.aziende();
      const dipendenti = await Promise.all(elenco.map(async (azienda) => {
        try { return [azienda.id, await api.dipendenti(azienda.id)] as const; }
        catch { return [azienda.id, []] as const; }
      }));
      setAziende(elenco);
      setDipendentiPerAzienda(Object.fromEntries(dipendenti));
      const corrente = leggiAziendaCorrenteId();
      if (corrente && elenco.some((a) => a.id === corrente)) {
        setCorrenteId(corrente);
      } else {
        // Non scegliere silenziosamente il primo cliente: il commercialista deve
        // confermare l'azienda prima di aprire strumenti operativi.
        setCorrenteId(null);
        if (corrente) dimenticaAziendaCorrente();
      }
      setSelezionate(new Set());
      onAziendeCambiate?.();
      window.dispatchEvent(new CustomEvent(AZIENDA_CORRENTE_CAMBIATA));
    }
    catch (e) { setErroreApi(e instanceof Error ? e.message : "Errore"); }
  }
  useEffect(() => { void carica(); }, []);
  useEffect(() => {
    const aggiorna = () => setCorrenteId(leggiAziendaCorrenteId());
    window.addEventListener(AZIENDA_CORRENTE_CAMBIATA, aggiorna);
    window.addEventListener("storage", aggiorna);
    return () => {
      window.removeEventListener(AZIENDA_CORRENTE_CAMBIATA, aggiorna);
      window.removeEventListener("storage", aggiorna);
    };
  }, []);

  // ---------- visura ----------
  async function elaboraVisura(file: File) {
    setErroreApi(null); setNotaVisura(null); setVisuraInCorso(true);
    try {
      const d = await api.aziendaDaVisura(file);
      setForm(f => applicaDatiVisura(f, d));
      setNotaVisura(d.nota ?? "Dati estratti dalla visura: controllali, poi premi «Aggiungi azienda».");
    } catch (err) {
      setErroreApi(err instanceof Error ? err.message : "Non sono riuscito a leggere la visura.");
    } finally { setVisuraInCorso(false); }
  }

  function apriSelettoreVisura() {
    if (visuraInCorso) return;
    const input = document.createElement("input");
    input.type = "file"; input.accept = "application/pdf,.pdf,image/*";
    input.onchange = () => { const f = input.files?.[0]; if (f) void elaboraVisura(f); };
    input.click();
  }

  // ---------- logo aziendale (compare sulla busta paga) ----------
  function aggiornaAnteprimaLogo(url: string | null) {
    setLogoUrl(prev => { if (prev) URL.revokeObjectURL(prev); return url; });
  }

  async function caricaAnteprimaLogo(aziendaId: string) {
    aggiornaAnteprimaLogo(await api.logoAzienda(aziendaId));
  }

  function apriSelettoreLogo() {
    if (!inModificaId || logoInCorso) return;
    const input = document.createElement("input");
    input.type = "file"; input.accept = "image/png,image/jpeg";
    input.onchange = async () => {
      const f = input.files?.[0];
      if (!f || !inModificaId) return;
      setLogoInCorso(true); setErroreApi(null);
      try {
        await api.caricaLogoAzienda(inModificaId, f);
        await caricaAnteprimaLogo(inModificaId);
        mostraSuccesso("Logo salvato: comparirà in testata della busta paga.");
      } catch (err) { setErroreApi(err instanceof Error ? err.message : "Caricamento logo non riuscito."); }
      finally { setLogoInCorso(false); }
    };
    input.click();
  }

  async function rimuoviLogo() {
    if (!inModificaId || logoInCorso) return;
    setLogoInCorso(true); setErroreApi(null);
    try {
      await api.rimuoviLogoAzienda(inModificaId);
      aggiornaAnteprimaLogo(null);
      mostraSuccesso("Logo rimosso.");
    } catch (err) { setErroreApi(err instanceof Error ? err.message : "Rimozione logo non riuscita."); }
    finally { setLogoInCorso(false); }
  }

  // ---------- selezione bulk ----------
  function toggleSelezione(id: string) {
    setSelezionate(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n; });
  }
  function toggleTutte() {
    const idsVisibili = aziendeFiltrate.map((azienda) => azienda.id);
    const tutteVisibiliSelezionate = idsVisibili.length > 0 && idsVisibili.every((id) => selezionate.has(id));
    setSelezionate((precedenti) => {
      const prossime = new Set(precedenti);
      idsVisibili.forEach((id) => tutteVisibiliSelezionate ? prossime.delete(id) : prossime.add(id));
      return prossime;
    });
  }
  async function eliminaSelezionate() {
    if (!selezionate.size) return;
    const n = selezionate.size;
    if (!confirm(`Eliminare ${n} aziend${n === 1 ? "a" : "e"} selezionat${n === 1 ? "a" : "e"}? Non è reversibile.`)) return;
    setEliminazioneInCorso(true); setErroreApi(null);
    try { await api.eliminaAziendeBulk(Array.from(selezionate)); await carica(); }
    catch (err) { setErroreApi(err instanceof Error ? err.message : "Eliminazione non riuscita."); }
    finally { setEliminazioneInCorso(false); }
  }

  // ---------- form ----------
  function set(k: keyof NuovaAzienda) {
    return (e: { target: { value: string } }) => {
      const aggiornato = { ...form, [k]: e.target.value };
      setForm(aggiornato);
      if (tentato) setErrori(validaAzienda(aggiornato, { datiContributiviProvvisori }));
    };
  }

  function cambiaSettore(codice: string) {
    const preset = SETTORI_AZIENDA.find(s => s.codice === codice);
    const aggiornato: NuovaAzienda = preset
      ? {
        ...form,
        settore: preset.codice,
        tipoContribuente: preset.tipoContribuente,
        codiceAteco: preset.codiceAteco,
        ccnlDefault: preset.ccnlDefault,
        livelloDefault: preset.livelloDefault,
      }
      : { ...form, settore: codice };
    setForm(aggiornato);
    if (tentato) setErrori(validaAzienda(aggiornato, { datiContributiviProvvisori }));
  }

  function scrollAlPrimoErrore(e: Errori) {
    for (const sez of SEZIONI) {
      const haCampoErrato = sez.campi.some(c => !!e[c]);
      if (haCampoErrato) {
        setSezioneAttiva(sez.id);
        refSezioni.current[sez.id]?.scrollIntoView({ behavior: "smooth", block: "start" });
        break;
      }
    }
  }

  function inviaForm(e: FormEvent) {
    e.preventDefault();
    setErroreApi(null);
    if (inModificaId) { void salvaCorrezione(); return; }
    const e2 = validaAzienda(form, { datiContributiviProvvisori });
    setErrori(e2);
    setTentato(true);
    if (Object.keys(e2).length > 0) { scrollAlPrimoErrore(e2); return; }
    setDaConfermare(form);
  }

  function scegliAzienda(a: Azienda) {
    setErroreApi(null);
    salvaAziendaCorrenteId(a.id);
    setCorrenteId(a.id);
    // La scelta apre la postazione completa del cliente (l'evento di cambio
    // azienda è sincrono: la shell conosce già il nuovo cliente).
    window.location.hash = "azienda";
  }

  function iniziaModifica(a: Azienda) {
    setErroreApi(null); setNotaVisura(null); setTentato(false); setErrori({});
    setInModificaId(a.id);
    setVista("scheda");
    setSezioneAttiva("identita");
    void caricaAnteprimaLogo(a.id);
    salvaAziendaCorrenteId(a.id);
    setCorrenteId(a.id);
    setDatiContributiviProvvisori(Boolean(a.haDipendenti && [
      a.matricolaInps, a.codiceSedeInps, a.cscInps, a.patInail, a.ccnlDefault, a.livelloDefault,
    ].some(valore => !valore?.trim())));
    setForm({
      ragioneSociale: a.ragioneSociale, partitaIva: a.partitaIva ?? "",
      codiceFiscale: a.codiceFiscale ?? "",
      indirizzo: a.indirizzo ?? "", cap: a.cap ?? "", comune: a.comune ?? "", provincia: a.provincia ?? "",
      codiceCatastale: a.codiceCatastale ?? "",
      telefono: a.telefono ?? "", email: a.email ?? "", pec: a.pec ?? "",
      codiceDestinatarioSdi: a.codiceDestinatarioSdi ?? "",
      settore: a.settore ?? "", codiceAteco: a.codiceAteco ?? "",
      matricolaInps: a.matricolaInps ?? "", codiceSedeInps: a.codiceSedeInps ?? "",
      cscInps: a.cscInps ?? "", patInail: a.patInail ?? "",
      oltreQuindiciDipendenti: a.oltreQuindiciDipendenti ?? false,
      contributiFondiSettore: a.contributiFondiSettore ?? 0,
      ccnlDefault: a.ccnlDefault ?? "", livelloDefault: a.livelloDefault ?? "",
      iban: a.iban ?? "",
      legaleRappresentanteNome: a.legaleRappresentanteNome ?? "",
      legaleRappresentanteCognome: a.legaleRappresentanteCognome ?? "",
      legaleRappresentanteCodiceFiscale: a.legaleRappresentanteCodiceFiscale ?? "",
      canalePredefinito: a.canalePredefinito ?? "",
      haDipendenti: a.haDipendenti ?? false, tipoContribuente: a.tipoContribuente ?? "",
      assoggettataIva: a.assoggettataIva ?? true,
    });
    refForm.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function annullaModifica() {
    setInModificaId(null); setForm(vuota); setErrori({}); setTentato(false);
    setDaConfermare(null); setVista("archivio");
    setDatiContributiviProvvisori(false);
    aggiornaAnteprimaLogo(null);
  }

  async function salvaCorrezione() {
    if (!inModificaId) return;
    const e2 = validaAzienda(form, { datiContributiviProvvisori }); setErrori(e2); setTentato(true);
    if (Object.keys(e2).length > 0) { scrollAlPrimoErrore(e2); return; }
    try {
      await api.correggiAzienda(inModificaId, form);
      const nome = form.ragioneSociale;
      annullaModifica(); await carica();
      mostraSuccesso(`Azienda «${nome}» aggiornata con successo.`);
      setIdAppenaAggiunta(inModificaId);
      setTimeout(() => setIdAppenaAggiunta(null), 2500);
      setTimeout(() => refLista.current?.scrollIntoView({ behavior: "smooth", block: "start" }), 100);
    } catch (err) { setErroreApi(err instanceof Error ? err.message : "Salvataggio non riuscito."); }
  }

  function mostraSuccesso(msg: string) {
    setSuccessoBanner(msg);
    setTimeout(() => setSuccessoBanner(null), 4000);
  }

  async function conferma() {
    if (!daConfermare) return;
    setConfermaInCorso(true);
    try {
      const nuova = await api.creaAzienda(daConfermare);
      setForm(vuota); setDaConfermare(null); setTentato(false); setErrori({});
      setDatiContributiviProvvisori(false);
      await carica();
      mostraSuccesso(`Azienda «${daConfermare.ragioneSociale}» aggiunta con successo.`);
      if (nuova?.id) {
        salvaAziendaCorrenteId(nuova.id);
        setIdAppenaAggiunta(nuova.id);
        setTimeout(() => setIdAppenaAggiunta(null), 2500);
        setTimeout(() => refLista.current?.scrollIntoView({ behavior: "smooth", block: "start" }), 100);
      }
    } catch (err) {
      setErroreApi(err instanceof Error ? err.message : "Errore");
      setDaConfermare(null);
    } finally { setConfermaInCorso(false); }
  }

  // quante sezioni hanno errori
  const sezioniConErrori = SEZIONI.filter(s => s.campi.some(c => !!errori[c]));
  const aziendeFiltrate = useMemo(() => {
    const testo = ricerca.trim().toLocaleLowerCase("it");
    if (!testo) return aziende;
    return aziende.filter((azienda) => [
      azienda.ragioneSociale,
      azienda.partitaIva,
      azienda.codiceFiscale,
      azienda.comune,
      azienda.provincia,
      azienda.codiceAteco,
      labelSettore(azienda.settore),
    ].filter(Boolean).some((valore) => String(valore).toLocaleLowerCase("it").includes(testo)));
  }, [aziende, ricerca]);

  function vaiANuovaAzienda() {
    annullaModifica();
    setVista("scheda");
    setSezioneAttiva("identita");
    window.setTimeout(() => {
      document.querySelector(".aziende-registro")?.scrollIntoView({ behavior: "smooth", block: "start" });
      document.getElementById("ragione-sociale-azienda")?.focus({ preventScroll: true });
    }, 0);
  }

  return (
    <section className="glass-card form-card aziende-registro">
      <div className="aziende-commandbar">
        <div>
          <span className="aziende-commandbar-icon"><Building2 size={20} aria-hidden="true" /></span>
          <span>
            <h2>{vista === "archivio" ? "Archivio aziende" : inModificaId ? "Modifica azienda" : "Nuova azienda"}</h2>
            <p>{vista === "archivio" ? "Ricerca e selezione clienti" : "Anagrafica fiscale, contributiva e operativa"}</p>
          </span>
        </div>
        <div className="aziende-commandbar-actions">
          <div className="aziende-view-tabs" role="tablist" aria-label="Vista aziende">
            <button className={vista === "archivio" ? "active" : ""} type="button" role="tab" aria-selected={vista === "archivio"} onClick={() => setVista("archivio")}>Archivio</button>
            <button className={vista === "scheda" ? "active" : ""} type="button" role="tab" aria-selected={vista === "scheda"} onClick={() => setVista("scheda")}>Anagrafica</button>
          </div>
          <span className="count blue">{aziende.length} aziende · {Object.values(dipendentiPerAzienda).reduce((totale, elenco) => totale + elenco.length, 0)} dipendenti</span>
        </div>
      </div>

      {/* banner successo */}
      {successoBanner && (
        <div style={{ marginBottom: 16, padding: "12px 16px", borderRadius: 10,
          background: "rgba(46,204,113,0.15)", border: "1.5px solid #2ecc71",
          color: "#2ecc71", fontSize: 14, display: "flex", alignItems: "center", gap: 8 }}>
          <Check size={17} aria-hidden="true" /> {successoBanner}
        </div>
      )}
      {erroreApi && (
        <div className="azienda-global-error" role="alert">
          <AlertTriangle size={16} aria-hidden="true" /> {erroreApi}
        </div>
      )}

      <div hidden={vista !== "archivio"}>
      <div className="aziende-searchbar">
        <label className="aziende-searchbox" htmlFor="cerca-azienda-archivio">
          <Search size={17} aria-hidden="true" />
          <input
            id="cerca-azienda-archivio"
            value={ricerca}
            onChange={(e) => setRicerca(e.target.value)}
            placeholder="Nome, Partita IVA, Codice Fiscale, comune o settore"
          />
        </label>
        <button className="btn primary" type="button" onClick={vaiANuovaAzienda}>
          <Plus size={17} aria-hidden="true" />
          Nuova azienda
        </button>
      </div>

      {/* lista aziende */}
      <div ref={refLista}>
      {aziende.length === 0 ? (
        <div className="empty-state">Nessuna azienda ancora registrata.</div>
      ) : (
        <>
          <div className="company-list-toolbar aziende-results-toolbar">
            <label className="company-select-all">
              <input type="checkbox"
              checked={aziendeFiltrate.length > 0 && aziendeFiltrate.every((azienda) => selezionate.has(azienda.id))}
                onChange={toggleTutte} />
              Seleziona tutte ({aziendeFiltrate.length})
            </label>
            {selezionate.size > 0 && (
              <button className="btn danger compact" type="button" disabled={eliminazioneInCorso} onClick={eliminaSelezionate}>
                <Trash2 size={15} aria-hidden="true" />
                {eliminazioneInCorso ? "Eliminazione…" : `Elimina selezionate (${selezionate.size})`}
              </button>
            )}
          </div>
          {aziendeFiltrate.length === 0 ? (
            <div className="empty-state">Nessuna azienda corrisponde alla ricerca.</div>
          ) : <div className="company-list company-table">
            <div className="company-table-head" aria-hidden="true">
              <span />
              <span>Azienda</span>
              <span>Codici fiscali</span>
              <span>Sede</span>
              <span>Dipendenti</span>
              <span>Azioni</span>
            </div>
            {aziendeFiltrate.map((a) => {
              const selezionata = correnteId === a.id;
              const dipendenti = dipendentiPerAzienda[a.id] ?? [];
              const mancanti = datiContributiviMancanti(a);
              return (
                <article
                  className={`company-row ${selezionata ? "selected" : ""} ${selezionate.has(a.id) ? "marked" : ""} ${idAppenaAggiunta === a.id ? "just-added" : ""}`}
                  key={a.id}
                >
                  <label className="company-bulk-check" title={`Seleziona ${a.ragioneSociale}`}>
                    <input
                      type="checkbox"
                      checked={selezionate.has(a.id)}
                      onChange={() => toggleSelezione(a.id)}
                      aria-label={`Seleziona ${a.ragioneSociale}`}
                    />
                  </label>

                  <button className="company-row-main" type="button" onClick={() => scegliAzienda(a)} aria-pressed={selezionata}>
                    <span className="company-row-avatar">{a.ragioneSociale.slice(0, 2).toUpperCase()}</span>
                    <span className="company-row-copy">
                      <strong>{a.ragioneSociale}</strong>
                      {mancanti.length > 0 ? (
                        <small className="company-row-incompleta"
                          title={`Mancano: ${mancanti.join(", ")}. F24 e UniEmens restano bloccati.`}>
                          Da completare: {mancanti.join(", ")}
                        </small>
                      ) : (
                        <small>{a.settore ? labelSettore(a.settore) : "Settore non indicato"}</small>
                      )}
                    </span>
                  </button>

                  <span className="company-table-cell company-tax-cell">
                    <strong>{a.partitaIva ? `P.IVA ${a.partitaIva}` : "P.IVA non indicata"}</strong>
                    <small>{a.codiceFiscale ? `CF ${a.codiceFiscale}` : "CF non indicato"}</small>
                  </span>
                  <span className="company-table-cell">
                    <strong>{a.comune || "Comune non indicato"}</strong>
                    <small>{[a.provincia, a.cap].filter(Boolean).join(" · ") || "Sede da completare"}</small>
                  </span>
                  <span className="company-table-cell company-employees-cell" title={dipendenti.map(d => d.nominativo || "Senza nome").join(", ")}>
                    <Users size={15} aria-hidden="true" />
                    <strong>{dipendenti.length}</strong>
                  </span>

                  <div className="company-row-buttons">
                    {selezionata ? (
                      <span className="status-pill green"><Check size={13} aria-hidden="true" /> Scelta</span>
                    ) : (
                      <button className="mini-action" type="button" onClick={() => scegliAzienda(a)}>
                        Scegli
                      </button>
                    )}
                    <button className="mini-action" type="button" onClick={() => iniziaModifica(a)}>
                      <Edit3 size={14} aria-hidden="true" /> Modifica
                    </button>
                  </div>
                </article>
              );
            })}
          </div>}
        </>
      )}
      </div>

      </div>

      <div className="azienda-editor-shell" hidden={vista !== "scheda"}>
        <aside className="azienda-editor-nav" aria-label="Sezioni anagrafica azienda">
          <p>Sezioni scheda</p>
          {[
            { id: "identita", label: "Identità e fatturazione", Icona: Building2 },
            { id: "sede", label: "Sede e territorio", Icona: MapPin },
            { id: "contatti", label: "Contatti e PEC", Icona: UserRound },
            { id: "fiscale", label: "Profilo fiscale", Icona: WalletCards },
            { id: "contributivo", label: "INPS, INAIL e CCNL", Icona: Landmark },
          ].map(({ id, label, Icona }, indice) => (
            <button
              className={`${sezioneAttiva === id ? "active" : ""} ${sezioniConErrori.some((sezione) => sezione.id === id) ? "has-error" : ""}`}
              key={id}
              type="button"
              onClick={() => { setSezioneAttiva(id); refSezioni.current[id]?.scrollIntoView({ behavior: "smooth", block: "start" }); }}
            >
              <span>{indice + 1}</span><Icona size={15} aria-hidden="true" />{label}
            </button>
          ))}
          <button className="azienda-editor-back" type="button" onClick={() => setVista("archivio")}>
            <X size={15} aria-hidden="true" /> Torna all'archivio
          </button>
        </aside>

        <div className="azienda-editor-main">

      {/* titolo form */}
      <h3 id="nuova-azienda-form" className="azienda-form-title">
        {inModificaId ? <Edit3 size={19} aria-hidden="true" /> : <Building2 size={19} aria-hidden="true" />}
        {inModificaId ? "Correggi azienda" : "Inserisci azienda"}
      </h3>
      <p style={{ margin: "0 0 16px", fontSize: 13, opacity: 0.7 }}>
        {inModificaId
          ? "Dati identificativi, fiscali, territoriali e contributivi dell'azienda."
          : "Dati fiscali, sede, contatti e posizioni contributive."
        }
      </p>

      {/* logo aziendale: solo per aziende già salvate, compare sulla busta paga */}
      {inModificaId && (
        <div style={{ marginBottom: 18, padding: 14, borderRadius: 10,
          background: "var(--panel-strong)", border: "1px solid var(--line)",
          display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
          {logoUrl ? (
            <img src={logoUrl} alt="Logo aziendale"
              style={{ width: 64, height: 64, objectFit: "contain", borderRadius: 8,
                background: "#fff", border: "1px solid var(--line)", padding: 4 }} />
          ) : (
            <span style={{ width: 64, height: 64, borderRadius: 8, display: "flex",
              alignItems: "center", justifyContent: "center", fontSize: 24,
              background: "var(--panel)", border: "1px dashed var(--line)" }}>🏷️</span>
          )}
          <div style={{ flex: 1, minWidth: 200 }}>
            <strong style={{ fontSize: 14 }}>Logo aziendale (facoltativo)</strong>
            <p style={{ margin: "2px 0 0", fontSize: 12, opacity: 0.7 }}>
              Compare in testata della busta paga. PNG o JPEG, massimo 1 MB.
            </p>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="btn secondary" type="button" disabled={logoInCorso} onClick={apriSelettoreLogo}>
              {logoInCorso ? "Attendere…" : logoUrl ? "Sostituisci logo" : "Carica logo"}
            </button>
            {logoUrl && (
              <button className="btn danger compact" type="button" disabled={logoInCorso} onClick={rimuoviLogo}>
                Rimuovi
              </button>
            )}
          </div>
        </div>
      )}

      {/* import rapido */}
      {!daConfermare && (
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap", marginBottom: 18 }}>
          <button className="btn secondary" type="button" onClick={apriSelettoreVisura}
            disabled={visuraInCorso}
            style={{ cursor: visuraInCorso ? "wait" : "pointer", display: "inline-flex", gap: 8, alignItems: "center" }}>
            <FileText size={16} aria-hidden="true" /> {visuraInCorso ? "Lettura in corso…" : "Carica visura"}
          </button>
          <button className="btn secondary" type="button"
            onClick={() => { setMostraIncolla(v => !v); setTestoIncollato(""); }}
            style={{ display: "inline-flex", gap: 8, alignItems: "center" }}>
            <ClipboardPaste size={16} aria-hidden="true" /> {mostraIncolla ? "Nascondi testo" : "Incolla testo visura"}
          </button>
        </div>
      )}
      {notaVisura && (
        <p style={{ fontSize: 12, marginBottom: 10, color: "#2ecc71", lineHeight: 1.5 }}>
          <Check size={15} aria-hidden="true" /> {notaVisura}
        </p>
      )}
      {mostraIncolla && !daConfermare && (
        <div style={{ marginBottom: 16, padding: 14, borderRadius: 10, background: "var(--panel-strong)", border: "1px solid var(--line)", boxShadow: "var(--inset)" }}>
          <textarea
            placeholder={"Incolla qui il testo della visura camerale…\n\nEsempio:\nDenominazione: Rossi Srl\nPartita IVA: 01234567890\nSede Legale: Via Roma 1, CAP 20121, Milano (MI)"}
            value={testoIncollato} onChange={e => setTestoIncollato(e.target.value)}
            rows={6} style={{ width: "100%", resize: "vertical", fontFamily: "inherit", fontSize: 13,
              padding: "8px 10px", borderRadius: 8, border: "1px solid rgba(95, 125, 120, 0.45)",
              background: "#fbfdfc", boxShadow: "var(--incavo)", color: "var(--text)", boxSizing: "border-box" }} />
          <button className="btn primary" type="button" style={{ marginTop: 8 }}
            disabled={!testoIncollato.trim()}
            onClick={() => {
              const dati = parsaTestoAzienda(testoIncollato);
              const aggiornato = { ...form, ...dati };
              setForm(aggiornato);
              if (tentato) setErrori(validaAzienda(aggiornato, { datiContributiviProvvisori }));
              setMostraIncolla(false); setTestoIncollato("");
              setNotaVisura("Dati estratti dal testo: controllali, poi premi «Aggiungi azienda».");
            }}>
            Estrai dati →
          </button>
        </div>
      )}

      {/* riepilogo errori */}
      {tentato && sezioniConErrori.length > 0 && (
        <div style={{ marginBottom: 16, padding: "12px 16px", borderRadius: 10,
          background: "rgba(231,76,60,0.12)", border: "1.5px solid #e74c3c" }}>
          <p style={{ margin: "0 0 6px", fontWeight: 700, color: "#e74c3c", fontSize: 14 }}>
            <AlertTriangle size={16} aria-hidden="true" /> Correggi i seguenti campi prima di continuare:
          </p>
          <ul style={{ margin: 0, paddingLeft: 18, fontSize: 13 }}>
            {SEZIONI.flatMap(s => s.campi.filter(c => errori[c]).map(c => (
              <li key={c} style={{ marginBottom: 2 }}>
                <button type="button"
                  style={{ background: "none", border: "none", color: "#e74c3c", cursor: "pointer",
                    textDecoration: "underline", fontSize: 13, padding: 0 }}
                  onClick={() => refSezioni.current[s.id]?.scrollIntoView({ behavior: "smooth", block: "start" })}>
                  {errori[c]}
                </button>
              </li>
            )))}
          </ul>
        </div>
      )}

      {/* conferma */}
      {daConfermare ? (
        <div className="confirm-panel" style={{ marginBottom: 16 }}>
          <p>
            Stai per aggiungere <b>{daConfermare.ragioneSociale}</b>
            {daConfermare.comune ? <> · {daConfermare.comune} ({daConfermare.provincia})</> : null}.
          </p>
          <p style={{ marginTop: 6, fontSize: 13 }}>
            Settore: <b>{labelSettore(daConfermare.settore)}</b>
            {daConfermare.ccnlDefault ? <> · CCNL {daConfermare.ccnlDefault} livello {daConfermare.livelloDefault}</> : null}
          </p>
          {datiContributiviProvvisori && (
            <p style={{ marginTop: 8, fontSize: 13, color: "#b45309", fontWeight: 700 }}>
              Modalità prova: i dati INPS/INAIL mancanti restano vuoti e devono essere completati
              prima di usare F24, UniEmens o altri adempimenti ufficiali.
            </p>
          )}
          <div className="actions-row">
            <button className="btn primary" type="button" onClick={conferma} disabled={confermaInCorso}
              style={{ minWidth: 110, opacity: confermaInCorso ? 0.7 : 1 }}>
              {confermaInCorso ? "Salvataggio…" : "Conferma"}
            </button>
            <button className="btn secondary" type="button" onClick={() => setDaConfermare(null)}
              disabled={confermaInCorso}>
              Annulla
            </button>
          </div>
        </div>
      ) : (
        <form ref={refForm} onSubmit={inviaForm} noValidate>

          {/* SEZIONE 1 – Dati identificativi */}
          <div data-sezione-azienda="identita" ref={el => { refSezioni.current["identita"] = el; }}
            style={sezioneStyle(sezioniConErrori.some(s => s.id === "identita"))}>
            <h4 style={titoloSezioneStyle(sezioniConErrori.some(s => s.id === "identita"))}>
              {SEZIONI[0].titolo}
            </h4>
            <div style={grigliaStyle}>
              <Campo label="Nome azienda" errore={errori.ragioneSociale}>
                <input id="ragione-sociale-azienda" style={inputStyle(errori.ragioneSociale)}
                  placeholder="Es. Rossi Srl" value={form.ragioneSociale}
                  onChange={set("ragioneSociale")} />
              </Campo>
              <Campo label="Partita IVA" errore={errori.partitaIva} facoltativo>
                <input style={inputStyle(errori.partitaIva)}
                  placeholder="11 cifre" value={form.partitaIva ?? ""}
                  onChange={set("partitaIva")} />
              </Campo>
              <Campo label="Codice Fiscale" errore={errori.codiceFiscale} facoltativo>
                <input style={inputStyle(errori.codiceFiscale)}
                  placeholder="11 cifre o 16 car." value={form.codiceFiscale ?? ""}
                  onChange={set("codiceFiscale")} />
              </Campo>
              <Campo label="Codice Destinatario SdI" errore={errori.codiceDestinatarioSdi} facoltativo>
                <input style={inputStyle(errori.codiceDestinatarioSdi)}
                  placeholder="Es. 0000000 o 7 car." value={form.codiceDestinatarioSdi ?? ""}
                  onChange={set("codiceDestinatarioSdi")} maxLength={7} />
              </Campo>
              <Campo label="Settore attività" errore={errori.settore}>
                <select style={selectStyle(errori.settore)} value={form.settore ?? ""}
                  onChange={e => cambiaSettore(e.target.value)}>
                  <option value="">Scegli settore...</option>
                  {SETTORI_AZIENDA.map(s => (
                    <option key={s.codice} value={s.codice}>{s.label}</option>
                  ))}
                  <option value="ALTRO">Altro / da verificare</option>
                </select>
              </Campo>
              <Campo label="Codice ATECO" errore={errori.codiceAteco}>
                <input style={inputStyle(errori.codiceAteco)}
                  placeholder="Es. 62.01.00" value={form.codiceAteco ?? ""}
                  onChange={set("codiceAteco")} />
              </Campo>
            </div>
          </div>

          {/* SEZIONE 2 – Sede legale */}
          <div data-sezione-azienda="sede" ref={el => { refSezioni.current["sede"] = el; }}
            style={sezioneStyle(sezioniConErrori.some(s => s.id === "sede"))}>
            <h4 style={titoloSezioneStyle(sezioniConErrori.some(s => s.id === "sede"))}>
              {SEZIONI[1].titolo}
            </h4>
            <div style={{ ...grigliaStyle, gridTemplateColumns: "1fr" }}>
              <Campo label="Indirizzo" errore={errori.indirizzo}>
                <input style={inputStyle(errori.indirizzo)}
                  placeholder="Via Roma 1" value={form.indirizzo}
                  onChange={set("indirizzo")} />
              </Campo>
            </div>
            <div style={{ ...grigliaStyle, marginTop: 12 }}>
              <Campo label="Comune" errore={errori.comune}>
                <div style={{ border: errori.comune ? "1.5px solid #e74c3c" : "1px solid rgba(160, 182, 178, 0.4)", borderRadius: 8 }}>
                  <ComuneAutocomplete
                    valore={form.comune}
                    onSelect={(c) => {
                      const ag = { ...form, comune: c.nome, provincia: c.prov, cap: c.cap, codiceCatastale: c.cat ?? form.codiceCatastale };
                      setForm(ag);
                      if (tentato) setErrori(validaAzienda(ag, { datiContributiviProvvisori }));
                    }}
                  />
                </div>
              </Campo>
              <Campo label="CAP" errore={errori.cap}>
                <input style={inputStyle(errori.cap)} placeholder="20121"
                  value={form.cap} onChange={set("cap")} maxLength={5} />
              </Campo>
              <Campo label="Provincia" errore={errori.provincia}>
                <input style={inputStyle(errori.provincia)} placeholder="MI"
                  value={form.provincia} onChange={set("provincia")} maxLength={2} />
              </Campo>
              <Campo label="Cod. Catastale Comune" errore={errori.codiceCatastale} facoltativo>
                <input style={inputStyle(errori.codiceCatastale)} placeholder="Es. F205 (Milano)"
                  value={form.codiceCatastale ?? ""} onChange={set("codiceCatastale")} maxLength={4} />
              </Campo>
            </div>
          </div>

          {/* SEZIONE 3 – Contatti */}
          <div data-sezione-azienda="contatti" ref={el => { refSezioni.current["contatti"] = el; }}
            style={sezioneStyle(sezioniConErrori.some(s => s.id === "contatti"))}>
            <h4 style={titoloSezioneStyle(sezioniConErrori.some(s => s.id === "contatti"))}>
              {SEZIONI[2].titolo}
            </h4>
            <div style={grigliaStyle}>
              <Campo label="Telefono / Cell" errore={errori.telefono} facoltativo>
                <input type="tel" style={inputStyle(errori.telefono)}
                  placeholder="+39 333 1234567" value={form.telefono ?? ""}
                  onChange={set("telefono")} />
              </Campo>
              <Campo label="Email aziendale" errore={errori.email} facoltativo>
                <input type="email" style={inputStyle(errori.email)}
                  placeholder="info@azienda.it" value={form.email ?? ""}
                  onChange={set("email")} />
              </Campo>
              <Campo label="PEC aziendale" errore={errori.pec} facoltativo>
                <input type="email" style={inputStyle(errori.pec)}
                  placeholder="azienda@pec.it" value={form.pec ?? ""}
                  onChange={set("pec")} />
              </Campo>
              <Campo label="Canale notifiche predefinito">
                <select style={selectStyle()} value={form.canalePredefinito ?? ""}
                  onChange={set("canalePredefinito")}>
                  <option value="">Manuale (copia testo)</option>
                  <option value="Email">Email</option>
                  <option value="Sms">SMS</option>
                  <option value="WhatsApp">WhatsApp</option>
                </select>
              </Campo>
            </div>
          </div>

          {/* SEZIONE 4 – Profilo fiscale & Tesoreria */}
          <div data-sezione-azienda="fiscale" ref={el => { refSezioni.current["fiscale"] = el; }}
            style={sezioneStyle(sezioniConErrori.some(s => s.id === "fiscale"))}>
            <h4 style={titoloSezioneStyle(sezioniConErrori.some(s => s.id === "fiscale"))}>
              {SEZIONI[3].titolo}
            </h4>
            <div style={grigliaStyle}>
              <Campo label="Tipo contribuente" errore={errori.tipoContribuente}>
                <select style={selectStyle(errori.tipoContribuente)} value={form.tipoContribuente ?? ""}
                  onChange={set("tipoContribuente")}>
                  <option value="">Non specificato</option>
                  <option value="Artigiano">Artigiano</option>
                  <option value="Commerciante">Commerciante</option>
                  <option value="Professionista">Professionista</option>
                  <option value="Societa">Società</option>
                </select>
              </Campo>
              <Campo label="IBAN Aziendale" errore={errori.iban} facoltativo>
                <input style={inputStyle(errori.iban)}
                  placeholder="IT60X0542811101000000123456" value={form.iban ?? ""}
                  onChange={set("iban")} />
              </Campo>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12, marginTop: 12 }}>
              <label style={{ display: "flex", alignItems: "center", gap: 10, cursor: "pointer", fontSize: 14 }}>
                <input type="checkbox" checked={form.haDipendenti ?? false}
                  onChange={e => {
                    const ag = { ...form, haDipendenti: e.target.checked };
                    if (!e.target.checked) setDatiContributiviProvvisori(false);
                    setForm(ag);
                    if (tentato) setErrori(validaAzienda(ag, {
                      datiContributiviProvvisori: e.target.checked && datiContributiviProvvisori,
                    }));
                  }} />
                <span>Ha dipendenti <small style={{ opacity: 0.7 }}>(abilita F24 IRPEF/INPS, CU, UniEmens, INAIL)</small></span>
              </label>
              <label style={{ display: "flex", alignItems: "center", gap: 10, cursor: "pointer", fontSize: 14 }}>
                <input type="checkbox" checked={form.assoggettataIva ?? true}
                  onChange={e => {
                    const ag = { ...form, assoggettataIva: e.target.checked };
                    setForm(ag);
                    if (tentato) setErrori(validaAzienda(ag, { datiContributiviProvvisori }));
                  }} />
                <span>Soggetta IVA <small style={{ opacity: 0.7 }}>(liquidazioni, LIPE, dichiarazione IVA)</small></span>
              </label>
              {form.haDipendenti && (
                <label style={{ display: "flex", alignItems: "flex-start", gap: 10, cursor: "pointer", fontSize: 14,
                  padding: 12, borderRadius: 8, border: "1px solid #d97706", background: "rgba(217,119,6,0.08)" }}>
                  <input type="checkbox" checked={datiContributiviProvvisori}
                    onChange={e => {
                      const attiva = e.target.checked;
                      setDatiContributiviProvvisori(attiva);
                      if (tentato) setErrori(validaAzienda(form, { datiContributiviProvvisori: attiva }));
                    }} />
                  <span>
                    <strong>Non ho ancora i codici INPS/INAIL: li inserisco dopo</strong><br />
                    <small style={{ opacity: 0.8 }}>
                      Salva l'anagrafica senza inventare codici 000000. L'azienda resta segnata «da completare»
                      nell'archivio e F24, UniEmens e INAIL restano bloccati finché non inserisci i codici veri.
                    </small>
                  </span>
                </label>
              )}
            </div>
          </div>

          {/* SEZIONE 5 – Dati INPS, INAIL, CCNL & Legale Rappresentante */}
          <div data-sezione-azienda="contributivo" ref={el => { refSezioni.current["contributivo"] = el; }}
            style={sezioneStyle(sezioniConErrori.some(s => s.id === "contributivo"))}>
            <h4 style={titoloSezioneStyle(sezioniConErrori.some(s => s.id === "contributivo"))}>
              {SEZIONI[4].titolo}
            </h4>
            <div style={grigliaStyle}>
              <Campo label="Matricola INPS" errore={errori.matricolaInps} facoltativo={!form.haDipendenti || datiContributiviProvvisori}>
                <input style={inputStyle(errori.matricolaInps)}
                  placeholder="Es. 1234567890" value={form.matricolaInps ?? ""}
                  onChange={set("matricolaInps")} />
              </Campo>
              <Campo label="Codice Sede INPS" errore={errori.codiceSedeInps} facoltativo={!form.haDipendenti || datiContributiviProvvisori}>
                <input style={inputStyle(errori.codiceSedeInps)}
                  placeholder="Es. 4500 (Milano)" value={form.codiceSedeInps ?? ""}
                  onChange={set("codiceSedeInps")} />
              </Campo>
              <Campo label="CSC INPS (5 cifre)" errore={errori.cscInps} facoltativo={!form.haDipendenti || datiContributiviProvvisori}>
                <input style={inputStyle(errori.cscInps)}
                  placeholder="Es. 10101 (Terziario)" value={form.cscInps ?? ""}
                  onChange={set("cscInps")} maxLength={5} />
              </Campo>
              <Campo label="P.A.T. INAIL" errore={errori.patInail} facoltativo={!form.haDipendenti || datiContributiviProvvisori}>
                <input style={inputStyle(errori.patInail)}
                  placeholder="Es. 12345678/90" value={form.patInail ?? ""}
                  onChange={set("patInail")} />
              </Campo>
              <Campo label="CCNL Predefinito" errore={errori.ccnlDefault} facoltativo={!form.haDipendenti || datiContributiviProvvisori}>
                <select style={selectStyle(errori.ccnlDefault)} value={form.ccnlDefault ?? ""}
                  onChange={set("ccnlDefault")}>
                  <option value="">Scegli CCNL...</option>
                  {form.ccnlDefault && !CCNL_OPZIONI.some(c => c.codice === form.ccnlDefault) && (
                    <option value={form.ccnlDefault}>{form.ccnlDefault}</option>
                  )}
                  {CCNL_OPZIONI.map(c => (
                    <option key={c.codice} value={c.codice}>{c.label}</option>
                  ))}
                </select>
              </Campo>
              <Campo label="Livello Predefinito" errore={errori.livelloDefault} facoltativo={!form.haDipendenti || datiContributiviProvvisori}>
                <input style={inputStyle(errori.livelloDefault)}
                  placeholder="Es. 3, 4, B1" value={form.livelloDefault ?? ""}
                  onChange={set("livelloDefault")} />
              </Campo>
              <Campo label="Legale Rappresentante (Nome)" errore={errori.legaleRappresentanteNome} facoltativo>
                <input style={inputStyle(errori.legaleRappresentanteNome)}
                  placeholder="Es. Mario" value={form.legaleRappresentanteNome ?? ""}
                  onChange={set("legaleRappresentanteNome")} />
              </Campo>
              <Campo label="Legale Rappresentante (Cognome)" errore={errori.legaleRappresentanteCognome} facoltativo>
                <input style={inputStyle(errori.legaleRappresentanteCognome)}
                  placeholder="Es. Rossi" value={form.legaleRappresentanteCognome ?? ""}
                  onChange={set("legaleRappresentanteCognome")} />
              </Campo>
              <Campo label="Legale Rappresentante (Codice Fiscale)" errore={errori.legaleRappresentanteCodiceFiscale} facoltativo>
                <input style={inputStyle(errori.legaleRappresentanteCodiceFiscale)}
                  placeholder="Es. RSSMRA80A01F205X" value={form.legaleRappresentanteCodiceFiscale ?? ""}
                  onChange={set("legaleRappresentanteCodiceFiscale")} maxLength={16} />
              </Campo>
              <Campo label="Fondi di settore a carico del dipendente" facoltativo>
                <select style={selectStyle(undefined)} value={String(form.contributiFondiSettore ?? 0)}
                  onChange={e => setForm({ ...form, contributiFondiSettore: Number(e.target.value) })}>
                  {form.contributiFondiSettore != null
                    && !FONDI_SETTORE.some(f => f.valore === form.contributiFondiSettore) && (
                    <option value={String(form.contributiFondiSettore)}>
                      {(form.contributiFondiSettore * 100).toLocaleString("it-IT")}%
                    </option>
                  )}
                  {FONDI_SETTORE.map(f => (
                    <option key={f.valore} value={String(f.valore)}>{f.label}</option>
                  ))}
                </select>
              </Campo>
            </div>
            <label style={{ display: "flex", alignItems: "flex-start", gap: 10, cursor: "pointer",
              fontSize: 14, marginTop: 12 }}>
              <input type="checkbox" checked={form.oltreQuindiciDipendenti ?? false}
                onChange={e => setForm({ ...form, oltreQuindiciDipendenti: e.target.checked })} />
              <span>
                Azienda con più di 15 dipendenti (CIG)
                <small style={{ display: "block", opacity: 0.7 }}>
                  Aggiunge lo 0,30% alla quota INPS trattenuta al dipendente (9,19% → 9,49%).
                  Il conteggio di legge è la media del semestre, con i part-time pro quota:
                  vale la tua dichiarazione, non il numero di dipendenti in archivio.
                </small>
              </span>
            </label>
          </div>

          {/* pulsanti finali */}
          <div style={{ display: "flex", gap: 12, marginTop: 8, flexWrap: "wrap" }}>
            {inModificaId ? (
              <>
                <button className="btn primary" type="submit">Salva correzioni</button>
                <button className="btn secondary" type="button" onClick={annullaModifica}>Annulla</button>
              </>
            ) : (
              <button className="btn primary" type="submit" style={{ minWidth: 180 }}>
                Aggiungi azienda
              </button>
            )}
          </div>
        </form>
      )}

        </div>
      </div>
    </section>
  );
}

export function AziendeLaterale({
  refreshKey,
  onNavigate,
}: {
  refreshKey: number;
  onNavigate: (sezione: "dipendenti" | "cedolini") => void;
}) {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [ricerca, setRicerca] = useState("");
  const [correnteId, setCorrenteId] = useState<string | null>(() => leggiAziendaCorrenteId());
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    let vivo = true;
    api.aziende()
      .then((elenco) => {
        if (!vivo) return;
        setAziende(elenco);
        const corrente = leggiAziendaCorrenteId();
        setCorrenteId(corrente && elenco.some((a) => a.id === corrente) ? corrente : null);
      })
      .catch((e) => {
        if (vivo) setErrore(e instanceof Error ? e.message : "Errore nel caricamento aziende");
      });
    return () => { vivo = false; };
  }, [refreshKey]);
  useEffect(() => {
    const aggiorna = () => setCorrenteId(leggiAziendaCorrenteId());
    window.addEventListener(AZIENDA_CORRENTE_CAMBIATA, aggiorna);
    window.addEventListener("storage", aggiorna);
    return () => {
      window.removeEventListener(AZIENDA_CORRENTE_CAMBIATA, aggiorna);
      window.removeEventListener("storage", aggiorna);
    };
  }, []);

  const testo = ricerca.trim().toLowerCase();
  const filtrate = testo
    ? aziende.filter((a) =>
      [a.ragioneSociale, a.partitaIva, labelSettore(a.settore), a.comune, a.provincia]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(testo)))
    : aziende;

  function scegli(a: Azienda) {
    salvaAziendaCorrenteId(a.id);
    setCorrenteId(a.id);
  }

  function apri(a: Azienda, sezione: "dipendenti" | "cedolini") {
    scegli(a);
    onNavigate(sezione);
  }

  function vaiAlForm() {
    document.getElementById("nuova-azienda-form")?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  return (
    <aside className="glass-card module-rail aziende-rail">
      <div className="aziende-rail-head">
        <div>
          <h2>Aziende esistenti</h2>
          <p>Scegli l'azienda prima di lavorare o crearne una nuova.</p>
        </div>
        <span className="count blue">{aziende.length}</span>
      </div>

      <label className="azienda-search">
        <span>⌕</span>
        <input
          value={ricerca}
          onChange={(e) => setRicerca(e.target.value)}
          placeholder="Cerca nome, P. IVA, comune..."
        />
      </label>

      {errore && <p className="errore" style={{ marginTop: 10 }}>{errore}</p>}

      {aziende.length === 0 ? (
        <div className="empty-state">Nessuna azienda ancora. Compila il modulo a sinistra.</div>
      ) : filtrate.length === 0 ? (
        <div className="empty-state">Nessuna azienda trovata con questa ricerca.</div>
      ) : (
        <div className="azienda-rail-list">
          {filtrate.map((a) => {
            const selezionata = correnteId === a.id;
            return (
              <article className={`azienda-rail-card ${selezionata ? "selected" : ""}`} key={a.id}>
                <button className="azienda-rail-main" type="button" onClick={() => scegli(a)}>
                  <span className="azienda-rail-avatar">{a.ragioneSociale.slice(0, 2).toUpperCase()}</span>
                  <span className="azienda-rail-info">
                    <strong>{a.ragioneSociale}</strong>
                    <small>
                      {[a.partitaIva ? `P.IVA ${a.partitaIva}` : null, a.settore ? labelSettore(a.settore) : null, a.comune ? `${a.comune}${a.provincia ? ` (${a.provincia})` : ""}` : null].filter(Boolean).join(" · ") || "Anagrafica azienda"}
                    </small>
                  </span>
                  {selezionata && <em>Scelta</em>}
                </button>
                <div className="azienda-rail-actions">
                  <button className="mini-action" type="button" onClick={() => scegli(a)}>
                    Scegli
                  </button>
                  <button className="mini-action" type="button" onClick={() => apri(a, "dipendenti")}>
                    Dipendenti
                  </button>
                  <button className="mini-action" type="button" onClick={() => apri(a, "cedolini")}>
                    Cedolini
                  </button>
                </div>
              </article>
            );
          })}
        </div>
      )}

      <button className="btn secondary aziende-rail-new" type="button" onClick={vaiAlForm}>
        + Crea nuova azienda
      </button>
    </aside>
  );
}

// ---------- stili helper ----------
function sezioneStyle(haErrori: boolean): React.CSSProperties {
  return {
    marginBottom: 20,
    padding: 18,
    borderRadius: 8,
    border: haErrori ? "1.5px solid #e74c3c" : "1px solid var(--line)",
    background: haErrori ? "rgba(231,76,60,0.05)" : "var(--panel-strong)",
    transition: "border-color 0.2s, background 0.2s",
  };
}

function titoloSezioneStyle(haErrori: boolean): React.CSSProperties {
  return {
    margin: "0 0 14px",
    fontSize: 15,
    fontWeight: 700,
    color: haErrori ? "#e74c3c" : "var(--accent, #4a9eff)",
    display: "flex",
    alignItems: "center",
    gap: 6,
  };
}

const grigliaStyle: React.CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
  gap: 14,
};
