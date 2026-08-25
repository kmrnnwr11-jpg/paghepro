import { useEffect, useState, type FormEvent } from "react";
import { ArrowLeft, BriefcaseBusiness, Check, CircleUserRound, FileText, Pencil, ReceiptText, Save, X } from "lucide-react";
import { api, scaricaBlob, type CedolinoArchiviato, type RapportoDettaglio } from "../api";
import {
  alimentaAdempimenti,
  documentoDefinitivo,
  etichettaDownloadCedolino,
  etichettaStatoCedolino,
  statoEffettivo,
} from "../cedolinoStati";
import { preparaDatiGateRapporto } from "../rapportoFiscaleForm";

type Tab = "anagrafica" | "rapporto" | "storico";

const tabs: { key: Tab; label: string; Icona: typeof CircleUserRound }[] = [
  { key: "anagrafica", label: "Anagrafica", Icona: CircleUserRound },
  { key: "rapporto", label: "Rapporto di lavoro", Icona: BriefcaseBusiness },
  { key: "storico", label: "Storico cedolini", Icona: ReceiptText },
];

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic"];

function iniziali(nome: string, cognome: string): string {
  return `${(cognome[0] ?? "").toUpperCase()}${(nome[0] ?? "").toUpperCase()}` || "—";
}

