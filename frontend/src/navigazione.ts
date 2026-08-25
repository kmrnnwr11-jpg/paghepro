import type { SectionKey } from "./homeStudio";

/**
 * Una sola navigazione per tutta l'app: cinque mondi al posto di un elenco
 * piatto di trentaquattro voci. Il modulo è puro (nessun React, nessun DOM)
 * così la copertura e la ricerca sono verificabili dai test.
 */

export type ChiaveMondo = "paghe" | "fisco" | "clienti" | "documenti" | "studio";

export interface VoceMondo {
  key: SectionKey;
  /** Nome nella lingua del consulente, non il nome tecnico della schermata. */
  label: string;
  /** Riga di spiegazione: chi non conosce la sigla capisce lo stesso. */
  detail: string;
  /** Parole con cui la persona potrebbe cercarla. */
  sinonimi?: string[];
  /** Riservata all'amministratore dello studio o al super-admin. */
  riservata?: "admin" | "superadmin";
}

export interface Mondo {
  chiave: ChiaveMondo;
  titolo: string;
  voci: VoceMondo[];
}

export const MONDI: Mondo[] = [
  {
    chiave: "paghe",
    titolo: "Paghe",
    voci: [
      { key: "presenze", label: "Presenze", detail: "Cartellino del mese: ferie, malattia, straordinari", sinonimi: ["cartellino", "ferie", "malattia", "straordinari"] },
      { key: "cedolini", label: "Buste paga", detail: "Archivio dei cedolini e PDF da consegnare", sinonimi: ["cedolini", "busta", "paga", "pdf"] },
      { key: "agricoltura", label: "Agricoltura OTD", detail: "Anteprima OTD; OTI bloccato finche non e validato", sinonimi: ["agricola", "agricoltura", "otd", "oti", "bracciante", "cpl", "posagri"] },
      { key: "elaborazioni", label: "Elabora il mese", detail: "Calcolo dei cedolini di tutti i dipendenti", sinonimi: ["calcolo", "elaborazione"] },
      { key: "dipendenti", label: "Dipendenti", detail: "Anagrafiche, assunzioni e cessazioni", sinonimi: ["lavoratori", "assunzione", "unilav"] },
      { key: "fine-anno", label: "Fine anno", detail: "Conguagli, TFR e autoliquidazione INAIL", sinonimi: ["conguaglio", "tfr", "inail"] },
      { key: "cu", label: "Certificazione Unica", detail: "CU da consegnare al lavoratore", sinonimi: ["cud", "cu"] },
    ],
  },
  {
    chiave: "fisco",
    titolo: "Fisco",
    voci: [
      { key: "f24", label: "F24", detail: "Versamenti del mese da cedolini e scadenze", sinonimi: ["versamento", "delega", "tributi"] },
      { key: "adempimenti", label: "Adempimenti", detail: "UniEmens e invii telematici", sinonimi: ["uniemens", "inps", "telematico"] },
      { key: "lipe", label: "LIPE / IVA", detail: "Liquidazione periodica IVA", sinonimi: ["iva", "liquidazione", "trimestre"] },
      { key: "iva-annuale", label: "IVA annuale", detail: "Dichiarazione IVA dell'anno" },
      { key: "mod770", label: "Modello 770", detail: "Dichiarazione del sostituto d'imposta", sinonimi: ["770", "sostituto"] },
      { key: "redditi-pf", label: "Redditi PF / 730", detail: "Dichiarazione delle persone fisiche", sinonimi: ["730", "unico", "persone fisiche"] },
      { key: "irap", label: "IRAP", detail: "Imposta regionale sulle attività produttive" },
      { key: "ires", label: "IRES società", detail: "Redditi delle società di capitali" },
      { key: "imu", label: "IMU", detail: "Imposta sugli immobili", sinonimi: ["immobili", "casa"] },
      { key: "intrastat", label: "Intrastat", detail: "Cessioni e acquisti intra-UE", sinonimi: ["intra", "ue", "estero"] },
    ],
  },
  {
    chiave: "clienti",
    titolo: "Clienti",
    voci: [
      { key: "aziende", label: "Aziende", detail: "Archivio clienti e nuova azienda da visura", sinonimi: ["cliente", "ditta", "visura", "anagrafica"] },
      { key: "pratiche", label: "Pratiche ComUnica", detail: "Apertura, variazione e cessazione P.IVA", sinonimi: ["comunica", "partita iva", "camera di commercio"] },
      { key: "moduli", label: "Moduli ufficiali", detail: "AA7/10 compilato sul modello dell'Agenzia", sinonimi: ["aa7", "aa9", "agenzia entrate"] },
      { key: "pagamenti", label: "Pagamenti clienti", detail: "Avvisi di pagamento e solleciti", sinonimi: ["incassi", "solleciti", "parcelle"] },
      { key: "posta", label: "Posta", detail: "Messaggi ed email verso i clienti", sinonimi: ["email", "pec", "messaggi"] },
      { key: "scadenzario", label: "Scadenze", detail: "DURC e versamenti in arrivo", sinonimi: ["durc", "promemoria", "scadenzario"] },
    ],
  },
  {
    chiave: "documenti",
    titolo: "Documenti",
    voci: [
      { key: "documenti", label: "Archivio documenti", detail: "Carica e ritrova PDF, foto e ricevute", sinonimi: ["file", "pdf", "carica", "inbox"] },
      { key: "fatture", label: "Fatturazione", detail: "Fatture elettroniche e invio allo SdI", sinonimi: ["fattura", "sdi", "fatturapa", "elettronica"] },
      { key: "firma", label: "Firma digitale", detail: "Firma CAdES/XAdES con PFX o smart card", sinonimi: ["cades", "xades", "cns", "p7m"] },
      { key: "contabilita", label: "Contabilità", detail: "Registri, spese e prima nota", sinonimi: ["registri", "spese", "prima nota"] },
      { key: "bilancio", label: "Bilancio", detail: "Stato patrimoniale e conto economico", sinonimi: ["ce", "sp", "esercizio"] },
    ],
  },
  {
    chiave: "studio",
    titolo: "Studio",
    voci: [
      { key: "impostazioni", label: "Impostazioni", detail: "Preferenze dello studio" },
      { key: "collega-sdi", label: "Collega Fatturazione", detail: "Credenziali del canale SdI" },
      { key: "utenti", label: "Utenti e permessi", detail: "Chi può fare cosa nello studio" },
      { key: "audit", label: "Registro attività", detail: "Chi ha letto o modificato cosa", riservata: "admin" },
      { key: "retention", label: "Conservazione dati", detail: "Cancellazione e retention GDPR", riservata: "admin" },
      { key: "studi", label: "Studi", detail: "Gestione piattaforma", riservata: "superadmin" },
    ],
  },
];

