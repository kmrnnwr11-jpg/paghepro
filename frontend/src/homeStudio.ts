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

export interface StrumentoRecente {
  sezione: SectionKey;
  aziendaId: string;
  usatoAlle: number;
}

// Queste schermate lavorano sui dati di un cliente preciso. La shell non le
// monta finché il professionista non ha scelto esplicitamente l'azienda.
const SEZIONI_CON_AZIENDA_OBBLIGATORIA = new Set<SectionKey>([
  "azienda",
  "moduli", "dipendenti", "presenze", "cedolini", "fatture", "lipe", "iva-annuale", "bilancio",
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
