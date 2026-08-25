import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { Building2, FileUp, Plus, Search, UserPlus, Users, X } from "lucide-react";
import { api, type Azienda, type Cedolino, type DipendenteRapporto, type Livello } from "../api";
import { CaricaUnilav } from "./CaricaUnilav";
import { ComuneAutocomplete } from "../ComuneAutocomplete";
import { salvaAziendaCorrenteId, scegliAziendaPreferita } from "../aziendaCorrente";
import { preparaDatiGateRapporto } from "../rapportoFiscaleForm";
import { creaSequenziatoreRichieste } from "../ultimaRichiesta";

const DEFAULT_CCNL = "COMMERCIO_TERZIARIO";
const DEFAULT_LIVELLO = "3";

type DipendenteConAzienda = DipendenteRapporto & {
  aziendaId: string;
  aziendaNome: string;
};

async function caricaDipendentiAziende(
  aziende: Azienda[],
  signal?: AbortSignal,
): Promise<DipendenteConAzienda[]> {
  const gruppi = await Promise.all(aziende.map(async (azienda) => {
    const dipendenti = await api.dipendenti(azienda.id, signal);
    return dipendenti.map((dipendente) => ({
      ...dipendente,
      aziendaId: azienda.id,
      aziendaNome: azienda.ragioneSociale,
    }));
  }));
  return gruppi.flat();
}

function codiceCcnlAzienda(azienda: Azienda | null): string {
  return azienda?.ccnlDefault?.trim() || DEFAULT_CCNL;
}

function livelloAzienda(azienda: Azienda | null): string {
  return azienda?.livelloDefault?.trim() || DEFAULT_LIVELLO;
}

function nomeCcnl(codice: string): string {
  const nomi: Record<string, string> = {
    COMMERCIO_TERZIARIO: "Commercio",
    TRASPORTI_LOGISTICA: "Logistica",
    EDILIZIA_INDUSTRIA: "Edilizia",
    METALMECCANICI_INDUSTRIA: "Metalmeccanici",
    TURISMO_PUBBLICI_ESERCIZI: "Turismo",
    MULTISERVIZI_PULIZIE: "Multiservizi",
    STUDI_PROFESSIONALI: "Studi professionali",
    ALIMENTARE_INDUSTRIA: "Alimentare",
    AGRICOLTURA_OPERAI: "Agricoltura",
  };
  return nomi[codice] ?? codice;
}

const oggiIso = new Date().toISOString().slice(0, 10);

const formVuoto = {
  codiceFiscale: "", nome: "", cognome: "", livello: DEFAULT_LIVELLO, iban: "", qualifica: "Impiegato",
  regione: "", codiceCatastaleComune: "", comuneNome: "",
  dataDecorrenzaDomicilioFiscale: "", riferimentoProgressiviFiscaliPregressi: "",
  dataAssunzione: oggiIso, tipoContratto: "Tempo indeterminato", oreSettimanali: "40",
  numeroMensilita: "14", percentualePartTime: "100", scattiAnzianita: "0", superminimo: "0",
  ferieResidueIniziali: "0", rolResiduiIniziali: "", exFestivitaResidueIniziali: "",
  addizionaleRegionaleConguaglioAnnuo: "", addizionaleComunaleSaldoAnnuo: "",
  addizionaleComunaleAccontoAnnuo: "",
};

