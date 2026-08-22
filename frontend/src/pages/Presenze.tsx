import { Fragment, useCallback, useEffect, useRef, useState } from "react";
import { Calculator, CalendarDays, CheckCircle2, Download, Pencil, Users, X } from "lucide-react";
import {
  api, scaricaBlob,
  type Azienda, type Cartellino, type CausaleGiornata,
  type GiornataCartellino, type PresenzeMese, type ProblemaCartellino,
} from "../api";
import { salvaAziendaCorrenteId, scegliAziendaPreferita } from "../aziendaCorrente";
import { creaSequenziatoreRichieste } from "../ultimaRichiesta";

const oggi = new Date();
const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const mesi = ["", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];

type DatiCartellino = Omit<Cartellino, "anno" | "mese" | "giornate"> & {
  oreStraordinarioNotturno: number; oreStraordinarioFestivo: number;
  giorniFerie: number; orePermessi: number; oreExFestivita: number;
  giorniMalattia: number; numeroEventoMalattiaAnno: number; giorniInfortunio: number;
  oreAssenzaNonRetribuita: number;
  statoAssegniFamiliari: "DaVerificare" | "NonSpettanti" | "Spettanti";
  importoAssegniFamiliari: number;
};

const vuoto: DatiCartellino = {
  oreStraordinario: 0, oreStraordinarioNotturno: 0, oreStraordinarioFestivo: 0,
  giorniFerie: 0, orePermessi: 0, oreExFestivita: 0,
  giorniMalattia: 0, numeroEventoMalattiaAnno: 1, giorniInfortunio: 0,
  oreAssenzaNonRetribuita: 0,
  statoAssegniFamiliari: "DaVerificare", importoAssegniFamiliari: 0,
};

function dalRiepilogo(r: PresenzeMese["righe"][number]): DatiCartellino {
  return {
    oreStraordinario: r.oreStraordinario,
    oreStraordinarioNotturno: r.oreStraordinarioNotturno,
    oreStraordinarioFestivo: r.oreStraordinarioFestivo,
    giorniFerie: r.giorniFerie,
    orePermessi: r.orePermessi,
    oreExFestivita: r.oreExFestivita,
    giorniMalattia: r.giorniMalattia,
    numeroEventoMalattiaAnno: r.numeroEventoMalattiaAnno,
    giorniInfortunio: r.giorniInfortunio,
    oreAssenzaNonRetribuita: r.oreAssenzaNonRetribuita,
    statoAssegniFamiliari: r.statoAssegniFamiliari,
    importoAssegniFamiliari: r.importoAssegniFamiliari,
  };
}

// Diritto residuo del dipendente dall'ultimo cedolino elaborato: guida chi inserisce
// ferie e permessi senza bloccare (le ferie anticipate restano possibili e segnalate).
function DirittiFeriePermessi({ riga, cartellino }: {
  riga: PresenzeMese["righe"][number];
  cartellino: DatiCartellino;
}) {
  if (riga.ferieResidue == null || riga.permessiResidui == null
    || riga.saldiAnno == null || riga.saldiMese == null) {
    return (
      <p className="muto">
        Residui ferie/permessi non ancora disponibili: si calcolano col primo cedolino elaborato.
      </p>
    );
  }
  const permessiChiesti = cartellino.orePermessi + cartellino.oreExFestivita;
  const ferieOltre = cartellino.giorniFerie > riga.ferieResidue;
  const permessiOltre = permessiChiesti > riga.permessiResidui;
  return (
    <div aria-live="polite">
      <p className="muto">
        Diritto residuo al cedolino di {mesi[riga.saldiMese]} {riga.saldiAnno}:{" "}
        <strong>{riga.ferieResidue.toLocaleString("it-IT")} gg ferie</strong> ·{" "}
        <strong>{riga.permessiResidui.toLocaleString("it-IT")} h permessi ROL/ex festività</strong>
        {" "}— saldi da validare col Consulente del Lavoro.
      </p>
      {ferieOltre && (
        <p><span className="status-pill amber">
          Ferie oltre il residuo: {cartellino.giorniFerie} gg su {riga.ferieResidue.toLocaleString("it-IT")} disponibili (anticipate)
        </span></p>
      )}
      {permessiOltre && (
        <p><span className="status-pill amber">
          Permessi oltre il residuo: {permessiChiesti} h su {riga.permessiResidui.toLocaleString("it-IT")} disponibili
        </span></p>
      )}
    </div>
  );
}