export function Dipendente({ rapportoId, onPayslip, onBack }: { rapportoId: string | null; onPayslip: () => void; onBack: () => void }) {
  const [tab, setTab] = useState<Tab>("anagrafica");
  const [d, setD] = useState<RapportoDettaglio | null>(null);
  const [cedolini, setCedolini] = useState<CedolinoArchiviato[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [inModifica, setInModifica] = useState(false);
  const [scaricandoId, setScaricandoId] = useState<string | null>(null);

  useEffect(() => {
    if (!rapportoId) { setCaricamento(false); return; }
    setCaricamento(true);
    setInModifica(false);
    Promise.all([api.rapportoDettaglio(rapportoId), api.cedoliniArchiviati(rapportoId)])
      .then(([det, ced]) => { setD(det); setCedolini(ced); })
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"))
      .finally(() => setCaricamento(false));
  }, [rapportoId]);

  if (!rapportoId) {
    return (
      <section className="glass-card page-card">
        <div className="empty-state">Seleziona un dipendente dall'elenco “Dipendenti” per aprirne la scheda.</div>
      </section>
    );
  }
  if (caricamento) {
    return <section className="glass-card page-card"><div className="empty-state">Caricamento…</div></section>;
  }
  if (!d) {
    return <section className="glass-card page-card"><p className="errore">{errore ?? "Dipendente non trovato."}</p></section>;
  }

  const anagrafica: [string, string][] = [
    ["Codice fiscale", d.codiceFiscale || "—"],
    ["Nome", d.nome || "—"],
    ["Cognome", d.cognome || "—"],
    ["IBAN", d.iban || "— (non inserito)"],
  ];
  const rapporto: [string, string][] = [
    ["CCNL", d.codiceCcnl === "COMMERCIO_TERZIARIO" ? "Commercio — Terziario" : d.codiceCcnl],
    ["Livello", d.livello],
    ["Qualifica", d.qualifica],
    ["Mensilità", String(d.numeroMensilita)],
    ["Part-time", d.partTime ? `Sì · ${d.percentualePartTime}%` : "No · 100%"],
    ["Scatti anzianità", String(d.scattiAnzianita)],
    ["Superminimo", `${euro(d.superminimo)} €`],
    ["Ferie riportate al 1° gennaio", `${d.ferieResidueIniziali.toLocaleString("it-IT")} giorni`],
    ["ROL riportati al 1° gennaio", d.rolResiduiIniziali == null ? "—" : `${d.rolResiduiIniziali.toLocaleString("it-IT")} ore`],
    ["Ex festività riportate al 1° gennaio", d.exFestivitaResidueIniziali == null ? "—" : `${d.exFestivitaResidueIniziali.toLocaleString("it-IT")} ore`],
    ["Regione domicilio fiscale", d.regione || "—"],
    ["Codice catastale comune fiscale", d.codiceCatastaleComune || "—"],
    ["Decorrenza domicilio fiscale", d.dataDecorrenzaDomicilioFiscale || "—"],
    ["Fonte progressivi fiscali", d.riferimentoProgressiviFiscaliPregressi || "—"],
    ["Conguaglio addizionale regionale", d.addizionaleRegionaleConguaglioAnnuo == null ? "—" : `${euro(d.addizionaleRegionaleConguaglioAnnuo)} €`],
    ["Saldo addizionale comunale", d.addizionaleComunaleSaldoAnnuo == null ? "—" : `${euro(d.addizionaleComunaleSaldoAnnuo)} €`],
    ["Acconto addizionale comunale", d.addizionaleComunaleAccontoAnnuo == null ? "—" : `${euro(d.addizionaleComunaleAccontoAnnuo)} €`],
    ["Data assunzione", d.dataAssunzione],
    ["Data cessazione", d.dataCessazione ?? "In forza"],
  ];
  // Lo storico espone i chiusi e gli archivi pre-workflow, distinguendoli. Una bozza resta
  // nell'area di lavorazione e lo storico precedente non deve sembrare validato a posteriori.
  const cedoliniStorico = cedolini.filter((c) => alimentaAdempimenti(statoEffettivo(c)));
  const cessato = Boolean(d.dataCessazione);

  async function scaricaCedolino(id: string) {
    setErrore(null); setScaricandoId(id);
    try {
      const { blob, filename } = await api.cedolinoPdf(id);
      await scaricaBlob(blob, filename);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Non riesco a scaricare il cedolino.");
    } finally { setScaricandoId(null); }
  }

  return (
    <div className="detail-layout employee-detail-layout">
      <header className="glass-card emp-header">
        <button className="emp-back" type="button" onClick={onBack} title="Torna ai dipendenti"><ArrowLeft size={18} /></button>
        <div className="emp-avatar">{iniziali(d.nome, d.cognome)}</div>
        <div className="emp-id">
          <div className="emp-id-top">
            <h1>{d.cognome} {d.nome}</h1>
            <span className={`status-pill ${d.anonimizzato || cessato ? "slate" : "green"}`}>
              {!d.anonimizzato && !cessato && <Check size={12} aria-hidden="true" />}
              {d.anonimizzato ? "Anonimizzato" : cessato ? "Rapporto cessato" : "In forza"}
            </span>
          </div>
          <div className="emp-id-meta">
            <span>{d.qualifica}</span><span>Livello {d.livello}</span><span>{d.codiceCcnl === "COMMERCIO_TERZIARIO" ? "CCNL Commercio" : d.codiceCcnl}</span><span className="num">CF {d.codiceFiscale || "non indicato"}</span>
          </div>
        </div>
        <div className="emp-header-actions">
          {!d.anonimizzato && !inModifica && (
            <button className="btn secondary" type="button" onClick={() => setInModifica(true)}><Pencil size={15} /> Correggi</button>
          )}
          <button className="btn primary" type="button" onClick={onPayslip}><ReceiptText size={15} /> Cedolini</button>
        </div>
      </header>

      {inModifica && (
        <CorreggiScheda
          d={d}
          onFatto={(aggiornato) => { setInModifica(false); if (aggiornato) setD(aggiornato); }}
        />
      )}

      <section className="glass-card emp-card">
        <div className="emp-tabs">
          {tabs.map(({ key, label, Icona }) => (
            <button key={key} type="button" className={`emp-tab ${tab === key ? "active" : ""}`} onClick={() => setTab(key)}>
              <Icona size={15} aria-hidden="true" /> {label}
            </button>
          ))}
        </div>

        <div className="emp-body">
          {tab === "anagrafica" && (
            <div><div className="emp-section-heading"><CircleUserRound size={18} /><div><h2>Dati personali</h2><p>Identificazione e coordinate di pagamento del lavoratore</p></div></div><div className="emp-grid">
              {anagrafica.map(([label, value]) => (
                <div key={label}>
                  <div className="emp-field-label">{label}</div>
                  <div className={`emp-field-value ${/\d/.test(value) ? "num" : ""}`}>{value}</div>
                </div>
              ))}
            </div></div>
          )}

          {tab === "rapporto" && (
            <div><div className="emp-section-heading"><BriefcaseBusiness size={18} /><div><h2>Rapporto di lavoro</h2><p>Inquadramento contrattuale e condizioni correnti</p></div></div><div className="emp-grid">
              {rapporto.map(([label, value]) => (
                <div key={label}>
                  <div className="emp-field-label">{label}</div>
                  <div className={`emp-field-value ${/\d/.test(value) ? "num" : ""}`}>{value}</div>
                </div>
              ))}
            </div></div>
          )}

          {tab === "storico" && (
            cedoliniStorico.length === 0 ? (
              <div className="employee-empty"><FileText size={28} /><strong>Nessun cedolino nello storico</strong><p>Qui l’ufficio vede i documenti chiusi dal nuovo workflow e gli eventuali archivi precedenti, sempre indicati separatamente.</p><button className="btn primary" type="button" onClick={onPayslip}>Apri lavorazione Cedolini</button></div>
            ) : (
              <div>
                <p style={{ fontSize: 12, opacity: 0.72, margin: "0 0 10px" }}>
                  Archivio interno dell’ufficio. “Chiuso” significa immutabile; “pre-workflow”
                  identifica una ricostruzione storica non definitiva, né validata retroattivamente. Consegna e presa
                  visione del lavoratore non sono automatiche.
                </p>
                <div className="table-wrap">
                <table className="data-table spacious">
                  <thead>
                    <tr>
                      <th>Periodo</th>
                      <th className="right">Imponibile IRPEF</th>
                      <th className="right">Ritenute IRPEF</th>
                      <th className="right">Netto</th>
                      <th>Stato</th>
                      <th className="right">Azioni</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...cedoliniStorico].sort((a, b) => b.anno - a.anno || b.mese - a.mese).map((c) => {
                      const stato = statoEffettivo(c);
                      return (
                        <tr key={c.id}>
                          <td><strong>{mesi[c.mese]} {c.anno}</strong></td>
                          <td className="right num">{c.imponibileIrpef != null ? `${euro(c.imponibileIrpef)} €` : "—"}</td>
                          <td className="right num">{c.irpefNetta != null ? `${euro(c.irpefNetta)} €` : "—"}</td>
                          <td className="right num"><strong>{euro(c.nettoInBusta)} €</strong></td>
                          <td>
                            <span className={`status-pill ${documentoDefinitivo(stato) ? "green" : "slate"}`}>
                              {etichettaStatoCedolino[stato]}
                            </span>
                          </td>
                          <td className="right">
                            <button className="mini-action" type="button" disabled={scaricandoId === c.id}
                              onClick={() => void scaricaCedolino(c.id)}>
                              {scaricandoId === c.id ? "Download…" : etichettaDownloadCedolino(stato)}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                </div>
              </div>
            )
          )}
        </div>
      </section>
    </div>
  );
}

/** Data in formato "gg/MM/aaaa" (dal dettaglio) → "aaaa-MM-gg" per l'input date. */
function dataIso(it: string): string {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(it);
  return m ? `${m[3]}-${m[2]}-${m[1]}` : "";
}

/**
 * Form di correzione della scheda: utile quando l'import automatico (UNILAV, OCR della visura)
 * ha letto male un dato. Precompilato con i valori attuali; salva solo ciò che è cambiato.
 */
function CorreggiScheda({ d, onFatto }: { d: RapportoDettaglio; onFatto: (aggiornato: RapportoDettaglio | null) => void }) {
  const [f, setF] = useState({
    nome: d.nome, cognome: d.cognome, codiceFiscale: d.codiceFiscale, iban: d.iban ?? "",
    sesso: d.sesso ?? "",
    livello: d.livello, qualifica: d.qualifica,
    numeroMensilita: String(d.numeroMensilita),
    percentualePartTime: String(d.partTime ? d.percentualePartTime : 100),
    scattiAnzianita: String(d.scattiAnzianita), superminimo: String(d.superminimo),
    ferieResidueIniziali: String(d.ferieResidueIniziali),
    rolResiduiIniziali: d.rolResiduiIniziali != null ? String(d.rolResiduiIniziali) : "",
    exFestivitaResidueIniziali:
      d.exFestivitaResidueIniziali != null ? String(d.exFestivitaResidueIniziali) : "",
    regione: d.regione ?? "",
    codiceCatastaleComune: d.codiceCatastaleComune ?? "",
    dataDecorrenzaDomicilioFiscale: d.dataDecorrenzaDomicilioFiscale ?? "",
    riferimentoProgressiviFiscaliPregressi: d.riferimentoProgressiviFiscaliPregressi ?? "",
    dataAssunzione: dataIso(d.dataAssunzione),
    dataCessazione: d.dataCessazione ? dataIso(d.dataCessazione) : "",
    provinciaLavoro: d.provinciaLavoro ?? "",
    addizionaleRegionaleConguaglioAnnuo:
      d.addizionaleRegionaleConguaglioAnnuo != null ? String(d.addizionaleRegionaleConguaglioAnnuo) : "",
    addizionaleComunaleSaldoAnnuo:
      d.addizionaleComunaleSaldoAnnuo != null ? String(d.addizionaleComunaleSaldoAnnuo) : "",
    addizionaleComunaleAccontoAnnuo:
      d.addizionaleComunaleAccontoAnnuo != null ? String(d.addizionaleComunaleAccontoAnnuo) : "",
    tipoPartTimeInps: d.tipoPartTimeInps ?? "",
    cittadinanzaInps: d.cittadinanzaInps ?? "",
    qualificaProfessionaleIstat: d.qualificaProfessionaleIstat ?? "",
    tipoContribuzioneInps: d.tipoContribuzioneInps ?? "",
  });
  const [errore, setErrore] = useState<string | null>(null);
  const [attesa, setAttesa] = useState(false);

  const set = (k: keyof typeof f) => (e: { target: { value: string } }) =>
    setF({ ...f, [k]: e.target.value });

  async function salva(e: FormEvent) {
    e.preventDefault();
    setErrore(null);
    const datiGate = preparaDatiGateRapporto(f);
    if (!datiGate.ok) {
      setErrore(`Completa i dati richiesti prima di salvare. ${datiGate.errori.join(" ")}`);
      return;
    }
    setAttesa(true);
    try {
      const pct = Number(f.percentualePartTime) || 100;
      await api.correggiRapporto(d.rapportoId, {
        nome: f.nome.trim(), cognome: f.cognome.trim(), codiceFiscale: f.codiceFiscale.trim(),
        iban: f.iban.trim(),
        // Vuoto = non indicato; il server verifica la coerenza col codice fiscale.
        sesso: f.sesso,
        livello: f.livello.trim(), qualifica: f.qualifica,
        numeroMensilita: Number(f.numeroMensilita) || d.numeroMensilita,
        partTime: pct < 100, percentualePartTime: pct,
        scattiAnzianita: Number(f.scattiAnzianita) || 0,
        superminimo: Number(f.superminimo) || 0,
        ferieResidueIniziali: Number(f.ferieResidueIniziali) || 0,
        ...datiGate.valore,
        dataAssunzione: f.dataAssunzione || undefined,
        dataCessazione: f.dataCessazione || undefined,
        rimuoviDataCessazione: f.dataCessazione ? undefined : true,
        provinciaLavoro: f.provinciaLavoro.trim(),
        // Stringa vuota = «togli il dato»: il server distingue fra assente e cancellato.
        tipoPartTimeInps: pct < 100 ? f.tipoPartTimeInps.trim() : "",
        cittadinanzaInps: f.cittadinanzaInps.trim(),
        qualificaProfessionaleIstat: f.qualificaProfessionaleIstat.trim(),
        tipoContribuzioneInps: f.tipoContribuzioneInps.trim(),
      });
      const aggiornato = await api.rapportoDettaglio(d.rapportoId);
      onFatto(aggiornato);
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Salvataggio non riuscito. Riprova.");
      setAttesa(false);
    }
  }

  return (
    <form className="glass-card form-card employee-correction" onSubmit={salva}>
      <div className="card-title-row employee-correction-head">
        <div>
          <h2><Pencil size={17} /> Correggi scheda</h2>
          <p>Aggiorna anagrafica e rapporto di lavoro.</p>
        </div>
        <button className="icon-btn" type="button" title="Chiudi modifica" onClick={() => onFatto(null)}><X size={17} /></button>
      </div>
      <div className="employee-correction-section"><h3><CircleUserRound size={15} /> Anagrafica</h3>
      <div className="form-grid">
        <label><span>Nome</span><input value={f.nome} onChange={set("nome")} /></label>
        <label><span>Cognome</span><input value={f.cognome} onChange={set("cognome")} /></label>
        <label><span>Codice fiscale</span><input value={f.codiceFiscale} onChange={set("codiceFiscale")} maxLength={16} /></label>
        <label><span>Sesso <small>(verificato col codice fiscale)</small></span>
          <select value={f.sesso} onChange={set("sesso")}>
            <option value="">Non indicato</option>
            <option value="M">M</option>
            <option value="F">F</option>
          </select></label>
        <label><span>IBAN <small>(vuoto = rimuovi)</small></span><input value={f.iban} onChange={set("iban")} /></label>
      </div></div>
      <div className="employee-correction-section"><h3><BriefcaseBusiness size={15} /> Rapporto di lavoro</h3><div className="form-grid">
        <label>
          <span>Qualifica</span>
          <select value={f.qualifica} onChange={set("qualifica")}>
            <option>Impiegato</option>
            <option>Operaio</option>
            <option>Quadro</option>
            <option>Dirigente</option>
            <option>Apprendista</option>
          </select>
        </label>
        <label><span>Livello</span><input value={f.livello} onChange={set("livello")} /></label>
        <label><span>Mensilità</span><input type="number" min={12} max={15} value={f.numeroMensilita} onChange={set("numeroMensilita")} /></label>
        <label><span>Percentuale part-time <small>(100 = tempo pieno)</small></span>
          <input type="number" min={1} max={100} value={f.percentualePartTime} onChange={set("percentualePartTime")} /></label>
        <label><span>Scatti anzianità</span><input type="number" min={0} value={f.scattiAnzianita} onChange={set("scattiAnzianita")} /></label>
        <label><span>Superminimo €</span><input type="number" min={0} step="0.01" value={f.superminimo} onChange={set("superminimo")} /></label>
        <label><span>Ferie riportate al 1° gennaio <small>(giorni)</small></span>
          <input type="number" step="0.01" value={f.ferieResidueIniziali} onChange={set("ferieResidueIniziali")} /></label>
        <label><span>ROL riportati al 1° gennaio <small>(ore; indica anche 0)</small></span>
          <input type="number" step="0.01" value={f.rolResiduiIniziali}
            onChange={set("rolResiduiIniziali")} required /></label>
        <label><span>Ex festività riportate al 1° gennaio <small>(ore; indica anche 0)</small></span>
          <input type="number" step="0.01" value={f.exFestivitaResidueIniziali}
            onChange={set("exFestivitaResidueIniziali")} required /></label>
        <label><span>Regione del domicilio fiscale</span>
          <input value={f.regione} onChange={set("regione")} required /></label>
        <label><span>Codice catastale del comune fiscale</span>
          <input maxLength={4} placeholder="es. F205" value={f.codiceCatastaleComune}
            onChange={set("codiceCatastaleComune")} required /></label>
        <label><span>Decorrenza domicilio fiscale</span>
          <input type="date" value={f.dataDecorrenzaDomicilioFiscale}
            onChange={set("dataDecorrenzaDomicilioFiscale")} required /></label>
        <label><span>Fonte progressivi fiscali <small>(o assenza pregressi)</small></span>
          <input placeholder="CU provvisoria / import certificato / assunzione nel mese"
            value={f.riferimentoProgressiviFiscaliPregressi}
            onChange={set("riferimentoProgressiviFiscaliPregressi")} required /></label>
        <label><span>Data assunzione</span><input type="date" value={f.dataAssunzione} onChange={set("dataAssunzione")} /></label>
        <label><span>Data cessazione <small>(vuoto = ancora in forza)</small></span>
          <input type="date" value={f.dataCessazione} onChange={set("dataCessazione")} /></label>
        <label><span>Provincia unità operativa <small>(sigla; vuoto = sede azienda)</small></span>
          <input maxLength={2} placeholder="es. MI" value={f.provinciaLavoro}
            onChange={set("provinciaLavoro")} /></label>
      </div>
      </div>

      <div className="employee-correction-section">
        <h3><BriefcaseBusiness size={15} /> Addizionali dal conguaglio</h3>
        <p className="hint">
          Compila tutti gli importi annui risultanti dal conguaglio dell'anno precedente.
          Digita 0 quando il conguaglio certifica zero: un campo vuoto blocca il cedolino
          archiviabile e non viene stimato sul solo mese.
        </p>
        <div className="form-grid">
          <label><span>Addizionale regionale anno precedente €</span>
            <input type="number" min={0} step="0.01" value={f.addizionaleRegionaleConguaglioAnnuo}
              onChange={set("addizionaleRegionaleConguaglioAnnuo")} required /></label>
          <label><span>Addizionale comunale, saldo €</span>
            <input type="number" min={0} step="0.01" value={f.addizionaleComunaleSaldoAnnuo}
              onChange={set("addizionaleComunaleSaldoAnnuo")} required /></label>
          <label><span>Addizionale comunale, acconto €</span>
            <input type="number" min={0} step="0.01" value={f.addizionaleComunaleAccontoAnnuo}
              onChange={set("addizionaleComunaleAccontoAnnuo")} required /></label>
        </div>
      </div>

      <div className="employee-correction-section">
        <h3><BriefcaseBusiness size={15} /> Dati per la denuncia INPS (UniEmens)</h3>
        <p className="hint">
          Servono alla denuncia mensile all'INPS e non si ricavano dalla busta paga.
          Senza la cittadinanza il flusso viene scartato; gli altri riguardano casi particolari.
        </p>
        <div className="form-grid">
        <label><span>Cittadinanza <small>(codice a 3 cifre, come sull'UNILAV)</small></span>
          <input inputMode="numeric" maxLength={3} placeholder="es. 330"
            value={f.cittadinanzaInps} onChange={set("cittadinanzaInps")} /></label>
        <label><span>Tipo di part-time <small>(solo se part-time)</small></span>
          <select value={f.tipoPartTimeInps} onChange={set("tipoPartTimeInps")}
            disabled={Number(f.percentualePartTime) >= 100}>
            <option value="">Da indicare</option>
            <option value="P">Orizzontale — meno ore tutti i giorni</option>
            <option value="V">Verticale — giornate intere, ma non tutte</option>
            <option value="M">Misto — giorni ridotti e giornate non lavorate</option>
          </select></label>
        <label><span>Qualifica professionale ISTAT <small>(facoltativa)</small></span>
          <input placeholder="es. 8.1.3.3.0.2"
            value={f.qualificaProfessionaleIstat} onChange={set("qualificaProfessionaleIstat")} /></label>
        <label><span>Tipo contribuzione <small>(solo casi particolari: apprendisti, intermittenti)</small></span>
          <input maxLength={2} placeholder="es. G0"
            value={f.tipoContribuzioneInps} onChange={set("tipoContribuzioneInps")} /></label>
        </div>
      {errore && <span className="errore">{errore}</span>}
      </div>
      <div className="actions-row employee-correction-actions">
        <button className="btn primary" type="submit" disabled={attesa}>
          <Save size={15} /> {attesa ? "Salvataggio…" : "Salva correzioni"}
        </button>
        <button className="btn secondary" type="button" disabled={attesa} onClick={() => onFatto(null)}>
          <X size={15} /> Annulla
        </button>
      </div>
    </form>
  );
}
