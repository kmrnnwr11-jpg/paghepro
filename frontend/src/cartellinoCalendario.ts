import type { CausaleGiornata, GiornataCartellino, ProblemaCartellino } from "./api";

type CampoOrePrincipale =
  | "oreOrdinarie"
  | "orePermessi"
  | "oreExFestivita"
  | "oreAssenzaNonRetribuita";

const CAMPI_ORE_PRINCIPALI: CampoOrePrincipale[] = [
  "oreOrdinarie",
  "orePermessi",
  "oreExFestivita",
  "oreAssenzaNonRetribuita",
];

const CAMPI_STRAORDINARIO = [
  "oreStraordinario",
  "oreStraordinarioNotturno",
  "oreStraordinarioFestivo",
] as const;

/** Il solo campo ore compatibile con la causale principale della giornata. */
export function campoOre(causale: CausaleGiornata): CampoOrePrincipale | null {
  switch (causale) {
    case "Lavorata": return "oreOrdinarie";
    case "PermessoRetribuito": return "orePermessi";
    case "ExFestivita": return "oreExFestivita";
    case "AssenzaNonRetribuita": return "oreAssenzaNonRetribuita";
    default: return null;
  }
}

/**
 * Ferie, ROL, ex festivita, malattia, infortunio e assenza non possono contenere
 * anche straordinario. Il lavoro in riposo o festivita resta invece rappresentabile.
 */
export function causaleConsenteStraordinario(causale: CausaleGiornata): boolean {
  return causale === "Lavorata"
    || causale === "RiposoSettimanale"
    || causale === "Festivita";
}

export function giornataVuota(
  data: string,
  causale: CausaleGiornata,
  ore: number,
): GiornataCartellino {
  const giornata: GiornataCartellino = {
    data,
    causale,
    oreOrdinarie: 0,
    orePermessi: 0,
    oreExFestivita: 0,
    oreAssenzaNonRetribuita: 0,
    oreStraordinario: 0,
    oreStraordinarioNotturno: 0,
    oreStraordinarioFestivo: 0,
  };
  const campo = campoOre(causale);
  const oreValide = Number.isFinite(ore) ? Math.max(0, ore) : 0;
  return campo ? { ...giornata, [campo]: oreValide } : giornata;
}

/**
 * Pasqua col metodo di Gauss/Meeus (calendario gregoriano): serve solo per il lunedì
 * dell'Angelo, l'unica festività nazionale mobile.
 */
function pasqua(anno: number): Date {
  const a = anno % 19;
  const b = Math.floor(anno / 100);
  const c = anno % 100;
  const d = Math.floor(b / 4);
  const e = b % 4;
  const f = Math.floor((b + 8) / 25);
  const g = Math.floor((b - f + 1) / 3);
  const h = (19 * a + b - d - g + 15) % 30;
  const i = Math.floor(c / 4);
  const k = c % 4;
  const l = (32 + 2 * e + 2 * i - h - k) % 7;
  const m = Math.floor((a + 11 * h + 22 * l) / 451);
  const mese = Math.floor((h + l - 7 * m + 114) / 31);
  const giorno = ((h + l - 7 * m + 114) % 31) + 1;
  return new Date(anno, mese - 1, giorno);
}

const FESTIVITA_FISSE: readonly [number, number, string][] = [
  [1, 1, "Capodanno"],
  [1, 6, "Epifania"],
  [4, 25, "Liberazione"],
  [5, 1, "Festa del Lavoro"],
  [6, 2, "Festa della Repubblica"],
  [8, 15, "Ferragosto"],
  [11, 1, "Ognissanti"],
  [12, 8, "Immacolata"],
  [12, 25, "Natale"],
  [12, 26, "Santo Stefano"],
];

/**
 * Festività nazionali dell'anno (data ISO → nome): le dieci a data fissa più il lunedì
 * dell'Angelo. Il santo patrono dipende dal comune e non viene dedotto: si aggiunge a
 * mano sulla giornata.
 */
export function festivitaNazionali(anno: number): Map<string, string> {
  const iso = (mese: number, giorno: number) =>
    `${anno}-${String(mese).padStart(2, "0")}-${String(giorno).padStart(2, "0")}`;
  const feste = new Map<string, string>(
    FESTIVITA_FISSE.map(([mese, giorno, nome]) => [iso(mese, giorno), nome]),
  );
  const pasquetta = pasqua(anno);
  pasquetta.setDate(pasquetta.getDate() + 1);
  feste.set(iso(pasquetta.getMonth() + 1, pasquetta.getDate()), "Lunedì dell'Angelo");
  return feste;
}