// ---------- calendario giorno per giorno ----------

const CAUSALI: { valore: CausaleGiornata; etichetta: string }[] = [
  { valore: "Lavorata", etichetta: "Lavorata" },
  { valore: "Ferie", etichetta: "Ferie" },
  { valore: "Malattia", etichetta: "Malattia" },
  { valore: "Infortunio", etichetta: "Infortunio" },
  { valore: "PermessoRetribuito", etichetta: "Permesso ROL" },
  { valore: "ExFestivita", etichetta: "Ex festività" },
  { valore: "AssenzaNonRetribuita", etichetta: "Assenza non retribuita" },
  { valore: "RiposoSettimanale", etichetta: "Riposo" },
  { valore: "Festivita", etichetta: "Festività" },
];

/** Il campo ore che quella causale riempie; null = giornata intera, non si misura a ore. */
function campoOre(causale: CausaleGiornata): keyof GiornataCartellino | null {
  switch (causale) {
    case "Lavorata": return "oreOrdinarie";
    case "PermessoRetribuito": return "orePermessi";
    case "ExFestivita": return "oreExFestivita";
    case "AssenzaNonRetribuita": return "oreAssenzaNonRetribuita";
    default: return null;
  }
}

function giornataVuota(data: string, causale: CausaleGiornata, ore: number): GiornataCartellino {
  const g: GiornataCartellino = {
    data, causale,
    oreOrdinarie: 0, orePermessi: 0, oreExFestivita: 0, oreAssenzaNonRetribuita: 0,
    oreStraordinario: 0, oreStraordinarioNotturno: 0, oreStraordinarioFestivo: 0,
  };
  const campo = campoOre(causale);
  return campo ? { ...g, [campo]: ore } : g;
}

/** Mese precompilato: feriali lavorati alle ore contrattuali, domeniche a riposo. */
function calendarioIniziale(anno: number, mese: number, oreGiorno: number): GiornataCartellino[] {
  const giorni = new Date(anno, mese, 0).getDate();
  return Array.from({ length: giorni }, (_, i) => {
    const data = new Date(anno, mese - 1, i + 1);
    const iso = `${anno}-${String(mese).padStart(2, "0")}-${String(i + 1).padStart(2, "0")}`;
    return data.getDay() === 0
      ? giornataVuota(iso, "RiposoSettimanale", 0)
      : giornataVuota(iso, "Lavorata", oreGiorno);
  });
}

const GIORNI_SETTIMANA = ["dom", "lun", "mar", "mer", "gio", "ven", "sab"];

function descrizione(d: DatiCartellino) {
  const parti = [
    d.giorniFerie > 0 && `${d.giorniFerie} gg ferie`,
    d.giorniMalattia > 0 && `${d.giorniMalattia} gg malattia`,
    d.giorniInfortunio > 0 && `${d.giorniInfortunio} gg infortunio`,
    d.orePermessi > 0 && `${d.orePermessi} h ROL`,
    d.oreExFestivita > 0 && `${d.oreExFestivita} h ex festivita`,
    d.oreAssenzaNonRetribuita > 0 && `${d.oreAssenzaNonRetribuita} h assenza n/r`,
    d.oreStraordinario + d.oreStraordinarioNotturno + d.oreStraordinarioFestivo > 0
      && `${d.oreStraordinario + d.oreStraordinarioNotturno + d.oreStraordinarioFestivo} h straord.`,
  ].filter(Boolean);
  return parti.length > 0 ? parti.join(" · ") : "Mese ordinario, nessuna variazione";
}

