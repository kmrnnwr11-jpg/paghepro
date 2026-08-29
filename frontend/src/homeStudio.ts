export type SectionKey =
  | "dashboard"
  | "aziende"
  | "azienda"
  | "pratiche"
  | "moduli"
  | "dipendenti"
  | "dipendente"
  | "presenze"
  | "cedolini"
  | "agricoltura"
  | "calcolo"
  | "adempimenti"
  | "scadenzario"
  | "posta"
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
  | "fine-anno"
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
  | "audit"
  | "retention"
  | "studi";

/**
 * Schermate che Aurora può aprire con l'azione [[APRI:…]] (consenso pieno): le pagine
 * operative, mai dettagli o amministrazione. Deve combaciare con la whitelist del server
 * (`AiChatEndpoints.SezioniApribili`).
 */
export const SEZIONI_APRIBILI_DA_AURORA: readonly SectionKey[] = [
  "dashboard", "aziende", "dipendenti", "presenze", "cedolini", "calcolo", "f24",
  "adempimenti", "scadenzario", "fatture", "pagamenti", "contabilita", "lipe",
  "iva-annuale", "bilancio", "documenti", "elaborazioni", "fine-anno", "cu", "mod770",
  "redditi-pf", "irap", "ires", "imu", "intrastat", "pratiche", "moduli", "firma",
  "posta", "agricoltura",
];

export function sezioneApribileDaAurora(sezione: string): SectionKey | null {
  return (SEZIONI_APRIBILI_DA_AURORA as readonly string[]).includes(sezione)
    ? (sezione as SectionKey)
    : null;
}

export interface StrumentoRecente {
  sezione: SectionKey;
  aziendaId: string;
  usatoAlle: number;
}

// Queste schermate lavorano sui dati di un cliente preciso. La shell non le
// monta finché il professionista non ha scelto esplicitamente l'azienda.
const SEZIONI_CON_AZIENDA_OBBLIGATORIA = new Set<SectionKey>([
  "azienda",
  "moduli", "dipendenti", "presenze", "cedolini", "agricoltura", "fatture", "lipe", "iva-annuale", "bilancio",
  "fine-anno", "cu", "mod770", "redditi-pf", "irap", "ires", "imu",
  "intrastat",
]);

export function sezioneRichiedeAzienda(sezione: SectionKey): boolean {
  return SEZIONI_CON_AZIENDA_OBBLIGATORIA.has(sezione);
}

export function aggiornaStrumentiRecenti(
  correnti: StrumentoRecente[],
  sezione: SectionKey,
  aziendaId: string,
  aziendeValide: ReadonlySet<string>,
  usatoAlle = Date.now(),
): StrumentoRecente[] {
  return [
    { sezione, aziendaId, usatoAlle },
    ...correnti.filter((voce) =>
      aziendeValide.has(voce.aziendaId)
      && (voce.sezione !== sezione || voce.aziendaId !== aziendaId)),
  ].slice(0, 4);
}