/**
 * Mese standard su cinque giorni: lunedi-venerdi lavorati, sabato e domenica a riposo.
 * Le festività nazionali che cadono in un giorno lavorativo nascono già come Festivita:
 * l'operatore corregge solo le eccezioni, non ricopia il calendario civile.
 */
export function calendarioIniziale(
  anno: number,
  mese: number,
  oreGiorno: number,
): GiornataCartellino[] {
  const giorni = new Date(anno, mese, 0).getDate();
  const feste = festivitaNazionali(anno);
  const calendario = Array.from({ length: giorni }, (_, indice) => {
    const giorno = indice + 1;
    const data = new Date(anno, mese - 1, giorno);
    const iso = `${anno}-${String(mese).padStart(2, "0")}-${String(giorno).padStart(2, "0")}`;
    const fineSettimana = data.getDay() === 0 || data.getDay() === 6;
    if (fineSettimana) return giornataVuota(iso, "RiposoSettimanale", 0);
    return feste.has(iso)
      ? giornataVuota(iso, "Festivita", 0)
      : giornataVuota(iso, "Lavorata", oreGiorno);
  });
  // Se il mese non comincia di lunedì il riporto della prima settimana non è conoscibile
  // dal solo mese: resta nullo finché l'operatore inserisce le ore del mese precedente.
  const primo = new Date(anno, mese - 1, 1);
  return applicaProgressiviSettimanali(
    calendario,
    primo.getDay() === 1 ? 0 : null,
  );
}

function chiaveSettimana(dataIso: string): string {
  const data = new Date(`${dataIso}T00:00:00`);
  const giorno = data.getDay() === 0 ? 7 : data.getDay();
  data.setDate(data.getDate() - giorno + 1);
  return `${data.getFullYear()}-${data.getMonth() + 1}-${data.getDate()}`;
}

/**
 * Ricalcola il progressivo lunedì-domenica. Il riporto è richiesto solo per la prima
 * settimana quando essa è iniziata nel mese precedente; `null` mantiene il blocco.
 */
export function applicaProgressiviSettimanali(
  giornate: GiornataCartellino[],
  riportoPrimaSettimana: number | null,
): GiornataCartellino[] {
  let chiavePrecedente: string | null = null;
  let progressivo: number | null = null;
  let primaSettimana = true;

  return giornate.map((giornata) => {
    const chiave = chiaveSettimana(giornata.data);
    if (chiave !== chiavePrecedente) {
      progressivo = primaSettimana ? riportoPrimaSettimana : 0;
      chiavePrecedente = chiave;
      primaSettimana = false;
    }
    const ore = giornata.oreOrdinarie
      + giornata.oreStraordinario
      + giornata.oreStraordinarioNotturno
      + giornata.oreStraordinarioFestivo;
    if (progressivo !== null) progressivo += ore;
    return { ...giornata, progressivoOreSettimanali: progressivo };
  });
}

/** Applica una nuova causale rimuovendo ogni valore incompatibile rimasto nella riga. */
export function cambiaCausaleGiornata(
  precedente: GiornataCartellino,
  causale: CausaleGiornata,
  oreGiorno: number,
): GiornataCartellino {
  const campoPrecedente = campoOre(precedente.causale);
  const ore = campoOre(causale)
    ? (campoPrecedente ? Number(precedente[campoPrecedente]) : oreGiorno)
    : 0;
  const nuova = giornataVuota(precedente.data, causale, ore);

  if (!causaleConsenteStraordinario(causale)) return nuova;
  return {
    ...nuova,
    oreStraordinario: precedente.oreStraordinario,
    oreStraordinarioNotturno: precedente.oreStraordinarioNotturno,
    oreStraordinarioFestivo: precedente.oreStraordinarioFestivo,
  };
}

function dateAttese(anno: number, mese: number): string[] {
  if (!Number.isInteger(anno) || !Number.isInteger(mese) || mese < 1 || mese > 12) return [];
  const giorni = new Date(anno, mese, 0).getDate();
  return Array.from({ length: giorni }, (_, indice) =>
    `${anno}-${String(mese).padStart(2, "0")}-${String(indice + 1).padStart(2, "0")}`);
}

