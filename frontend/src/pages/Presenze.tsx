import { Fragment, useCallback, useEffect, useRef, useState } from "react";
import { Calculator, CalendarDays, CheckCircle2, Download, Pencil, Users, X } from "lucide-react";
import {
  api, scaricaBlob,
  type Azienda, type Cartellino, type CausaleGiornata,
  type GiornataCartellino, type PresenzeMese, type ProblemaCartellino,
} from "../api";
import { salvaAziendaCorrenteId, scegliAziendaPreferita } from "../aziendaCorrente";
import {
  calendarioIniziale,
  applicaProgressiviSettimanali,
  campoOre,
  cambiaCausaleGiornata,
  causaleConsenteStraordinario,
  validaCalendarioArchiviabile,
} from "../cartellinoCalendario";
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
  riferimentoQuattordicesimaSeparata: "", dataPagamentoQuattordicesima: "",
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
    riferimentoQuattordicesimaSeparata: r.riferimentoQuattordicesimaSeparata ?? "",
    dataPagamentoQuattordicesima: r.dataPagamentoQuattordicesima ?? "",
  };
}

// Mantiene visibile il controllo sui residui anche con il calendario obbligatorio.
function DirittiFeriePermessi({ riga, giornate }: {
  riga: PresenzeMese["righe"][number];
  giornate: GiornataCartellino[];
}) {
  if (riga.ferieResidue == null || riga.permessiResidui == null
    || riga.saldiAnno == null || riga.saldiMese == null) {
    return (
      <p className="muto">
        Residui ferie/permessi non ancora disponibili: si calcolano col primo cedolino elaborato.
      </p>
    );
  }
  const ferieChieste = giornate.filter((giornata) => giornata.causale === "Ferie").length;
  const permessiChiesti = giornate.reduce(
    (totale, giornata) => totale + giornata.orePermessi + giornata.oreExFestivita,
    0,
  );
  const ferieOltre = ferieChieste > riga.ferieResidue;
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
          Ferie oltre il residuo: {ferieChieste} gg su {riga.ferieResidue.toLocaleString("it-IT")} disponibili (anticipate)
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

const CAMPI_STRAORDINARIO: {
  chiave: "oreStraordinario" | "oreStraordinarioNotturno" | "oreStraordinarioFestivo";
  etichetta: string;
}[] = [
  { chiave: "oreStraordinario", etichetta: "Str. ord." },
  { chiave: "oreStraordinarioNotturno", etichetta: "Str. nott." },
  { chiave: "oreStraordinarioFestivo", etichetta: "Str. fest." },
];

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
  // Senza calendario completo il cedolino resta non elaborabile.
  const [calendari, setCalendari] = useState<Record<string, GiornataCartellino[]>>({});
  const [riportiPrimaSettimana, setRiportiPrimaSettimana] = useState<Record<string, number | null>>({});
  const [problemi, setProblemi] = useState<Record<string, ProblemaCartellino[]>>({});
  const [verificando, setVerificando] = useState<string | null>(null);
  // «Fai il mese»: una conferma sola per tutti invece di quattro clic a testa.
  // La conferma esplicita del professionista resta obbligatoria, ma la dà una
  // volta vedendo l'elenco completo, non dodici volte a scatola chiusa.
  const [lottoAperto, setLottoAperto] = useState(false);
  const [lottoConfermato, setLottoConfermato] = useState(false);
  const [lottoInCorso, setLottoInCorso] = useState<string | null>(null);
  const [lottoEsiti, setLottoEsiti] = useState<Record<string, string> | null>(null);
  const [pdfInCorso, setPdfInCorso] = useState<number | null>(null);
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
    setRiportiPrimaSettimana({});
    setProblemi({});
    try {
      const b = await api.presenzeMese(azienda.id, anno, mese, richiesta.signal);
      if (!richiesta.attiva() || scopeCorrenteRef.current !== scopeRichiesto) return;
      setBoard(b);
      setScopeBoard(scopeRichiesto);
      setDati(Object.fromEntries(b.righe.map((r) => [r.rapportoId, dalRiepilogo(r)])));
      setConfermato(Object.fromEntries(b.righe.map((r) => [r.rapportoId, false])));
      // I vecchi cartellini a soli totali restano visibili, ma vanno convertiti in calendario
      // prima di poter creare una nuova versione archiviata.
      setCalendari(Object.fromEntries(
        b.righe.filter((r) => r.giornate.length > 0).map((r) => [r.rapportoId, r.giornate])));
      setRiportiPrimaSettimana(Object.fromEntries(
        b.righe.filter((r) => r.giornate.length > 0).map((r) => {
          const prima = r.giornate[0];
          const giorno = new Date(`${prima.data}T00:00:00`).getDay();
          const orePrima = prima.oreOrdinarie + prima.oreStraordinario
            + prima.oreStraordinarioNotturno + prima.oreStraordinarioFestivo;
          return [r.rapportoId, giorno === 1
            ? 0
            : prima.progressivoOreSettimanali == null
              ? null
              : prima.progressivoOreSettimanali - orePrima];
        })));
      setProblemi({});
      setLottoAperto(false);
      setLottoConfermato(false);
      setLottoEsiti(null);
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
    setProblemi(({ [rapportoId]: _rimosso, ...resto }) => resto);
    // Cambiati i dati, la conferma data prima non vale più.
    setLottoConfermato(false);
  }

  function preparaCambioPeriodo() {
    richiesteBoard.current.invalida();
    setCaricamento(true);
    setBoard(null);
    setScopeBoard(null);
    setDati({});
    setConfermato({});
    setCalendari({});
    setRiportiPrimaSettimana({});
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
    setProblemi(({ [rapportoId]: _rimosso, ...resto }) => resto);
    setLottoConfermato(false);
  }

  /** Il calendario vince sui riepiloghi e il server ne ricava i totali. */
  function corpo(rapportoId: string): Cartellino {
    const giornateOriginali = calendari[rapportoId];
    const giornate = giornateOriginali
      ? applicaProgressiviSettimanali(
          giornateOriginali,
          riportiPrimaSettimana[rapportoId] ?? null,
        )
      : undefined;
    const base = { anno, mese, ...(dati[rapportoId] ?? vuoto) };
    return giornate ? { ...base, giornate } : base;
  }

  function apriCalendario(rapportoId: string, oreGiorno: number) {
    const calendario = calendarioIniziale(anno, mese, oreGiorno);
    const cominciaLunedi = new Date(`${calendario[0].data}T00:00:00`).getDay() === 1;
    setRiportiPrimaSettimana((c) => ({ ...c, [rapportoId]: cominciaLunedi ? 0 : null }));
    setCalendari((c) => ({ ...c, [rapportoId]: calendario }));
    setConfermato((c) => ({ ...c, [rapportoId]: false }));
    setProblemi(({ [rapportoId]: _rimosso, ...resto }) => resto);
    setLottoConfermato(false);
  }

  function aggiornaGiornata(rapportoId: string, indice: number, modifica: Partial<GiornataCartellino>) {
    setCalendari((correnti) => {
      const giornate = correnti[rapportoId];
      if (!giornate) return correnti;
      const modificate = giornate.map((g, i) => (i === indice ? { ...g, ...modifica } : g));
      const nuove = applicaProgressiviSettimanali(
        modificate,
        riportiPrimaSettimana[rapportoId] ?? null,
      );
      return { ...correnti, [rapportoId]: nuove };
    });
    setConfermato((c) => ({ ...c, [rapportoId]: false }));
    setProblemi(({ [rapportoId]: _rimosso, ...resto }) => resto);
    setLottoConfermato(false);
  }

  function aggiornaRiportoPrimaSettimana(rapportoId: string, valore: number | null) {
    setRiportiPrimaSettimana((correnti) => ({ ...correnti, [rapportoId]: valore }));
    setCalendari((correnti) => {
      const giornate = correnti[rapportoId];
      return giornate ? {
        ...correnti,
        [rapportoId]: applicaProgressiviSettimanali(giornate, valore),
      } : correnti;
    });
    setConfermato((correnti) => ({ ...correnti, [rapportoId]: false }));
    setProblemi(({ [rapportoId]: _rimosso, ...resto }) => resto);
    setLottoConfermato(false);
  }

  function aggiornaTesto(
    rapportoId: string,
    chiave: "riferimentoQuattordicesimaSeparata" | "dataPagamentoQuattordicesima",
    valore: string,
  ) {
    setDati((correnti) => ({
      ...correnti,
      [rapportoId]: { ...(correnti[rapportoId] ?? vuoto), [chiave]: valore },
    }));
    setConfermato((correnti) => ({ ...correnti, [rapportoId]: false }));
    setProblemi(({ [rapportoId]: _rimosso, ...resto }) => resto);
    setLottoConfermato(false);
  }

  /** Cambio causale: il normalizzatore azzera ore e straordinari incompatibili. */
  function cambiaCausale(
    rapportoId: string, indice: number, causale: CausaleGiornata, oreGiorno: number,
  ) {
    const giornate = calendari[rapportoId];
    if (!giornate) return;
    aggiornaGiornata(
      rapportoId,
      indice,
      cambiaCausaleGiornata(giornate[indice], causale, oreGiorno),
    );
  }

  function problemiCalendario(rapportoId: string): ProblemaCartellino[] {
    return validaCalendarioArchiviabile(anno, mese, calendari[rapportoId]);
  }

  function calendarioVerificato(rapportoId: string): boolean {
    const esitoServer = problemi[rapportoId];
    return problemiCalendario(rapportoId).length === 0
      && esitoServer !== undefined
      && !esitoServer.some((problema) => problema.bloccante);
  }

  function anfCompleto(rapportoId: string): boolean {
    const cartellino = dati[rapportoId] ?? vuoto;
    return cartellino.statoAssegniFamiliari !== "DaVerificare"
      && (cartellino.statoAssegniFamiliari !== "Spettanti"
        || cartellino.importoAssegniFamiliari > 0);
  }

  async function verifica(rapportoId: string) {
    const scopeVerifica = scopeCorrente;
    if (!scopeVerifica || scopeBoard !== scopeVerifica) return;
    const locali = problemiCalendario(rapportoId);
    if (locali.length > 0) {
      setErrore(locali[0].messaggio);
      return;
    }
    setVerificando(rapportoId);
    setErrore(null);
    try {
      const esito = await api.verificaCartellino(rapportoId, corpo(rapportoId));
      if (scopeCorrenteRef.current !== scopeVerifica) return;
      const problemiServer = !esito.elaborabile
        && !esito.problemi.some((problema) => problema.bloccante)
        ? [...esito.problemi, {
            messaggio: "Il controllo del server non considera elaborabile questo calendario.",
            bloccante: true,
          }]
        : esito.problemi;
      setProblemi((p) => ({ ...p, [rapportoId]: problemiServer }));
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
    const locali = problemiCalendario(rapportoId);
    if (locali.length > 0) {
      setErrore(locali[0].messaggio);
      return;
    }
    if (problemi[rapportoId] === undefined) {
      setErrore("Controlla il calendario del mese prima di elaborare il cedolino.");
      return;
    }
    const bloccante = problemi[rapportoId].find((problema) => problema.bloccante);
    if (bloccante) {
      setErrore(`Correggi il calendario prima di elaborare: ${bloccante.messaggio}`);
      return;
    }
    if (!anfCompleto(rapportoId)) {
      setErrore("Completa la verifica degli assegni familiari prima di elaborare il cedolino.");
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

  /** Nel lotto entrano solo calendari completi, controllati e con ANF definito. */
  const daElaborare = (boardCorrente?.righe ?? []).filter((r) =>
    !r.elaborato && calendarioVerificato(r.rapportoId) && anfCompleto(r.rapportoId));
  const daPreparare = (boardCorrente?.righe ?? []).filter((r) => !r.elaborato).length
    - daElaborare.length;

  /**
   * Elabora in sequenza tutte le buste del mese dopo un'unica conferma.
   * Regole invariate: niente ricalcolo forzato in blocco — un mese con F24 già
   * inviato viene saltato e segnalato, si corregge uno per uno.
   */
  async function elaboraLotto() {
    const scopeOperazione = scopeCorrente;
    if (!scopeOperazione || scopeBoard !== scopeOperazione) return;
    if (!lottoConfermato) {
      setErrore("Spunta la conferma: le buste paga non partono senza il tuo controllo.");
      return;
    }
    setErrore(null);
    setAvviso(null);
    const esiti: Record<string, string> = {};
    let fatte = 0;
    const avvisi: string[] = [];

    for (const riga of daElaborare) {
      if (scopeCorrenteRef.current !== scopeOperazione) return;
      setLottoInCorso(riga.rapportoId);
      try {
        const cedolino = await api.archiviaCedolino(riga.rapportoId, corpo(riga.rapportoId));
        esiti[riga.rapportoId] = "fatta";
        fatte += 1;
        const sottoZero = [
          (cedolino.ferieResidue ?? 0) < 0 && "ferie",
          (cedolino.permessiResidui ?? 0) < 0 && "permessi",
        ].filter(Boolean).join(" e ");
        if (sottoZero) avvisi.push(`${riga.nominativo}: ${sottoZero} sotto zero`);
      } catch (e) {
        const msg = e instanceof Error ? e.message : "errore";
        esiti[riga.rapportoId] = msg.includes("ricalcolo forzato")
          ? "saltata: mese con F24 già inviato, correggila da «Compila»"
          : `non fatta: ${msg}`;
      }
    }

    setLottoInCorso(null);
    if (scopeCorrenteRef.current !== scopeOperazione) return;
    setLottoEsiti(esiti);
    setLottoConfermato(false);
    if (avvisi.length > 0)
      setAvviso(`Buste valide, ma controlla le assenze: ${avvisi.join(" · ")}.`);
    const saltate = daElaborare.length - fatte;
    if (saltate > 0)
      setErrore(`${fatte} buste elaborate, ${saltate} no: guarda il motivo accanto al nome.`);
    await carica();
    if (scopeCorrenteRef.current === scopeOperazione) {
      setLottoEsiti(esiti);
      setAperto(null);
    }
  }

  /**
   * Scarica in fila tutte le buste del mese. Il browser chiede una volta il
   * permesso per più file; su telefono ogni PDF passa dal foglio di condivisione.
   */
  async function scaricaTutti() {
    const scopeDownload = scopeCorrente;
    if (!scopeDownload || scopeBoard !== scopeDownload) return;
    const elaborati = (boardCorrente?.righe ?? []).filter((r) => r.elaborato);
    if (elaborati.length === 0) return;
    setErrore(null);
    const falliti: string[] = [];
    for (let i = 0; i < elaborati.length; i += 1) {
      if (scopeCorrenteRef.current !== scopeDownload) break;
      setPdfInCorso(i + 1);
      try {
        const cedolini = await api.cedoliniArchiviati(elaborati[i].rapportoId);
        const cedolino = cedolini.find((c) => c.anno === anno && c.mese === mese);
        if (!cedolino) throw new Error("non trovato in archivio");
        const { blob, filename } = await api.cedolinoPdf(cedolino.id);
        await scaricaBlob(blob, filename);
      } catch {
        falliti.push(elaborati[i].nominativo || "senza nome");
      }
    }
    setPdfInCorso(null);
    if (scopeCorrenteRef.current === scopeDownload && falliti.length > 0)
      setErrore(`Non sono riuscito a scaricare: ${falliti.join(", ")}.`);
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
                <>
                <section className="fai-mese" aria-label="Fai le buste paga del mese">
                  <div className="fai-mese-testa">
                    <div>
                      <strong>
                        {boardCorrente.elaborati === boardCorrente.totale
                          ? `Mese completo: ${boardCorrente.totale} buste paga fatte`
                          : `${boardCorrente.elaborati} buste su ${boardCorrente.totale}`}
                      </strong>
                      <small>
                        {[
                          daElaborare.length > 0 ? `${daElaborare.length} da elaborare` : null,
                          daPreparare > 0 ? `${daPreparare} da compilare o controllare` : null,
                          `${mesi[mese]} ${anno}`,
                        ].filter(Boolean).join(" · ")}
                      </small>
                    </div>
                    <div className="fai-mese-bottoni">
                      {daElaborare.length > 0 && !lottoAperto && (
                        <button className="btn primary" type="button" onClick={() => setLottoAperto(true)}>
                          Fai le {daElaborare.length} buste del mese
                        </button>
                      )}
                      {boardCorrente.elaborati > 0 && (
                        <button
                          className="btn secondary"
                          type="button"
                          disabled={pdfInCorso !== null}
                          onClick={() => { void scaricaTutti(); }}
                        >
                          <Download size={16} aria-hidden="true" />
                          {pdfInCorso !== null
                            ? `Scarico ${pdfInCorso} di ${boardCorrente.elaborati}…`
                            : `Scarica le ${boardCorrente.elaborati} buste in PDF`}
                        </button>
                      )}
                    </div>
                  </div>

                  {lottoAperto && daElaborare.length > 0 && (
                    <div className="fai-mese-conferma">
                      <p>
                        Controlla il riepilogo di ognuno. Chi ha assenze o casi particolari va
                        sistemato prima con «Compila»: qui elaboro i dati che vedi.
                      </p>
                      <ul className="fai-mese-elenco">
                        {daElaborare.map((r) => (
                          <li key={r.rapportoId}>
                            <b>{r.nominativo || "-"}</b>
                            <span data-aurora-private>{descrizione(dati[r.rapportoId] ?? vuoto)}</span>
                            {lottoInCorso === r.rapportoId && <em>in corso…</em>}
                          </li>
                        ))}
                      </ul>
                      <label className="fai-mese-spunta">
                        <input
                          type="checkbox"
                          checked={lottoConfermato}
                          disabled={lottoInCorso !== null}
                          onChange={(e) => setLottoConfermato(e.target.checked)}
                        />
                        <span>Ho controllato i dati di tutti e {daElaborare.length} e confermo l'elaborazione.</span>
                      </label>
                      <div className="fai-mese-bottoni">
                        <button
                          className="btn primary"
                          type="button"
                          disabled={!lottoConfermato || lottoInCorso !== null}
                          onClick={() => { void elaboraLotto(); }}
                        >
                          {lottoInCorso !== null
                            ? "Elaborazione in corso…"
                            : `Elabora ${daElaborare.length} buste paga`}
                        </button>
                        <button
                          className="btn"
                          type="button"
                          disabled={lottoInCorso !== null}
                          onClick={() => { setLottoAperto(false); setLottoConfermato(false); }}
                        >
                          Annulla
                        </button>
                      </div>
                    </div>
                  )}
                </section>

                <div className="table-wrap">
                  <table className="data-table spacious presence-table">
                    <thead><tr><th>Dipendente</th><th>Riepilogo mese</th><th>Stato</th><th className="right">Netto</th><th className="right">Azioni</th></tr></thead>
                    <tbody>
                      {boardCorrente.righe.map((r) => {
                        const cartellino = dati[r.rapportoId] ?? vuoto;
                        const espanso = aperto === r.rapportoId;
                        const problemiLocali = problemiCalendario(r.rapportoId);
                        const esitoServer = problemi[r.rapportoId];
                        const problemiVisibili = [
                          ...problemiLocali,
                          ...(esitoServer ?? []),
                        ];
                        const meseControllato = calendarioVerificato(r.rapportoId);
                        return (
                          <Fragment key={r.rapportoId}>
                            <tr>
                              <td><strong>{r.nominativo || "-"}</strong></td>
                              <td className="presence-summary" data-aurora-private>
                                {descrizione(cartellino)}
                              </td>
                              <td>
                                <span className={`status-pill ${r.elaborato ? "green" : confermato[r.rapportoId] ? "blue" : "amber"}`}>
                                  {r.elaborato ? "Elaborato" : confermato[r.rapportoId] ? "Pronto" : "Da controllare"}
                                </span>
                                {lottoEsiti?.[r.rapportoId] && lottoEsiti[r.rapportoId] !== "fatta" && (
                                  <small className="esito-lotto">{lottoEsiti[r.rapportoId]}</small>
                                )}
                              </td>
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
                                  <button className="mini-action icon-command" type="button" title="Compila cartellino" onClick={() => {
                                    if (espanso) {
                                      setAperto(null);
                                      return;
                                    }
                                    if (!calendari[r.rapportoId]) {
                                      apriCalendario(r.rapportoId, r.oreGiornaliere);
                                    }
                                    setAperto(r.rapportoId);
                                  }}>
                                    {espanso ? <X size={14} /> : <Pencil size={14} />}<span>{espanso ? "Chiudi" : "Compila"}</span>
                                  </button>
                                </div>
                              </td>
                            </tr>
                            {espanso && (
                              <tr className="presence-editor-row" data-aurora-private><td colSpan={5}>
                                <div className="presence-editor">
                                  <div className="presence-section calendario-testa">
                                    <h3><CalendarDays size={16} aria-hidden="true" /> Calendario giornaliero obbligatorio</h3>
                                    <p className="muto">
                                      I totali sono ricavati esclusivamente dalle giornate. Senza tutti i giorni del mese
                                      compilati e controllati non è possibile archiviare il cedolino.
                                    </p>
                                  </div>

                                  {calendari[r.rapportoId] ? (
                                    <div className="presence-section">
                                      <h3>Calendario di {mesi[mese]} {anno}</h3>
                                      {new Date(`${calendari[r.rapportoId][0].data}T00:00:00`).getDay() !== 1 && (
                                        <div className="presence-fields" style={{ marginBottom: 10 }}>
                                          <label>
                                            <span>Ore già lavorate nella prima settimana</span>
                                            <input
                                              type="number" min={0} step={0.5}
                                              placeholder="Somma dei giorni del mese precedente"
                                              value={riportiPrimaSettimana[r.rapportoId] ?? ""}
                                              onChange={(e) => aggiornaRiportoPrimaSettimana(
                                                r.rapportoId,
                                                e.target.value === "" ? null : Math.max(0, Number(e.target.value)),
                                              )}
                                            />
                                            <small className="muto">
                                              Il mese inizia a settimana già avviata: inserisci il progressivo
                                              ricavato dal cartellino precedente, anche quando è zero.
                                            </small>
                                          </label>
                                        </div>
                                      )}
                                      <div className="table-wrap calendario-mese">
                                        <table className="data-table">
                                          <thead><tr>
                                            <th>Giorno</th><th>Che cosa è stato</th>
                                            <th className="right">Ore</th>
                                            <th className="right">Progressivo sett.</th>
                                            {CAMPI_STRAORDINARIO.map((campoStraordinario) => (
                                              <th className="right" key={campoStraordinario.chiave}>
                                                {campoStraordinario.etichetta}
                                              </th>
                                            ))}
                                          </tr></thead>
                                          <tbody>
                                            {calendari[r.rapportoId].map((g, i) => {
                                              const data = new Date(g.data + "T00:00:00");
                                              const festivo = data.getDay() === 0 || data.getDay() === 6;
                                              const campo = campoOre(g.causale);
                                              const straordinarioConsentito = causaleConsenteStraordinario(g.causale);
                                              const eventoSanitario = g.causale === "Malattia"
                                                || g.causale === "Infortunio";
                                              return (
                                                <Fragment key={g.data}>
                                                <tr className={festivo ? "giorno-riposo" : undefined}>
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
                                                  <td className="right num">
                                                    {g.progressivoOreSettimanali == null
                                                      ? <span className="status-pill red">manca</span>
                                                      : `${g.progressivoOreSettimanali.toLocaleString("it-IT")} h`}
                                                  </td>
                                                  {CAMPI_STRAORDINARIO.map((campoStraordinario) => (
                                                    <td className="right" key={campoStraordinario.chiave}>
                                                      {straordinarioConsentito ? (
                                                        <input
                                                          type="number" min={0} step={0.5}
                                                          aria-label={`${campoStraordinario.etichetta} del ${g.data}`}
                                                          value={g[campoStraordinario.chiave]}
                                                          onChange={(e) => aggiornaGiornata(
                                                            r.rapportoId,
                                                            i,
                                                            { [campoStraordinario.chiave]: Math.max(0, Number(e.target.value)) },
                                                          )}
                                                          onFocus={(e) => e.currentTarget.select()}
                                                        />
                                                      ) : g[campoStraordinario.chiave] > 0 ? (
                                                        <button className="mini-action" type="button"
                                                          title={`Rimuovi ${campoStraordinario.etichetta} incompatibile con ${g.causale}`}
                                                          onClick={() => aggiornaGiornata(r.rapportoId, i, {
                                                            [campoStraordinario.chiave]: 0,
                                                          })}>
                                                          Azzera
                                                        </button>
                                                      ) : <span className="muto">—</span>}
                                                    </td>
                                                  ))}
                                                </tr>
                                                {eventoSanitario && (
                                                  <tr className="presence-editor-row">
                                                    <td colSpan={7}>
                                                      <div className="presence-fields">
                                                        <label><span>Numero/protocollo evento</span>
                                                          <input value={g.identificativoEvento ?? ""}
                                                            onChange={(e) => aggiornaGiornata(
                                                              r.rapportoId, i,
                                                              { identificativoEvento: e.target.value })} />
                                                        </label>
                                                        <label><span>Data inizio evento</span>
                                                          <input type="date" max={g.data}
                                                            value={g.dataInizioEvento ?? ""}
                                                            onChange={(e) => aggiornaGiornata(
                                                              r.rapportoId, i,
                                                              { dataInizioEvento: e.target.value || null })} />
                                                        </label>
                                                        <label><span>RMG mese precedente €</span>
                                                          <input type="number" min={0.01} step={0.0001}
                                                            value={g.retribuzioneMediaGiornalieraPrecedente ?? ""}
                                                            onChange={(e) => aggiornaGiornata(
                                                              r.rapportoId, i,
                                                              { retribuzioneMediaGiornalieraPrecedente:
                                                                e.target.value === "" ? null : Number(e.target.value) })} />
                                                        </label>
                                                        <label><span>Ratei giornalieri €</span>
                                                          <input type="number" min={0} step={0.0001}
                                                            value={g.rateiGiornalieri ?? ""}
                                                            onChange={(e) => aggiornaGiornata(
                                                              r.rapportoId, i,
                                                              { rateiGiornalieri:
                                                                e.target.value === "" ? null : Number(e.target.value) })} />
                                                        </label>
                                                        <label><span>Coefficiente lordizzazione</span>
                                                          <input type="number" min={0.0001} step={0.0001}
                                                            value={g.coefficienteLordizzazione ?? ""}
                                                            onChange={(e) => aggiornaGiornata(
                                                              r.rapportoId, i,
                                                              { coefficienteLordizzazione:
                                                                e.target.value === "" ? null : Number(e.target.value) })} />
                                                        </label>
                                                      </div>
                                                    </td>
                                                  </tr>
                                                )}
                                                </Fragment>
                                              );
                                            })}
                                          </tbody>
                                        </table>
                                      </div>
                                      <DirittiFeriePermessi
                                        riga={r}
                                        giornate={calendari[r.rapportoId]}
                                      />
                                      {calendari[r.rapportoId].some((g) => g.causale === "Malattia") && (
                                        <div className="presence-fields" style={{ marginTop: 10 }}>
                                          {campo(r.rapportoId, "Evento di malattia nell'anno", "numeroEventoMalattiaAnno", "numero")}
                                        </div>
                                      )}
                                      <div className="actions-row" style={{ marginTop: 10 }}>
                                        <button
                                          className="btn secondary" type="button"
                                          disabled={verificando === r.rapportoId || problemiLocali.length > 0}
                                          onClick={() => { void verifica(r.rapportoId); }}
                                        >
                                          {verificando === r.rapportoId ? "Controllo..." : "Controlla il mese"}
                                        </button>
                                      </div>
                                      {problemiVisibili.length > 0 ? (
                                        <ul className="calendario-problemi">
                                          {problemiVisibili.map((p, i) => (
                                            <li key={`${p.messaggio}-${i}`} className={p.bloccante ? "bloccante" : undefined}>
                                              <span className={`status-pill ${p.bloccante ? "red" : "amber"}`}>
                                                {p.bloccante ? "Da correggere" : "Attenzione"}
                                              </span>{" "}
                                              {p.messaggio}
                                            </li>
                                          ))}
                                        </ul>
                                      ) : esitoServer !== undefined ? (
                                          <p><span className="status-pill green">Calendario in ordine</span></p>
                                      ) : (
                                        <p><span className="status-pill amber">Controllo del mese non ancora eseguito</span></p>
                                      )}
                                    </div>
                                  ) : (
                                    <div className="presence-section">
                                      <p><span className="status-pill red">Calendario mancante</span></p>
                                      <p className="muto">La compilazione a soli totali non può creare un cedolino archiviabile.</p>
                                      <button className="btn secondary" type="button"
                                        onClick={() => apriCalendario(r.rapportoId, r.oreGiornaliere)}>
                                        <CalendarDays size={16} /> Crea calendario lunedì-venerdì
                                      </button>
                                    </div>
                                  )}
                                  {mese === 7 && (
                                    <div className="presence-section">
                                      <h3>Quattordicesima del 1° luglio</h3>
                                      <p className="muto">
                                        Il prospetto ordinario non incorpora la quattordicesima: indica il
                                        documento separato e la sua data di pagamento.
                                      </p>
                                      <div className="presence-fields">
                                        <label><span>Riferimento prospetto separato</span>
                                          <input
                                            placeholder="es. 14A-2026-000123"
                                            value={cartellino.riferimentoQuattordicesimaSeparata ?? ""}
                                            onChange={(e) => aggiornaTesto(
                                              r.rapportoId,
                                              "riferimentoQuattordicesimaSeparata",
                                              e.target.value,
                                            )}
                                          />
                                        </label>
                                        <label><span>Data di pagamento</span>
                                          <input type="date" value={cartellino.dataPagamentoQuattordicesima ?? ""}
                                            onChange={(e) => aggiornaTesto(
                                              r.rapportoId,
                                              "dataPagamentoQuattordicesima",
                                              e.target.value,
                                            )} />
                                        </label>
                                      </div>
                                    </div>
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
                                    <label><input type="checkbox" checked={Boolean(confermato[r.rapportoId])}
                                      disabled={!meseControllato}
                                      onChange={(e) => setConfermato((c) => ({ ...c, [r.rapportoId]: e.target.checked }))} />
                                      <span>Ho controllato presenze, ferie, permessi, malattia, infortunio e assenze del mese.</span>
                                    </label>
                                    <button className="btn primary" type="button"
                                      disabled={!confermato[r.rapportoId]
                                        || !meseControllato
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
                </>
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