export function Dipendenti({ onApri }: { onApri: (rapportoId: string) => void }) {
  const [tutteAziende, setTutteAziende] = useState<Azienda[]>([]);
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [filtroAziendaId, setFiltroAziendaId] = useState("");
  const [lista, setLista] = useState<DipendenteConAzienda[]>([]);
  const [livelli, setLivelli] = useState<Livello[]>([]);
  const [form, setForm] = useState(formVuoto);
  const [vista, setVista] = useState<"archivio" | "nuovo" | "unilav">("archivio");
  const [ricerca, setRicerca] = useState("");
  const [caricamento, setCaricamento] = useState(true);
  const [salvataggio, setSalvataggio] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);
  const richiesteAziende = useRef(creaSequenziatoreRichieste());
  const richiesteLista = useRef(creaSequenziatoreRichieste());
  const richiesteLivelli = useRef(creaSequenziatoreRichieste());
  const montato = useRef(true);
  const aziendaCorrenteRef = useRef<string | null>(null);
  const [scopeLista, setScopeLista] = useState<string | null>(null);
  aziendaCorrenteRef.current = azienda?.id ?? null;
  const listaCorrente = useMemo(
    () => scopeLista === (azienda?.id ?? null) ? lista : [],
    [azienda?.id, lista, scopeLista],
  );

  const caricaLista = useCallback(async (aziendaRichiesta: Azienda) => {
    const richiesta = richiesteLista.current.apri();
    const aziendaId = aziendaRichiesta.id;
    setLista([]);
    setScopeLista(null);
    setCaricamento(true);
    try {
      const dipendenti = await caricaDipendentiAziende([aziendaRichiesta], richiesta.signal);
      if (!richiesta.attiva() || aziendaCorrenteRef.current !== aziendaId) return;
      setLista(dipendenti);
      setScopeLista(aziendaId);
    } finally {
      if (richiesta.attiva()) setCaricamento(false);
    }
  }, []);

  useEffect(() => {
    const richiesta = richiesteAziende.current.apri();
    (async () => {
      try {
        const aziende = await api.aziende(richiesta.signal);
        if (!richiesta.attiva()) return;
        setTutteAziende(aziende);
        const a = scegliAziendaPreferita(aziende);
        aziendaCorrenteRef.current = a?.id ?? null;
        setAzienda(a);
        setFiltroAziendaId(a?.id ?? "");
        if (a) await caricaLista(a);
        else {
          setLista([]);
          setScopeLista(null);
          setCaricamento(false);
        }
      } catch (e) {
        if (e instanceof DOMException && e.name === "AbortError") return;
        if (richiesta.attiva()) {
          setErrore(e instanceof Error ? e.message : "Errore di caricamento");
          setCaricamento(false);
        }
      }
    })();
    return () => richiesta.invalida();
  }, [caricaLista]);

  useEffect(() => {
    if (!azienda) return;
    const richiesta = richiesteLivelli.current.apri();
    const codice = codiceCcnlAzienda(azienda);
    const livelloDefault = livelloAzienda(azienda);
    setForm((f) => ({ ...f, livello: livelloDefault }));
    setLivelli([]);
    api.livelli(codice, richiesta.signal)
      .then((r) => {
        if (!richiesta.attiva() || aziendaCorrenteRef.current !== azienda.id) return;
        setLivelli(r.livelli);
        setForm((f) => {
          const livelloOk = r.livelli.some(l => l.codice === f.livello);
          return { ...f, livello: livelloOk ? f.livello : (r.livelli[0]?.codice ?? livelloDefault) };
        });
      })
      .catch((e) => {
        if (!(e instanceof DOMException && e.name === "AbortError") && richiesta.attiva())
          setLivelli([]);
      });
    return () => richiesta.invalida();
  }, [azienda]);

  useEffect(() => {
    montato.current = true;
    const sequenzaAziende = richiesteAziende.current;
    const sequenzaLista = richiesteLista.current;
    const sequenzaLivelli = richiesteLivelli.current;
    return () => {
      montato.current = false;
      sequenzaAziende.invalida();
      sequenzaLista.invalida();
      sequenzaLivelli.invalida();
    };
  }, []);

  async function cambiaAzienda(id: string) {
    const a = tutteAziende.find(x => x.id === id) ?? null;
    if (!a) return;
    richiesteLista.current.invalida();
    aziendaCorrenteRef.current = a.id;
    setFiltroAziendaId(id);
    setAzienda(a);
    salvaAziendaCorrenteId(a.id);
    setErrore(null);
    setVista("archivio");
    try {
      await caricaLista(a);
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") return;
      if (aziendaCorrenteRef.current === a.id) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
        setCaricamento(false);
      }
    }
  }

  async function aggiungi(e: FormEvent) {
    e.preventDefault();
    if (!azienda) {
      setErrore("Prima aggiungi un'azienda oppure importa un UNILAV: ogni dipendente deve essere collegato a un datore di lavoro.");
      return;
    }
    setErrore(null);
    const datiGate = preparaDatiGateRapporto(form);
    if (!datiGate.ok) {
      setErrore(`Completa i dati richiesti prima di salvare. ${datiGate.errori.join(" ")}`);
      return;
    }
    setSalvataggio(true);
    try {
      await api.creaDipendente(azienda.id, {
        codiceFiscale: form.codiceFiscale.trim().toUpperCase(),
        nome: form.nome.trim(),
        cognome: form.cognome.trim(),
        codiceCcnl: codiceCcnlAzienda(azienda),
        livello: form.livello || livelloAzienda(azienda),
        iban: form.iban.trim() || undefined,
        qualifica: form.qualifica,
        ...datiGate.valore,
        dataAssunzione: form.dataAssunzione || undefined,
        tipoContratto: form.tipoContratto,
        oreSettimanali: Number(form.oreSettimanali) || 40,
        numeroMensilita: Number(form.numeroMensilita) || 14,
        partTime: partTime,
        percentualePartTime: partTime ? Number(form.percentualePartTime) || 100 : 100,
        scattiAnzianita: Number(form.scattiAnzianita) || 0,
        superminimo: Number(form.superminimo) || 0,
        ferieResidueIniziali: Number(form.ferieResidueIniziali) || 0,
      });
      if (aziendaCorrenteRef.current !== azienda.id) return;
      await caricaLista(azienda);
      if (aziendaCorrenteRef.current !== azienda.id) return;
      setForm(formVuoto);
      setPartTime(false);
      setVista("archivio");
    } catch (e) {
      if (aziendaCorrenteRef.current === azienda.id)
        setErrore(e instanceof Error ? e.message : "Errore nel salvataggio");
    } finally {
      setSalvataggio(false);
    }
  }

  const set = (k: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));
  const [partTime, setPartTime] = useState(false);

  // Dopo l'import UNILAV: porta la lista sull'azienda (datore) creata/trovata dal PDF.
  const dopoImport = async (az: { id: string; ragioneSociale: string }) => {
    richiesteLista.current.invalida();
    aziendaCorrenteRef.current = az.id;
    setAzienda(az);
    setFiltroAziendaId(az.id);
    setErrore(null);
    setLista([]);
    setScopeLista(null);
    setCaricamento(true);
    salvaAziendaCorrenteId(az.id);
    setTutteAziende((correnti) =>
      correnti.some((a) => a.id === az.id) ? correnti : [...correnti, az]);
    try {
      const richiestaAziende = richiesteAziende.current.apri();
      const aggiornamentoAziende = api.aziende(richiestaAziende.signal)
        .then((aziende) => {
          if (richiestaAziende.attiva()) setTutteAziende(aziende);
        });
      await Promise.all([aggiornamentoAziende, caricaLista(az)]);
    } catch (e) {
      if (!montato.current || (e instanceof DOMException && e.name === "AbortError")) return;
      try {
        if (aziendaCorrenteRef.current === az.id) await caricaLista(az);
      } catch { /* ignora */ }
    }
  };

  const listaFiltrata = useMemo(() => {
    const testo = ricerca.trim().toLocaleLowerCase("it");
    if (!testo) return listaCorrente;
    return listaCorrente.filter((dipendente) => [
      dipendente.nominativo,
      dipendente.qualifica,
      dipendente.livello,
      dipendente.codiceCcnl,
      dipendente.aziendaNome,
      dipendente.dipendenteId,
    ].some((valore) => String(valore).toLocaleLowerCase("it").includes(testo)));
  }, [listaCorrente, ricerca]);

  const ccnlPresenti = new Set(listaCorrente.map((dipendente) => dipendente.codiceCcnl)).size;

  function apriNuovo() {
    if (!azienda) {
      setErrore(tutteAziende.length === 0
        ? "Prima registra un'azienda: ogni dipendente deve essere collegato a un datore di lavoro."
        : "Seleziona l'azienda in cui inserire il dipendente.");
      return;
    }
    setErrore(null);
    setVista("nuovo");
  }

  return (
    <section className="glass-card employee-workspace">
      <header className="employee-commandbar">
        <div className="employee-command-title">
          <span><Users size={20} aria-hidden="true" /></span>
          <div>
            <h1>Dipendenti</h1>
            <p>{azienda ? azienda.ragioneSociale : "Seleziona un'azienda per iniziare"}</p>
          </div>
        </div>
        <nav className="employee-view-tabs" aria-label="Gestione dipendenti">
          <button className={vista === "archivio" ? "active" : ""} type="button" onClick={() => setVista("archivio")}><Users size={15} /> Archivio</button>
          <button className={vista === "nuovo" ? "active" : ""} type="button" onClick={apriNuovo}><UserPlus size={15} /> Nuovo</button>
          <button className={vista === "unilav" ? "active" : ""} type="button" onClick={() => setVista("unilav")}><FileUp size={15} /> Importa UNILAV</button>
        </nav>
      </header>

      <div className="employee-companybar">
        <Building2 size={17} aria-hidden="true" />
        {tutteAziende.length === 0 ? (
          <p>Nessuna azienda registrata. <a href="#aziende">Apri Aziende</a></p>
        ) : (
          <label>
            <span>Azienda di lavoro</span>
            <select value={filtroAziendaId} onChange={(e) => { void cambiaAzienda(e.target.value); }}>
              {tutteAziende.map((voce) => <option key={voce.id} value={voce.id}>{voce.ragioneSociale}</option>)}
            </select>
          </label>
        )}
      </div>

      {errore && <div className="employee-error" role="alert">{errore}</div>}

      {vista === "archivio" && (
        <div className="employee-pane">
          <div className="employee-stats" aria-label="Riepilogo dipendenti">
            <div><span>Dipendenti</span><strong>{listaCorrente.length}</strong><small>nell'azienda scelta</small></div>
            <div><span>Qualifiche</span><strong>{new Set(listaCorrente.map((d) => d.qualifica)).size}</strong><small>profili presenti</small></div>
            <div><span>CCNL</span><strong>{ccnlPresenti}</strong><small>contratti applicati</small></div>
          </div>

          <div className="employee-list-toolbar">
            <label className="employee-search" htmlFor="cerca-dipendente">
              <Search size={16} aria-hidden="true" />
              <input id="cerca-dipendente" value={ricerca} onChange={(e) => setRicerca(e.target.value)} placeholder="Cerca nome, matricola, qualifica o CCNL" />
              {ricerca && <button type="button" title="Cancella ricerca" onClick={() => setRicerca("")}><X size={15} /></button>}
            </label>
            <button className="btn primary" type="button" onClick={apriNuovo}><Plus size={16} /> Nuovo dipendente</button>
          </div>

          {caricamento ? (
            <div className="empty-state">Caricamento dipendenti…</div>
          ) : listaCorrente.length === 0 ? (
            <div className="employee-empty">
              <UserPlus size={28} aria-hidden="true" />
              <strong>Nessun dipendente registrato</strong>
              <p>Inserisci il primo lavoratore manualmente oppure importa il documento UNILAV.</p>
              <div><button className="btn primary" type="button" onClick={apriNuovo}>Inserisci dipendente</button><button className="btn secondary" type="button" onClick={() => setVista("unilav")}>Importa UNILAV</button></div>
            </div>
          ) : listaFiltrata.length === 0 ? (
            <div className="empty-state">Nessun dipendente corrisponde alla ricerca.</div>
          ) : (
            <div className="table-wrap employee-table-wrap">
              <table className="data-table employee-table">
                <thead><tr><th>Dipendente</th><th>Matricola</th><th>Qualifica</th><th>Livello</th><th>CCNL</th><th className="right">Azioni</th></tr></thead>
                <tbody>{listaFiltrata.map((d) => (
                  <tr key={d.rapportoId} onClick={() => onApri(d.rapportoId)}>
                    <td><div className="person-cell"><span>{iniziali(d.nominativo)}</span><div><strong>{d.nominativo || "Nominativo mancante"}</strong><small>{d.aziendaNome}</small></div></div></td>
                    <td className="num">{d.dipendenteId.slice(0, 6).toUpperCase()}</td>
                    <td>{d.qualifica}</td><td>{d.livello}</td><td>{nomeCcnl(d.codiceCcnl)}</td>
                    <td className="right"><button className="mini-action" type="button" onClick={(e) => { e.stopPropagation(); onApri(d.rapportoId); }}>Apri scheda</button></td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {vista === "nuovo" && (
        <div className="employee-pane employee-form-pane">
          <div className="employee-pane-heading"><div><h2>Nuovo dipendente</h2><p>{azienda ? `Rapporto collegato a ${azienda.ragioneSociale}` : "Seleziona prima l'azienda"}</p></div><button className="btn secondary" type="button" onClick={() => setVista("archivio")}>Annulla</button></div>
        <form className="form-grid employee-create-form" onSubmit={aggiungi}>
          <label>
            <span>Codice fiscale</span>
            <input value={form.codiceFiscale} onChange={set("codiceFiscale")} placeholder="RSSMRA80A01F205X" required maxLength={16} />
          </label>
          <label>
            <span>Qualifica</span>
            <select value={form.qualifica} onChange={set("qualifica")}>
              <option>Impiegato</option>
              <option>Operaio</option>
              <option>Quadro</option>
              <option>Dirigente</option>
              <option>Apprendista</option>
            </select>
          </label>
          <label>
            <span>Nome</span>
            <input value={form.nome} onChange={set("nome")} required />
          </label>
          <label>
            <span>Cognome</span>
            <input value={form.cognome} onChange={set("cognome")} required />
          </label>
          <label>
            <span>Livello CCNL {nomeCcnl(codiceCcnlAzienda(azienda))}</span>
            <select value={form.livello} onChange={set("livello")}>
              {livelli.length === 0
                ? <option value={livelloAzienda(azienda)}>{livelloAzienda(azienda)}</option>
                : livelli.map((l) => <option key={l.codice} value={l.codice}>{l.codice} — {l.descrizione}</option>)}
            </select>
          </label>
          <label>
            <span>IBAN <small>(facoltativo)</small></span>
            <input value={form.iban} onChange={set("iban")} placeholder="IT.." />
          </label>

          <h3 className="wide-field">Domicilio fiscale e progressivi</h3>
          <label className="wide-field">
            <span>Comune del domicilio fiscale <small>(seleziona un risultato)</small></span>
            <ComuneAutocomplete
              valore={form.comuneNome}
              placeholder="Es. Milano"
              required
              onInputChange={(valore) => setForm((f) => ({
                ...f,
                comuneNome: valore,
                regione: "",
                codiceCatastaleComune: "",
              }))}
              onSelect={(c) =>
                setForm((f) => ({ ...f, comuneNome: c.nome, codiceCatastaleComune: c.cat, regione: c.reg }))}
            />
          </label>
          <label>
            <span>Regione del domicilio fiscale</span>
            <input value={form.regione} onChange={set("regione")} required />
          </label>
          <label>
            <span>Codice catastale del comune</span>
            <input value={form.codiceCatastaleComune} onChange={set("codiceCatastaleComune")}
              maxLength={4} placeholder="Es. F205" required />
          </label>
          <label>
            <span>Decorrenza domicilio fiscale</span>
            <input type="date" value={form.dataDecorrenzaDomicilioFiscale}
              onChange={set("dataDecorrenzaDomicilioFiscale")} required />
          </label>
          <label>
            <span>Fonte progressivi fiscali <small>(o assenza pregressi)</small></span>
            <input value={form.riferimentoProgressiviFiscaliPregressi}
              onChange={set("riferimentoProgressiviFiscaliPregressi")}
              placeholder="CU provvisoria / import certificato / assunzione nel mese" required />
          </label>

          <h3 className="wide-field">Inquadramento e retribuzione</h3>
          <label>
            <span>Data assunzione</span>
            <input type="date" value={form.dataAssunzione} onChange={set("dataAssunzione")} required />
          </label>
          <label>
            <span>Tipo contratto</span>
            <select value={form.tipoContratto} onChange={set("tipoContratto")}>
              <option>Tempo indeterminato</option>
              <option>Tempo determinato</option>
            </select>
          </label>
          <label>
            <span>Ore settimanali</span>
            <input type="number" min={1} max={48} value={form.oreSettimanali} onChange={set("oreSettimanali")} />
          </label>
          <label>
            <span>Mensilità</span>
            <select value={form.numeroMensilita} onChange={set("numeroMensilita")}>
              <option value="13">13 (con tredicesima)</option>
              <option value="14">14 (con quattordicesima)</option>
            </select>
          </label>
          <label>
            <span>Part-time</span>
            <span style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <input type="checkbox" checked={partTime} onChange={(e) => setPartTime(e.target.checked)} />
              <input type="number" min={5} max={100} value={form.percentualePartTime}
                onChange={set("percentualePartTime")} disabled={!partTime}
                aria-label="Percentuale part-time" style={{ maxWidth: 90 }} /> %
            </span>
          </label>
          <label>
            <span>Scatti di anzianità maturati</span>
            <input type="number" min={0} max={12} value={form.scattiAnzianita} onChange={set("scattiAnzianita")} />
          </label>
          <label>
            <span>Superminimo mensile <small>(EUR, oltre il minimo CCNL)</small></span>
            <input type="number" min={0} step="0.01" value={form.superminimo} onChange={set("superminimo")} />
          </label>
          <label>
            <span>Ferie riportate al 1° gennaio <small>(giorni)</small></span>
            <input type="number" step="0.01" value={form.ferieResidueIniziali}
              onChange={set("ferieResidueIniziali")} />
          </label>
          <label>
            <span>ROL riportati al 1° gennaio <small>(ore; indica anche 0)</small></span>
            <input type="number" step="0.01" value={form.rolResiduiIniziali}
              onChange={set("rolResiduiIniziali")} required />
          </label>
          <label>
            <span>Ex festività riportate al 1° gennaio <small>(ore; indica anche 0)</small></span>
            <input type="number" step="0.01" value={form.exFestivitaResidueIniziali}
              onChange={set("exFestivitaResidueIniziali")} required />
          </label>

          <h3 className="wide-field">Addizionali dal conguaglio</h3>
          <p className="hint wide-field">
            Compila tutti gli importi annui. Digita 0 quando il conguaglio certifica zero:
            un campo lasciato vuoto blocca il cedolino archiviabile.
          </p>
          <label>
            <span>Addizionale regionale anno precedente €</span>
            <input type="number" min={0} step="0.01"
              value={form.addizionaleRegionaleConguaglioAnnuo}
              onChange={set("addizionaleRegionaleConguaglioAnnuo")} required />
          </label>
          <label>
            <span>Addizionale comunale, saldo €</span>
            <input type="number" min={0} step="0.01"
              value={form.addizionaleComunaleSaldoAnnuo}
              onChange={set("addizionaleComunaleSaldoAnnuo")} required />
          </label>
          <label>
            <span>Addizionale comunale, acconto €</span>
            <input type="number" min={0} step="0.01"
              value={form.addizionaleComunaleAccontoAnnuo}
              onChange={set("addizionaleComunaleAccontoAnnuo")} required />
          </label>

          <AnteprimaRetribuzione
            azienda={azienda}
            livelli={livelli}
            livello={form.livello || livelloAzienda(azienda)}
            qualifica={form.qualifica}
            numeroMensilita={Number(form.numeroMensilita) || 14}
            scattiAnzianita={Number(form.scattiAnzianita) || 0}
            superminimo={Number(form.superminimo) || 0}
            partTime={partTime}
            percentualePartTime={partTime ? Number(form.percentualePartTime) || 100 : 100}
            regione={form.regione}
            codiceCatastaleComune={form.codiceCatastaleComune}
          />

          <button className="btn primary wide-field" type="submit" disabled={salvataggio || !azienda}>
            {salvataggio ? "Salvataggio…" : "Salva dipendente"}
          </button>
        </form>
        </div>
      )}

      {vista === "unilav" && <div className="employee-pane employee-unilav-pane"><CaricaUnilav onImportato={dopoImport} /></div>}
    </section>
  );
}

// Anteprima retributiva dell'assunzione: paga base dal minimo tabellare CCNL del
// livello scelto (+ superminimo, riproporzionata sul part-time) e stima del netto
// col motore reale su un mese ordinario. Solo lettura: i valori restano da validare.
function AnteprimaRetribuzione({
  azienda, livelli, livello, qualifica, numeroMensilita, scattiAnzianita,
  superminimo, partTime, percentualePartTime, regione, codiceCatastaleComune,
}: {
  azienda: Azienda | null;
  livelli: Livello[];
  livello: string;
  qualifica: string;
  numeroMensilita: number;
  scattiAnzianita: number;
  superminimo: number;
  partTime: boolean;
  percentualePartTime: number;
  regione: string;
  codiceCatastaleComune: string;
}) {
  const [stima, setStima] = useState<Cedolino | null>(null);
  const [stimaErrore, setStimaErrore] = useState("");
  const [stimando, setStimando] = useState(false);

  const livelloSel = livelli.find((l) => l.codice === livello) ?? null;
  const coeff = partTime ? Math.min(100, Math.max(5, percentualePartTime)) / 100 : 1;
  const lordoBase = livelloSel === null ? null : (livelloSel.minimoTabellare + superminimo) * coeff;
  const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  async function stimaNetto() {
    if (!azienda) return;
    setStimando(true);
    setStimaErrore("");
    setStima(null);
    try {
      const adesso = new Date();
      setStima(await api.calcolaCedolino({
        codiceCcnl: codiceCcnlAzienda(azienda),
        livello, qualifica, numeroMensilita, scattiAnzianita,
        regione, codiceCatastaleComune,
        superminimo, partTime, percentualePartTime,
      }, { anno: adesso.getFullYear(), mese: adesso.getMonth() + 1, oreStraordinario: 0 }));
    } catch {
      setStimaErrore("Stima non disponibile: controlla livello e comune di residenza.");
    } finally {
      setStimando(false);
    }
  }

  return (
    <div className="wide-field" aria-live="polite">
      <p className="muto">
        {livelloSel
          ? <>Paga base mensile CCNL ({livelloSel.codice}): <strong>{euro(livelloSel.minimoTabellare)} EUR</strong>
            {superminimo > 0 && <> + superminimo <strong>{euro(superminimo)} EUR</strong></>}
            {partTime && <> · part-time {percentualePartTime}%</>}
            {lordoBase !== null && <> → lordo di partenza <strong>{euro(lordoBase)} EUR</strong></>}
            {" "}su {numeroMensilita} mensilità.</>
          : "Scegli un livello CCNL per vedere la paga base."}
      </p>
      <p style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "center" }}>
        <button className="btn secondary" type="button" disabled={stimando || !azienda || !livelloSel} onClick={() => void stimaNetto()}>
          {stimando ? "Calcolo…" : "Stima netto e costo azienda"}
        </button>
        {stima && (
          <span className="muto">
            Netto in busta ≈ <strong>{euro(stima.nettoInBusta)} EUR</strong> ·
            costo azienda ≈ <strong>{euro(stima.costoAzienda)} EUR</strong>
            {" "}(mese ordinario, stima da validare col Consulente del Lavoro)
          </span>
        )}
        {stimaErrore && <span className="muto">{stimaErrore}</span>}
      </p>
    </div>
  );
}

function iniziali(nominativo: string): string {
  const parti = nominativo.trim().split(/\s+/).filter(Boolean);
  if (parti.length === 0) return "—";
  return parti.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? "").join("");
}