/**
 * Controlli client fail-closed prima dell'archiviazione. Il server resta la fonte
 * autoritativa, ma dal browser non deve partire un cartellino vuoto o sovrapposto.
 */
export function validaCalendarioArchiviabile(
  anno: number,
  mese: number,
  giornate?: GiornataCartellino[],
): ProblemaCartellino[] {
  const problemi: ProblemaCartellino[] = [];
  const attese = dateAttese(anno, mese);
  if (attese.length === 0) {
    return [{ messaggio: "Periodo del calendario non valido.", bloccante: true }];
  }
  if (!giornate?.length) {
    return [{
      messaggio: "Completa il calendario giorno per giorno prima di elaborare il cedolino.",
      bloccante: true,
    }];
  }

  const conteggioDate = new Map<string, number>();
  for (const giornata of giornate) {
    conteggioDate.set(giornata.data, (conteggioDate.get(giornata.data) ?? 0) + 1);
  }
  const mancanti = attese.filter((data) => !conteggioDate.has(data));
  const duplicate = [...conteggioDate.entries()]
    .filter(([, quantita]) => quantita > 1)
    .map(([data]) => data);
  const fuoriMese = [...conteggioDate.keys()].filter((data) => !attese.includes(data));
  if (giornate.length !== attese.length || mancanti.length > 0 || duplicate.length > 0 || fuoriMese.length > 0) {
    problemi.push({
      messaggio: "Il calendario deve contenere una sola riga per ogni giorno del mese selezionato.",
      bloccante: true,
    });
  }

  for (const giornata of giornate) {
    const tuttiICampi = [...CAMPI_ORE_PRINCIPALI, ...CAMPI_STRAORDINARIO];
    if (tuttiICampi.some((campo) => !Number.isFinite(giornata[campo]) || giornata[campo] < 0)) {
      problemi.push({
        messaggio: `${giornata.data}: le ore devono essere numeri maggiori o uguali a zero.`,
        bloccante: true,
      });
      continue;
    }

    const campoConsentito = campoOre(giornata.causale);
    const campiIncompatibili = CAMPI_ORE_PRINCIPALI.filter((campo) => campo !== campoConsentito);
    if (campiIncompatibili.some((campo) => giornata[campo] > 0)) {
      problemi.push({
        messaggio: `${giornata.data}: la causale ${giornata.causale} si sovrappone ad altre ore della giornata.`,
        bloccante: true,
      });
    }
    if (campoConsentito && giornata[campoConsentito] <= 0) {
      problemi.push({
        messaggio: `${giornata.data}: indica le ore della causale ${giornata.causale}.`,
        bloccante: true,
      });
    }

    const haStraordinario = CAMPI_STRAORDINARIO.some((campo) => giornata[campo] > 0);
    if (haStraordinario && !causaleConsenteStraordinario(giornata.causale)) {
      problemi.push({
        messaggio: `${giornata.data}: lo straordinario non puo sovrapporsi alla causale ${giornata.causale}.`,
        bloccante: true,
      });
    }

    if (giornata.progressivoOreSettimanali == null
      || !Number.isFinite(giornata.progressivoOreSettimanali)
      || giornata.progressivoOreSettimanali < 0) {
      problemi.push({
        messaggio: `${giornata.data}: manca il progressivo verificabile delle ore settimanali.`,
        bloccante: true,
      });
    }

    const eventoSanitario = giornata.causale === "Malattia"
      || giornata.causale === "Infortunio";
    if (eventoSanitario) {
      const datiCompleti = Boolean(giornata.identificativoEvento?.trim())
        && Boolean(giornata.dataInizioEvento)
        && Number.isFinite(giornata.retribuzioneMediaGiornalieraPrecedente)
        && Number(giornata.retribuzioneMediaGiornalieraPrecedente) > 0
        && Number.isFinite(giornata.rateiGiornalieri)
        && Number(giornata.rateiGiornalieri) >= 0
        && Number.isFinite(giornata.coefficienteLordizzazione)
        && Number(giornata.coefficienteLordizzazione) > 0;
      if (!datiCompleti) {
        problemi.push({
          messaggio: `${giornata.data}: completa protocollo, data iniziale, RMG precedente, ratei e lordizzazione dell'evento.`,
          bloccante: true,
        });
      }
    }
  }

  return problemi;
}