export function Presenze() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(oggi.getFullYear());
  const [mese, setMese] = useState(oggi.getMonth() + 1);
  const [board, setBoard] = useState<PresenzeMese | null>(null);
  const [dati, setDati] = useState<Record<string, DatiCartellino>>({});
  const [aperto, setAperto] = useState<string | null>(null);
  const [confermato, setConfermato] = useState<Record<string, boolean>>({});
  const [caricamento, setCaricamento] = useState(true);
  const [elaborando, setElaborando] = useState<string | null>(null);
  const [scaricando, setScaricando] = useState<string | null>(null);
  const [errore, setErrore] = useState<string | null>(null);
  const [avviso, setAvviso] = useState<string | null>(null);
  // Calendario giorno per giorno: presente solo per i dipendenti in cui è stato aperto.
  const [calendari, setCalendari] = useState<Record<string, GiornataCartellino[]>>({});
  const [problemi, setProblemi] = useState<Record<string, ProblemaCartellino[]>>({});
  const [verificando, setVerificando] = useState<string | null>(null);
  const richiesteBoard = useRef(creaSequenziatoreRichieste());
  const scopeCorrente = azienda ? `${azienda.id}:${anno}-${mese}` : null;
  const scopeCorrenteRef = useRef(scopeCorrente);
  const [scopeBoard, setScopeBoard] = useState<string | null>(null);
  scopeCorrenteRef.current = scopeCorrente;
  const boardCorrente = scopeBoard === scopeCorrente ? board : null;

  useEffect(() => {
    const controller = new AbortController();
    (async () => {
      try {
        const aziende = await api.aziende(controller.signal);
        const scelta = scegliAziendaPreferita(aziende);
        setAzienda(scelta);
        if (scelta) salvaAziendaCorrenteId(scelta.id);
      } catch (e) {
        if (e instanceof DOMException && e.name === "AbortError") return;
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
    return () => controller.abort();
  }, []);

  const carica = useCallback(async () => {
    if (!azienda) return;
    const scopeRichiesto = `${azienda.id}:${anno}-${mese}`;
    const richiesta = richiesteBoard.current.apri();
    setCaricamento(true);
    setBoard(null);
    setScopeBoard(null);
    setDati({});
    setConfermato({});
    setCalendari({});
    setProblemi({});
    try {
      const b = await api.presenzeMese(azienda.id, anno, mese, richiesta.signal);
      if (!richiesta.attiva() || scopeCorrenteRef.current !== scopeRichiesto) return;
      setBoard(b);
      setScopeBoard(scopeRichiesto);
      setDati(Object.fromEntries(b.righe.map((r) => [r.rapportoId, dalRiepilogo(r)])));
      setConfermato(Object.fromEntries(b.righe.map((r) => [r.rapportoId, r.elaborato])));
      // Il calendario riappare solo se era già stato compilato: chi lavora a totali non se lo trova.
      setCalendari(Object.fromEntries(
        b.righe.filter((r) => r.giornate.length > 0).map((r) => [r.rapportoId, r.giornate])));
      setProblemi({});
    } finally {
      if (richiesta.attiva()) setCaricamento(false);
    }
  }, [azienda, anno, mese]);

  useEffect(() => {
    setAperto(null);
    setErrore(null);
    setAvviso(null);
    if (!azienda) {
      richiesteBoard.current.invalida();
      setBoard(null);
      setScopeBoard(null);
      setCaricamento(false);
      return;
    }
    carica().catch((e) => {
      if (e instanceof DOMException && e.name === "AbortError") return;
      if (scopeCorrenteRef.current === `${azienda.id}:${anno}-${mese}`)
        setErrore(e instanceof Error ? e.message : "Errore");
    });
  }, [azienda, anno, mese, carica]);

  useEffect(() => () => richiesteBoard.current.invalida(), []);

  function aggiorna(rapportoId: string, chiave: keyof DatiCartellino, valore: number) {
    setDati((correnti) => ({
      ...correnti,
      [rapportoId]: { ...(correnti[rapportoId] ?? vuoto), [chiave]: valore },
    }));
    setConfermato((correnti) => ({ ...correnti, [rapportoId]: false }));
  }

  function preparaCambioPeriodo() {
    richiesteBoard.current.invalida();
    setCaricamento(true);
    setBoard(null);
    setScopeBoard(null);
    setDati({});
    setConfermato({});
    setCalendari({});
    setProblemi({});
    setAperto(null);
    setErrore(null);
    setAvviso(null);
  }

  function aggiornaAnf(
    rapportoId: string,
    stato: "DaVerificare" | "NonSpettanti" | "Spettanti",
  ) {
    setDati((correnti) => ({
      ...correnti,
      [rapportoId]: {
        ...(correnti[rapportoId] ?? vuoto),
        statoAssegniFamiliari: stato,
        importoAssegniFamiliari: stato === "Spettanti"
          ? (correnti[rapportoId]?.importoAssegniFamiliari ?? 0)
          : 0,
      },
    }));
    setConfermato((correnti) => ({ ...correnti, [rapportoId]: false }));
  }

  /** Il cartellino da mandare: col calendario aperto vincono le giornate, i totali li fa il server. */
  function corpo(rapportoId: string): Cartellino {
    const giornate = calendari[rapportoId];
    const base = { anno, mese, ...(dati[rapportoId] ?? vuoto) };
    return giornate ? { ...base, giornate } : base;
  }

  function apriCalendario(rapportoId: string, oreGiorno: number) {
    setCalendari((c) => ({ ...c, [rapportoId]: calendarioIniziale(anno, mese, oreGiorno) }));
    setConfermato((c) => ({ ...c, [rapportoId]: false }));
  }

  function chiudiCalendario(rapportoId: string) {
    setCalendari(({ [rapportoId]: _tolto, ...resto }) => resto);
    setProblemi(({ [rapportoId]: _via, ...resto }) => resto);
  }

  function aggiornaGiornata(rapportoId: string, indice: number, modifica: Partial<GiornataCartellino>) {
    setCalendari((correnti) => {
      const giornate = correnti[rapportoId];
      if (!giornate) return correnti;
      const nuove = giornate.map((g, i) => (i === indice ? { ...g, ...modifica } : g));
      return { ...correnti, [rapportoId]: nuove };
    });
    setConfermato((c) => ({ ...c, [rapportoId]: false }));
  }

  /** Cambio causale: azzera le ore dell'altra causale, così non restano valori orfani. */
  function cambiaCausale(
    rapportoId: string, indice: number, causale: CausaleGiornata, oreGiorno: number,
  ) {
    const giornate = calendari[rapportoId];
    if (!giornate) return;
    const precedente = giornate[indice];
    const ore = campoOre(causale)
      ? (campoOre(precedente.causale) ? Number(precedente[campoOre(precedente.causale)!]) : oreGiorno)
      : 0;
    aggiornaGiornata(rapportoId, indice, {
      ...giornataVuota(precedente.data, causale, ore),
      // Gli straordinari sopravvivono al cambio causale: sono un dato a parte.
      oreStraordinario: precedente.oreStraordinario,
      oreStraordinarioNotturno: precedente.oreStraordinarioNotturno,
      oreStraordinarioFestivo: precedente.oreStraordinarioFestivo,
    });
  }

  async function verifica(rapportoId: string) {
    const scopeVerifica = scopeCorrente;
    if (!scopeVerifica || scopeBoard !== scopeVerifica) return;
    setVerificando(rapportoId);
    setErrore(null);
    try {
      const esito = await api.verificaCartellino(rapportoId, corpo(rapportoId));
      if (scopeCorrenteRef.current !== scopeVerifica) return;
      setProblemi((p) => ({ ...p, [rapportoId]: esito.problemi }));
    } catch (e) {
      if (scopeCorrenteRef.current === scopeVerifica)
        setErrore(e instanceof Error ? e.message : "Impossibile controllare il mese.");
    } finally {
      setVerificando(null);
    }
  }

  async function elabora(rapportoId: string) {
    const scopeOperazione = scopeCorrente;
    if (!scopeOperazione || scopeBoard !== scopeOperazione
      || !boardCorrente?.righe.some((r) => r.rapportoId === rapportoId)) {
      setErrore("Il periodo è cambiato: attendi il caricamento del cartellino corretto.");
      return;
    }
    if (!confermato[rapportoId]) {
      setErrore("Controlla tutti i dati del mese e conferma il cartellino prima di elaborare.");
      return;
    }
    setElaborando(rapportoId);
    setErrore(null);
    setAvviso(null);
    const corpoOperazione = corpo(rapportoId);
    try {
      let cedolino;
      try {
        cedolino = await api.archiviaCedolino(rapportoId, corpoOperazione);
      } catch (e) {
        // Mese con F24 già inviato: il commercialista può correggere un errore
        // ricalcolando comunque, ma solo dopo conferma esplicita.
        const msg = e instanceof Error ? e.message : "";
        if (!msg.includes("ricalcolo forzato")) throw e;
        if (scopeCorrenteRef.current !== scopeOperazione) return;
        if (!confirm(`${msg}\n\nVuoi ricalcolare comunque questa busta paga?`)) return;
        cedolino = await api.archiviaCedolino(rapportoId, corpoOperazione, true);
        if (scopeCorrenteRef.current === scopeOperazione)
          setAvviso("Busta paga ricalcolata su un mese con F24 già inviato: ricordati di rigenerare l'F24 del mese.");
      }
      // Avviso (non blocco): saldi ferie/permessi sotto zero dopo questo cedolino.
      const sottoZero = [
        (cedolino.ferieResidue ?? 0) < 0 && `ferie residue ${cedolino.ferieResidue} gg`,
        (cedolino.permessiResidui ?? 0) < 0 && `permessi residui ${cedolino.permessiResidui} h`,
      ].filter(Boolean).join(" e ");
      if (sottoZero && scopeCorrenteRef.current === scopeOperazione)
        setAvviso(`Attenzione: ${sottoZero} sotto zero. La busta è valida, ma verifica le assenze inserite.`);
      if (scopeCorrenteRef.current === scopeOperazione) {
        await carica();
        if (scopeCorrenteRef.current === scopeOperazione) setAperto(null);
      }
    } catch (e) {
      if (scopeCorrenteRef.current === scopeOperazione)
        setErrore(e instanceof Error ? e.message : "Errore nel calcolo");
    } finally {
      setElaborando(null);
    }
  }

  async function scaricaPdf(rapportoId: string) {
    const scopeDownload = scopeCorrente;
    if (!scopeDownload || scopeBoard !== scopeDownload) return;
    setScaricando(rapportoId);
    setErrore(null);
    try {
      const cedolini = await api.cedoliniArchiviati(rapportoId);
      if (scopeCorrenteRef.current !== scopeDownload) return;
      const cedolino = cedolini.find((c) => c.anno === anno && c.mese === mese);
      if (!cedolino) throw new Error("Cedolino del periodo non trovato nell'archivio.");
      const { blob, filename } = await api.cedolinoPdf(cedolino.id);
      if (scopeCorrenteRef.current !== scopeDownload) return;
      await scaricaBlob(blob, filename);
    } catch (e) {
      if (scopeCorrenteRef.current === scopeDownload)
        setErrore(e instanceof Error ? e.message : "Impossibile scaricare il PDF.");
    } finally {
      setScaricando(null);
    }
  }

  function campo(rapportoId: string, etichetta: string, chiave: keyof DatiCartellino, unita: string) {
    return (
      <label>
        <span>{etichetta}</span>
        <div className="presence-number">
          <input
            type="number"
            min={chiave === "numeroEventoMalattiaAnno" ? 1 : 0}
            step={chiave === "numeroEventoMalattiaAnno" ? 1 : 0.5}
            value={(dati[rapportoId] ?? vuoto)[chiave]}
            onChange={(e) => aggiorna(rapportoId, chiave, Number(e.target.value))}
            onFocus={(e) => e.currentTarget.select()}
            onBlur={(e) => {
              const minimo = chiave === "numeroEventoMalattiaAnno" ? 1 : 0;
              const letto = Number(e.currentTarget.value);
              const normalizzato = Number.isFinite(letto) ? Math.max(minimo, letto) : minimo;
              e.currentTarget.value = String(normalizzato);
              if ((dati[rapportoId] ?? vuoto)[chiave] !== normalizzato) {
                aggiorna(rapportoId, chiave, normalizzato);
              }
            }}
          />
          <span>{unita}</span>
        </div>
      </label>
    );
  }

  return (
    <div>
      <div className="page-heading">
        <div>
          <h1>Cartellino mensile</h1>
          <p>Presenze e causali{azienda ? ` · ${azienda.ragioneSociale}` : ""} · {mesi[mese]} {anno}</p>
        </div>
        <span className="status-pill amber">Bozza da validare</span>
      </div>

      {caricamento ? (
        <section className="glass-card page-card"><div className="empty-state">Caricamento...</div></section>
      ) : !azienda ? (
        <section className="glass-card page-card"><div className="empty-state">Aggiungi prima un'azienda, poi i dipendenti da elaborare.</div></section>
      ) : (
        <div className="timesheet-layout">
          <div className="timesheet-main">
            <div className="ts-kpi-grid">
              <article className="glass-card ts-kpi">
                <span className="ts-kpi-icon tone-icon blue"><Users size={20} /></span>
                <div><span>Dipendenti</span><b className="num">{boardCorrente?.totale ?? 0}</b><small>Totale azienda</small></div>
              </article>
              <article className="glass-card ts-kpi">
                <span className="ts-kpi-icon tone-icon green"><CheckCircle2 size={20} /></span>
                <div><span>Cedolini elaborati</span><b className="num">{boardCorrente?.elaborati ?? 0}/{boardCorrente?.totale ?? 0}</b><small>Questo mese</small></div>
              </article>
            </div>

            <div className="glass-card page-card">
              <div className="form-grid presence-period">
                <label>
                  <span>Anno</span>
                  {/* Regola: le buste paga si elaborano al massimo per il mese corrente. */}
                  <input type="number" min={2000} max={oggi.getFullYear()} value={anno} onChange={(e) => {
                    preparaCambioPeriodo();
                    setAnno(Number(e.target.value));
                  }} />
                </label>
                <label>
                  <span>Mese</span>
                  <select value={mese} onChange={(e) => {
                    preparaCambioPeriodo();
                    setMese(Number(e.target.value));
                  }}>
                    {mesi.slice(1).map((m, i) => (
                      <option key={i + 1} value={i + 1}
                        disabled={anno >= oggi.getFullYear() && i + 1 > oggi.getMonth() + 1}>
                        {m}{anno >= oggi.getFullYear() && i + 1 > oggi.getMonth() + 1 ? " (futuro)" : ""}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              {caricamento ? (
                <div className="empty-state">Caricamento del periodo…</div>
              ) : !boardCorrente || boardCorrente.righe.length === 0 ? (
                <div className="empty-state">Nessun dipendente. Aggiungine uno in Dipendenti.</div>
              ) : (
                <div className="table-wrap">
                  <table className="data-table spacious presence-table">
                    <thead><tr><th>Dipendente</th><th>Riepilogo mese</th><th>Stato</th><th className="right">Netto</th><th className="right">Azioni</th></tr></thead>
                    <tbody>
                      {boardCorrente.righe.map((r) => {
                        const cartellino = dati[r.rapportoId] ?? vuoto;
                        const espanso = aperto === r.rapportoId;
                        return (
                          <Fragment key={r.rapportoId}>
                            <tr>
                              <td><strong>{r.nominativo || "-"}</strong></td>
                              <td className="presence-summary" data-aurora-private>
                                {descrizione(cartellino)}
                              </td>
                              <td><span className={`status-pill ${r.elaborato ? "green" : confermato[r.rapportoId] ? "blue" : "amber"}`}>
                                {r.elaborato ? "Elaborato" : confermato[r.rapportoId] ? "Pronto" : "Da controllare"}
                              </span></td>
                              <td className="right num">{r.elaborato ? `${euro(r.netto)} EUR` : "-"}</td>
                              <td className="right">
                                <div className="actions-row" style={{ justifyContent: "flex-end" }}>
                                  {r.elaborato && (
                                    <button
                                      className="mini-action icon-command"
                                      type="button"
                                      title="Scarica la busta paga in PDF"
                                      disabled={scaricando === r.rapportoId}
                                      onClick={() => { void scaricaPdf(r.rapportoId); }}
                                    >
                                      <Download size={14} />
                                      <span>{scaricando === r.rapportoId ? "Download..." : "PDF"}</span>
                                    </button>
                                  )}
                                  <button className="mini-action icon-command" type="button" title="Compila cartellino" onClick={() => setAperto(espanso ? null : r.rapportoId)}>
                                    {espanso ? <X size={14} /> : <Pencil size={14} />}<span>{espanso ? "Chiudi" : "Compila"}</span>
                                  </button>
                                </div>
                              </td>
                            </tr>
                            {espanso && (
                              <tr className="presence-editor-row" data-aurora-private><td colSpan={5}>
                                <div className="presence-editor">
                                  <div className="presence-section calendario-testa">
                                    <h3>Come vuoi compilare il mese</h3>
                                    <div className="actions-row">
                                      <button
                                        className={`mini-action icon-command${calendari[r.rapportoId] ? "" : " attivo"}`}
                                        type="button"
                                        onClick={() => chiudiCalendario(r.rapportoId)}
                                      >
                                        <Pencil size={14} /><span>A totali</span>
                                      </button>
                                      <button
                                        className={`mini-action icon-command${calendari[r.rapportoId] ? " attivo" : ""}`}
                                        type="button"
                                        onClick={() => apriCalendario(r.rapportoId, r.oreGiornaliere)}
                                      >
                                        <CalendarDays size={14} /><span>Giorno per giorno</span>
                                      </button>
                                    </div>
                                    <p className="muto">
                                      {calendari[r.rapportoId]
                                        ? "I totali del mese li ricava il programma dalle giornate: quello che scrivi qui sotto è ciò che finisce in busta, nel Libro Unico e nelle denunce."
                                        : "Stai compilando i totali del mese. Il giorno per giorno serve per il Libro Unico, per la malattia nel nuovo UniEmens e per chi si paga a giornate."}
                                    </p>
                                  </div>

                                  {calendari[r.rapportoId] ? (
                                    <div className="presence-section">
                                      <h3>Calendario di {mesi[mese]} {anno}</h3>
                                      <div className="table-wrap calendario-mese">
                                        <table className="data-table">
                                          <thead><tr>
                                            <th>Giorno</th><th>Che cosa è stato</th>
                                            <th className="right">Ore</th><th className="right">Straordinario</th>
                                          </tr></thead>
                                          <tbody>
                                            {calendari[r.rapportoId].map((g, i) => {
                                              const data = new Date(g.data + "T00:00:00");
                                              const festivo = data.getDay() === 0;
                                              const campo = campoOre(g.causale);
                                              return (
                                                <tr key={g.data} className={festivo ? "giorno-riposo" : undefined}>
                                                  <td>
                                                    <strong>{data.getDate()}</strong>{" "}
                                                    <span className="muto">{GIORNI_SETTIMANA[data.getDay()]}</span>
                                                  </td>
                                                  <td>
                                                    <select
                                                      value={g.causale}
                                                      onChange={(e) => cambiaCausale(
                                                        r.rapportoId, i,
                                                        e.target.value as CausaleGiornata, r.oreGiornaliere)}
                                                    >
                                                      {CAUSALI.map((c) => (
                                                        <option key={c.valore} value={c.valore}>{c.etichetta}</option>
                                                      ))}
                                                    </select>
                                                  </td>
                                                  <td className="right">
                                                    {campo ? (
                                                      <input
                                                        type="number" min={0} step={0.5}
                                                        value={Number(g[campo])}
                                                        onChange={(e) => aggiornaGiornata(
                                                          r.rapportoId, i,
                                                          { [campo]: Math.max(0, Number(e.target.value)) })}
                                                        onFocus={(e) => e.currentTarget.select()}
                                                      />
                                                    ) : <span className="muto">—</span>}
                                                  </td>
                                                  <td className="right">
                                                    <input
                                                      type="number" min={0} step={0.5}
                                                      value={g.oreStraordinario}
                                                      onChange={(e) => aggiornaGiornata(
                                                        r.rapportoId, i,
                                                        { oreStraordinario: Math.max(0, Number(e.target.value)) })}
                                                      onFocus={(e) => e.currentTarget.select()}
                                                    />
                                                  </td>
                                                </tr>
                                              );
                                            })}
                                          </tbody>
                                        </table>
                                      </div>
                                      {calendari[r.rapportoId].some((g) => g.causale === "Malattia") && (
                                        <div className="presence-fields" style={{ marginTop: 10 }}>
                                          {campo(r.rapportoId, "Evento di malattia nell'anno", "numeroEventoMalattiaAnno", "numero")}
                                        </div>
                                      )}
                                      <div className="actions-row" style={{ marginTop: 10 }}>
                                        <button
                                          className="btn secondary" type="button"
                                          disabled={verificando === r.rapportoId}
                                          onClick={() => { void verifica(r.rapportoId); }}
                                        >
                                          {verificando === r.rapportoId ? "Controllo..." : "Controlla il mese"}
                                        </button>
                                      </div>
                                      {problemi[r.rapportoId] && (
                                        problemi[r.rapportoId].length === 0 ? (
                                          <p><span className="status-pill green">Mese in ordine</span></p>
                                        ) : (
                                          <ul className="calendario-problemi">
                                            {problemi[r.rapportoId].map((p, i) => (
                                              <li key={i} className={p.bloccante ? "bloccante" : undefined}>
                                                <span className={`status-pill ${p.bloccante ? "red" : "amber"}`}>
                                                  {p.bloccante ? "Da correggere" : "Attenzione"}
                                                </span>{" "}
                                                {p.messaggio}
                                              </li>
                                            ))}
                                          </ul>
                                        )
                                      )}
                                    </div>
                                  ) : (
                                  <>
                                  <div className="presence-section"><h3>Ferie e permessi retribuiti</h3><div className="presence-fields">
                                    {campo(r.rapportoId, "Ferie godute", "giorniFerie", "giorni")}
                                    {campo(r.rapportoId, "Permessi ROL", "orePermessi", "ore")}
                                    {campo(r.rapportoId, "Ex festivita", "oreExFestivita", "ore")}
                                  </div><DirittiFeriePermessi riga={r} cartellino={cartellino} /></div>
                                  {/* segue: sezioni a totali, usate solo senza calendario */}
                                  <div className="presence-section"><h3>Malattia e infortunio</h3><div className="presence-fields">
                                    {campo(r.rapportoId, "Malattia", "giorniMalattia", "giorni")}
                                    {campo(r.rapportoId, "Evento nell'anno", "numeroEventoMalattiaAnno", "numero")}
                                    {campo(r.rapportoId, "Infortunio INAIL", "giorniInfortunio", "giorni")}
                                  </div></div>
                                  <div className="presence-section"><h3>Assenze e lavoro aggiuntivo</h3><div className="presence-fields">
                                    {campo(r.rapportoId, "Assenza non retribuita", "oreAssenzaNonRetribuita", "ore")}
                                    {campo(r.rapportoId, "Straordinario", "oreStraordinario", "ore")}
                                    {campo(r.rapportoId, "Straordinario notturno", "oreStraordinarioNotturno", "ore")}
                                    {campo(r.rapportoId, "Straordinario festivo", "oreStraordinarioFestivo", "ore")}
                                  </div></div>
                                  </>
                                  )}
                                  <div className="presence-section">
                                    <h3>Assegni familiari (ANF)</h3>
                                    <p className="muto">Indica sempre l’esito del mese. Se spettano, scrivi l’importo: il PDF finale non accetta un dato sconosciuto.</p>
                                    <div className="presence-fields">
                                      <label>
                                        <span>Esito verifica</span>
                                        <select value={cartellino.statoAssegniFamiliari}
                                          onChange={(e) => aggiornaAnf(
                                            r.rapportoId,
                                            e.target.value as "DaVerificare" | "NonSpettanti" | "Spettanti")}>
                                          <option value="DaVerificare">Da verificare</option>
                                          <option value="NonSpettanti">Non spettanti</option>
                                          <option value="Spettanti">Spettanti</option>
                                        </select>
                                      </label>
                                      {cartellino.statoAssegniFamiliari === "Spettanti" && (
                                        <label>
                                          <span>Importo ANF del mese</span>
                                          <div className="presence-number">
                                            <input type="number" min={0.01} step={0.01}
                                              value={cartellino.importoAssegniFamiliari}
                                              onChange={(e) => aggiorna(
                                                r.rapportoId, "importoAssegniFamiliari",
                                                Math.max(0, Number(e.target.value)))} />
                                            <span>EUR</span>
                                          </div>
                                        </label>
                                      )}
                                    </div>
                                  </div>
                                  <div className="presence-confirm">
                                    <label><input type="checkbox" checked={Boolean(confermato[r.rapportoId])} onChange={(e) => setConfermato((c) => ({ ...c, [r.rapportoId]: e.target.checked }))} />
                                      <span>Ho controllato presenze, ferie, permessi, malattia, infortunio e assenze del mese.</span>
                                    </label>
                                    <button className="btn primary" type="button"
                                      disabled={!confermato[r.rapportoId]
                                        || scopeBoard !== scopeCorrente
                                        || caricamento
                                        || cartellino.statoAssegniFamiliari === "DaVerificare"
                                        || (cartellino.statoAssegniFamiliari === "Spettanti"
                                          && cartellino.importoAssegniFamiliari <= 0)
                                        || elaborando === r.rapportoId}
                                      onClick={() => elabora(r.rapportoId)}>
                                      <Calculator size={16} /> {elaborando === r.rapportoId ? "Elaborazione..." : r.elaborato ? "Ricalcola cedolino" : "Genera cedolino"}
                                    </button>
                                  </div>
                                </div>
                              </td></tr>
                            )}
                          </Fragment>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
              {errore && <p className="errore">{errore}</p>}
              {avviso && (
                <p style={{ marginTop: 10, padding: "10px 14px", borderRadius: 10, fontSize: 13,
                  background: "rgba(243,156,18,0.14)", border: "1.5px solid #f39c12", color: "#b9770e" }}>
                  ⚠️ {avviso}
                </p>
              )}
            </div>
          </div>

          <aside className="timesheet-rail">
            <section className="glass-card side-card">
              <div className="rail-head"><strong>Controllo prima del calcolo</strong></div>
              <div className="check-row"><CheckCircle2 size={17} /><strong>Seleziona il periodo corretto</strong></div>
              <div className="check-row"><CheckCircle2 size={17} /><strong>Compila tutte le causali del dipendente</strong></div>
              <div className="check-row"><CheckCircle2 size={17} /><strong>Conferma e genera il cedolino</strong></div>
              <p className="muto presence-note">Il PDF riporta le causali inserite. I calcoli restano da validare con un consulente del lavoro.</p>
            </section>
          </aside>
        </div>
      )}
    </div>
  );
}