/** Schermate di dettaglio: aprono un mondo pur non essendo voci di menu. */
const MONDO_DI_DETTAGLIO: Partial<Record<SectionKey, ChiaveMondo>> = {
  dipendente: "paghe",
  calcolo: "paghe",
  azienda: "clienti",
};

const INDICE_MONDI = new Map<SectionKey, ChiaveMondo>([
  ...MONDI.flatMap((mondo) => mondo.voci.map((voce) => [voce.key, mondo.chiave] as const)),
  ...Object.entries(MONDO_DI_DETTAGLIO).map(([key, mondo]) => [key as SectionKey, mondo!] as const),
]);

/** Il mondo da evidenziare nella barra per la schermata aperta. */
export function mondoDiSezione(sezione: SectionKey): ChiaveMondo | null {
  return INDICE_MONDI.get(sezione) ?? null;
}

/** Tutte le voci, appiattite: serve alla ricerca e ai controlli di copertura. */
export function tutteLeVoci(): VoceMondo[] {
  return MONDI.flatMap((mondo) => mondo.voci);
}

/** Le voci visibili a chi ha questo ruolo. */
export function vociPerRuolo(mondo: Mondo, ruolo: string | null): VoceMondo[] {
  return mondo.voci.filter((voce) => {
    if (!voce.riservata) return true;
    if (voce.riservata === "superadmin") return ruolo === "superadmin";
    // La conservazione dati è responsabilità dell'admin dello studio: il backend
    // respinge anche il super-admin di piattaforma.
    if (voce.key === "retention") return ruolo === "admin";
    return ruolo === "admin" || ruolo === "superadmin";
  });
}

function normalizza(testo: string): string {
  return testo
    .toLocaleLowerCase("it")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "");
}

export interface RisultatoRicerca extends VoceMondo {
  mondo: ChiaveMondo;
  titoloMondo: string;
}

/**
 * Ricerca su nome, spiegazione e sinonimi. Ordina prima chi ha il nome che
 * inizia col testo cercato, poi chi lo contiene: chi scrive "ced" trova
 * "Cedolini" in cima e non una voce che lo cita di sfuggita.
 */
export function cercaVoci(testo: string, ruolo: string | null = null): RisultatoRicerca[] {
  const q = normalizza(testo.trim());
  const tutte = MONDI.flatMap((mondo) =>
    vociPerRuolo(mondo, ruolo).map((voce) => ({ ...voce, mondo: mondo.chiave, titoloMondo: mondo.titolo })));
  if (!q) return tutte;

  const punteggio = (voce: RisultatoRicerca): number => {
    const nome = normalizza(voce.label);
    if (nome.startsWith(q)) return 0;
    if (nome.includes(q)) return 1;
    if ((voce.sinonimi ?? []).some((s) => normalizza(s).startsWith(q))) return 2;
    if ((voce.sinonimi ?? []).some((s) => normalizza(s).includes(q))) return 3;
    if (normalizza(voce.detail).includes(q)) return 4;
    return Number.POSITIVE_INFINITY;
  };

  return tutte
    .map((voce) => ({ voce, p: punteggio(voce) }))
    .filter((r) => Number.isFinite(r.p))
    .sort((a, b) => a.p - b.p || a.voce.label.localeCompare(b.voce.label, "it"))
    .map((r) => r.voce);
}
