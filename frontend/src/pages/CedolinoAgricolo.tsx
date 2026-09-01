import { type FormEvent, useEffect, useMemo, useState } from "react";
import { Calculator, Download, Info, ShieldAlert } from "lucide-react";
import {
  api,
  scaricaBlob,
  type AnteprimaCedolinoAgricolo,
  type Azienda,
  type DatiMensiliCedolinoAgricolo,
  type DipendenteRapporto,
  type ProvinciaAgricola,
  type RapportoDettaglio,
  type TabellaAgricola,
} from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";
import {
  eRapportoAgricolo,
  eTipoContrattoOti,
  validaDatiCedolinoAgricolo,
  giorniDetrazioneNelMese,
  oreDaGiornate,
  oreGiornaliereDaContratto,
  GIORNI_SETTIMANA_AGRICOLA,
} from "../agricoltura";

const mesi = [
  "", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
  "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre",
];
const euro = (valore: number) => valore.toLocaleString("it-IT", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const numero = (valore: number | null | undefined, decimali = 2) => valore == null
  ? "-"
  : valore.toLocaleString("it-IT", {
    minimumFractionDigits: decimali,
    maximumFractionDigits: decimali,
  });

interface CampiForm {
  anno: string;
  mese: string;
  oreOrdinarie: string;
  giornateLavorate: string;
  giorniDetrazione: string;
  unaTantum: string;
  redditoPresuntoAnnuo: string;
  detrazioniGiaGoduteAnno: string;
  arrotondamentoRiportato: string;
  ultimoCedolino: boolean;
  giorniTrasferta: string;
  indennitaTrasfertaGiornaliera: string;
  rimborsiSpeseDocumentate: string;
  oreGiorno: string;
}

const annoCorrente = new Date().getFullYear();
const formIniziale: CampiForm = {
  anno: String(annoCorrente),
  mese: String(new Date().getMonth() + 1),
  oreOrdinarie: "",
  giornateLavorate: "",
  giorniDetrazione: "",
  unaTantum: "0",
  redditoPresuntoAnnuo: "",
  detrazioniGiaGoduteAnno: "0",
  arrotondamentoRiportato: "0",
  ultimoCedolino: false,
  giorniTrasferta: "0",
  indennitaTrasfertaGiornaliera: "0",
  rimborsiSpeseDocumentate: "0",
  oreGiorno: "",
};

function valoreNumero(valore: string): number {
  if (!valore.trim()) return Number.NaN;
  return Number(valore.replace(",", "."));
}

export function CedolinoAgricolo() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [rapporto, setRapporto] = useState<RapportoDettaglio | null>(null);
  const [province, setProvince] = useState<ProvinciaAgricola[]>([]);
  const [tabella, setTabella] = useState<TabellaAgricola | null>(null);
  const [form, setForm] = useState<CampiForm>(formIniziale);
  const [anteprima, setAnteprima] = useState<AnteprimaCedolinoAgricolo | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [calcolando, setCalcolando] = useState(false);
  const [scaricando, setScaricando] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    let attivo = true;
    (async () => {
      try {
        const [aziende, tabelleDisponibili] = await Promise.all([
          api.aziende(),
          api.provinceAgricole(),
        ]);
        if (!attivo) return;
        const scelta = scegliAziendaPreferita(aziende);
        setAzienda(scelta);
        setProvince(tabelleDisponibili);
        if (!scelta) return;
        const rapporti = (await api.dipendenti(scelta.id)).filter((d) => eRapportoAgricolo(d.codiceCcnl));
        if (!attivo) return;
        setDipendenti(rapporti);
        if (rapporti.length > 0) setRapportoId(rapporti[0].rapportoId);
        const tabellaPreferita = tabelleDisponibili.find((p) =>
          p.calcolabile && p.provincia.toUpperCase() === scelta.provincia?.toUpperCase());
        if (tabellaPreferita)
          setForm((corrente) => ({ ...corrente, anno: String(tabellaPreferita.anno) }));
      } catch (e) {
        if (attivo) setErrore(e instanceof Error ? e.message : "Non riesco a caricare i dati agricoli.");
      } finally {
        if (attivo) setCaricamento(false);
      }
    })();
    return () => { attivo = false; };
  }, []);

  useEffect(() => {
    let attivo = true;
    setRapporto(null);
    setAnteprima(null);
    if (!rapportoId) return () => { attivo = false; };
    api.rapportoDettaglio(rapportoId)
      .then((dettaglio) => { if (attivo) setRapporto(dettaglio); })
      .catch((e) => { if (attivo) setErrore(e instanceof Error ? e.message : "Rapporto non disponibile."); });
    return () => { attivo = false; };
  }, [rapportoId]);

  // Dal contratto: ore al giorno (ore settimanali ÷ 6) e giorni di detrazione del mese.
  // Sono proposte modificabili: il datore può variare le ore, il motore usa quello che vede.
  useEffect(() => {
    if (!rapporto) return;
    const oreGiorno = oreGiornaliereDaContratto(rapporto.oreSettimanali);
    const detrazione = giorniDetrazioneNelMese(
      Number(form.anno), Number(form.mese), rapporto.dataAssunzione, rapporto.dataCessazione);
    setForm((corrente) => ({
      ...corrente,
      oreGiorno: oreGiorno === null ? "" : String(oreGiorno),
      giorniDetrazione: detrazione === null ? corrente.giorniDetrazione : String(detrazione),
    }));
  }, [rapporto, form.anno, form.mese]);

  const provinciaRapporto = (rapporto?.provinciaLavoro || azienda?.provincia || "").toUpperCase();
  const anno = Number(form.anno);
  const disponibilita = province.find((p) => p.anno === anno && p.provincia.toUpperCase() === provinciaRapporto);
  const otiNonSupportato = rapporto ? eTipoContrattoOti(rapporto.tipoContratto) : false;

  useEffect(() => {
    let attivo = true;
    setTabella(null);
    setAnteprima(null);
    if (!provinciaRapporto || !Number.isInteger(anno)) return () => { attivo = false; };
    api.tabellaAgricola(anno, provinciaRapporto)
      .then((dati) => { if (attivo) setTabella(dati); })
      .catch(() => { if (attivo) setTabella(null); });
    return () => { attivo = false; };
  }, [anno, provinciaRapporto]);

  const dati = useMemo<DatiMensiliCedolinoAgricolo>(() => ({
    anno: Number(form.anno),
    mese: Number(form.mese),
    oreOrdinarie: valoreNumero(form.oreOrdinarie),
    giornateLavorate: valoreNumero(form.giornateLavorate),
    giorniDetrazione: valoreNumero(form.giorniDetrazione),
    unaTantum: valoreNumero(form.unaTantum),
    detrazioniGiaGoduteAnno: valoreNumero(form.detrazioniGiaGoduteAnno),
    arrotondamentoRiportato: valoreNumero(form.arrotondamentoRiportato),
    ultimoCedolino: form.ultimoCedolino,
    redditoPresuntoAnnuo: form.redditoPresuntoAnnuo.trim()
      ? valoreNumero(form.redditoPresuntoAnnuo)
      : null,
    giorniTrasferta: valoreNumero(form.giorniTrasferta),
    indennitaTrasfertaGiornaliera: valoreNumero(form.indennitaTrasfertaGiornaliera),
    rimborsiSpeseDocumentate: valoreNumero(form.rimborsiSpeseDocumentate),
  }), [form]);

  // Tendina delle ore al giorno: mezz'ora per mezz'ora da 1 a 10, con il valore del contratto
  // (ore settimanali ÷ 6) sempre presente anche quando non cade su una mezz'ora.
  const oreGiornoContratto = (() => {
    const valore = oreGiornaliereDaContratto(rapporto?.oreSettimanali);
    return valore === null ? "" : String(valore);
  })();
  const opzioniOreGiorno = useMemo(() => {
    const base = Array.from({ length: 19 }, (_, i) => String(1 + i * 0.5));
    const extra = [oreGiornoContratto, form.oreGiorno].filter((v) => v && !base.includes(v));
    return [...base, ...extra].sort((a, b) => Number(a) - Number(b));
  }, [oreGiornoContratto, form.oreGiorno]);

  const tariffa = tabella?.tariffe.find((t) =>
    t.livello.toUpperCase() === rapporto?.livello.toUpperCase());

  function aggiorna(campo: keyof CampiForm, valore: string | boolean) {
    setForm((corrente) => {
      const prossimo = { ...corrente, [campo]: valore };
      // Giornate × ore al giorno → ore del mese proposte (si possono sempre correggere a mano);
      // i giorni di trasferta seguono le giornate finché non vengono cambiati esplicitamente.
      if (campo === "giornateLavorate" || campo === "oreGiorno") {
        const ore = oreDaGiornate(valoreNumero(prossimo.giornateLavorate), valoreNumero(prossimo.oreGiorno));
        if (ore !== null) prossimo.oreOrdinarie = String(ore);
        if (campo === "giornateLavorate") prossimo.giorniTrasferta = String(valore);
      }
      return prossimo;
    });
    setAnteprima(null);
    setErrore(null);
  }

  function controllaDati(): boolean {
    const errori = validaDatiCedolinoAgricolo(dati);
    if (otiNonSupportato)
      errori.unshift("Il calcolo OTI e bloccato: le tabelle validate disponibili coprono solo gli OTD.");
    if (!disponibilita)
      errori.unshift(`Non esiste una tabella CPL ${provinciaRapporto || "della provincia"} per il ${form.anno}.`);
    else if (!disponibilita.calcolabile)
      errori.unshift(disponibilita.motivo ?? "Mancano i parametri nazionali per questo anno.");
    if (!tariffa && tabella)
      errori.unshift(`Il livello ${rapporto?.livello || "del rapporto"} non e presente nella tabella caricata.`);
    if (errori.length > 0) {
      setErrore(errori.join(" "));
      return false;
    }
    return true;
  }

  async function calcola(evento: FormEvent) {
    evento.preventDefault();
    if (!rapportoId || !controllaDati()) return;
    setCalcolando(true);
    setErrore(null);
    try {
      setAnteprima(await api.anteprimaCedolinoAgricolo(rapportoId, dati));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Impossibile calcolare l'anteprima.");
    } finally {
      setCalcolando(false);
    }
  }

  async function scaricaPdf() {
    if (!rapportoId || !controllaDati()) return;
    setScaricando(true);
    setErrore(null);
    try {
      const file = await api.cedolinoAgricoloPdf(rapportoId, dati);
      await scaricaBlob(file.blob, file.filename);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Impossibile scaricare il PDF agricolo.");
    } finally {
      setScaricando(false);
    }
  }

  if (caricamento)
    return <div className="glass-card page-card"><div className="empty-state">Caricamento rapporti agricoli...</div></div>;

  return (
    <div className="module-layout wide">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Anteprima busta agricola</h1>
            <p>Operai OTD · tariffa provinciale, ore e giornate INPS separate · OTI in preparazione</p>
          </div>
          <span className="status-pill orange">Bozza non archiviata</span>
        </div>

        <div className="empty-state" style={{ textAlign: "left", marginBottom: 16 }}>
          <ShieldAlert size={18} aria-hidden="true" />
          Questa funzione prepara un prospetto da controllare. Non chiude il cedolino e non genera
          LUL, UniEmens/PosAgri o F24.
        </div>

        {!azienda ? (
          <div className="empty-state">Scegli prima l'azienda cliente dalla barra in alto.</div>
        ) : dipendenti.length === 0 ? (
          <div className="empty-state">
            Nessun rapporto con CCNL AGRICOLTURA_OPERAI per {azienda.ragioneSociale}.
            Completa prima l'inquadramento nella scheda Dipendenti.
          </div>
        ) : (
          <form onSubmit={calcola}>
            <div className="form-grid">
              <label className="wide-field">
                <span>Lavoratore agricolo</span>
                <select value={rapportoId} onChange={(e) => setRapportoId(e.target.value)}>
                  {dipendenti.map((d) => (
                    <option value={d.rapportoId} key={d.rapportoId}>
                      {d.nominativo} · {d.livello}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>Anno</span>
                <input type="number" min="2000" max="2100" value={form.anno}
                  onChange={(e) => aggiorna("anno", e.target.value)} />
              </label>
              <label>
                <span>Mese</span>
                <select value={form.mese} onChange={(e) => aggiorna("mese", e.target.value)}>
                  {mesi.slice(1).map((mese, indice) => <option value={indice + 1} key={mese}>{mese}</option>)}
                </select>
              </label>
              <label>
                <span>Giornate lavorate INPS</span>
                <input type="number" min="0" step="1" value={form.giornateLavorate}
                  onChange={(e) => aggiorna("giornateLavorate", e.target.value)} placeholder="es. 8" />
              </label>
              <label>
                <span>
                  Ore al giorno dal contratto
                  {rapporto?.oreSettimanali
                    ? ` (${rapporto.oreSettimanali} h/sett. ÷ ${GIORNI_SETTIMANA_AGRICOLA} gg, modificabile)`
                    : " (modificabile)"}
                </span>
                <select value={form.oreGiorno} onChange={(e) => aggiorna("oreGiorno", e.target.value)}>
                  {!form.oreGiorno && <option value="">Scegli le ore al giorno</option>}
                  {opzioniOreGiorno.map((valore) => (
                    <option value={valore} key={valore}>
                      {Number(valore).toLocaleString("it-IT")} ore{valore === oreGiornoContratto ? " (contratto)" : ""}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>Ore ordinarie effettive (giornate × ore/giorno, correggibili)</span>
                <input type="number" min="0" step="0.01" value={form.oreOrdinarie}
                  onChange={(e) => aggiorna("oreOrdinarie", e.target.value)} placeholder="es. 64" />
              </label>
              <label>
                <span>Giorni di detrazione</span>
                <input type="number" min="0" step="1" value={form.giorniDetrazione}
                  onChange={(e) => aggiorna("giorniDetrazione", e.target.value)} placeholder="es. 10" />
              </label>
              <label>
                <span>Altre competenze imponibili</span>
                <input type="number" min="0" step="0.01" value={form.unaTantum}
                  onChange={(e) => aggiorna("unaTantum", e.target.value)} />
              </label>
              <label>
                <span>Giorni di trasferta (fuori dal comune della sede)</span>
                <input type="number" min="0" step="1" value={form.giorniTrasferta}
                  onChange={(e) => aggiorna("giorniTrasferta", e.target.value)} />
              </label>
              <label>
                <span>Indennità di trasferta al giorno (esente entro 46,48 €)</span>
                <input type="number" min="0" step="0.01" value={form.indennitaTrasfertaGiornaliera}
                  onChange={(e) => aggiorna("indennitaTrasfertaGiornaliera", e.target.value)} />
              </label>
              <label>
                <span>Rimborsi spese documentati (esenti)</span>
                <input type="number" min="0" step="0.01" value={form.rimborsiSpeseDocumentate}
                  onChange={(e) => aggiorna("rimborsiSpeseDocumentate", e.target.value)} />
              </label>
              <label>
                <span>Reddito annuo presunto reale</span>
                <input type="number" min="0.01" step="0.01" value={form.redditoPresuntoAnnuo}
                  onChange={(e) => aggiorna("redditoPresuntoAnnuo", e.target.value)} placeholder="obbligatorio" />
              </label>
              <label>
                <span>Detrazioni gia godute nell'anno</span>
                <input type="number" min="0" step="0.01" value={form.detrazioniGiaGoduteAnno}
                  onChange={(e) => aggiorna("detrazioniGiaGoduteAnno", e.target.value)} />
              </label>
              <label>
                <span>Riporto arrotondamenti</span>
                <input type="number" step="0.01" value={form.arrotondamentoRiportato}
                  onChange={(e) => aggiorna("arrotondamentoRiportato", e.target.value)} />
              </label>
              <label className="wide-field" style={{ display: "flex", alignItems: "center", gap: 9 }}>
                <input type="checkbox" checked={form.ultimoCedolino}
                  onChange={(e) => aggiorna("ultimoCedolino", e.target.checked)} />
                <span>Ultimo cedolino del rapporto: chiudi il riporto arrotondamenti</span>
              </label>
            </div>

            <div className="empty-state" style={{ textAlign: "left", marginTop: 14 }}>
              <Info size={17} aria-hidden="true" />
              <strong>CPL {provinciaRapporto || "da completare"} {form.anno}</strong>
              {disponibilita?.calcolabile
                ? ` · tabella disponibile · livello ${rapporto?.livello ?? "-"}`
                : ` · ${disponibilita?.motivo ?? "tabella non disponibile"}`}
              {tariffa && ` · paga oraria ${numero(tariffa.pagaOraria, 5)} EUR`}
            </div>

            {otiNonSupportato && (
              <p className="errore" role="alert" style={{ marginTop: 14 }}>
                Rapporto OTI riconosciuto, ma non calcolabile: le sole tabelle validate sono OTD.
                Servono parametri OTI distinti e verifica professionale.
              </p>
            )}

            {errore && <p className="errore" role="alert" style={{ marginTop: 14 }}>{errore}</p>}

            <div className="actions-row" style={{ marginTop: 16 }}>
              <button className="btn primary" type="submit"
                disabled={otiNonSupportato || calcolando || scaricando}>
                <Calculator size={16} aria-hidden="true" />
                {calcolando ? "Calcolo..." : "Calcola anteprima"}
              </button>
              <button className="btn secondary" type="button"
                disabled={otiNonSupportato || calcolando || scaricando}
                onClick={() => void scaricaPdf()}>
                <Download size={16} aria-hidden="true" />
                {scaricando ? "Preparazione PDF..." : "Scarica PDF bozza"}
              </button>
            </div>
          </form>
        )}

        {anteprima && (
          <div style={{ marginTop: 22 }}>
            <div className="card-title-row">
              <div>
                <h2>{mesi[anteprima.cedolino.mese]} {anteprima.cedolino.anno}</h2>
                <p>{anteprima.tipoOperaio} · CPL {anteprima.provinciaCpl} · livello {anteprima.livelloTariffa}</p>
              </div>
              <span className="status-pill blue">Da validare</span>
            </div>
            <div className="kpi-grid">
              <div className="kpi-card"><span>Lordo</span><strong>{euro(anteprima.cedolino.totaleCompetenze)} EUR</strong></div>
              <div className="kpi-card"><span>Contributi lavoratore</span><strong>{euro(anteprima.cedolino.contributiDipendente)} EUR</strong></div>
              <div className="kpi-card"><span>IRPEF netta</span><strong>{euro(anteprima.cedolino.irpefNetta)} EUR</strong></div>
              <div className="kpi-card"><span>Netto in busta</span><strong>{euro(anteprima.cedolino.nettoInBusta)} EUR</strong></div>
            </div>
            <div className="table-wrap" style={{ marginTop: 16 }}>
              <table className="data-table spacious">
                <thead><tr><th>Codice</th><th>Voce</th><th className="right">Quantita</th><th className="right">Base</th><th className="right">Importo</th></tr></thead>
                <tbody>
                  {anteprima.cedolino.righe.map((riga, indice) => (
                    <tr key={`${riga.codice}-${indice}`}>
                      <td><strong>{riga.codice}</strong></td>
                      <td>{riga.descrizione}<small style={{ display: "block", opacity: .65 }}>{riga.tipo}</small></td>
                      <td className="right num">{numero(riga.quantita)}</td>
                      <td className="right num">{numero(riga.base, riga.codice === "AGR_ORD" ? 5 : 2)}</td>
                      <td className="right num">{riga.importo === 0 ? "-" : `${euro(riga.importo)} EUR`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p style={{ marginTop: 12, fontSize: 12, opacity: .72 }}>
              Fonte applicata: {anteprima.fonteTabelle}. Il costo datoriale completo non viene
              dichiarato: i contributi definitivi sono determinati dall'INPS dopo PosAgri.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
