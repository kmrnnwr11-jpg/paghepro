import type { DatiMensiliCedolinoAgricolo } from "./api";

export const CODICE_CCNL_AGRICOLTURA = "AGRICOLTURA_OPERAI";

export function eRapportoAgricolo(codiceCcnl: string): boolean {
  return codiceCcnl.trim().toUpperCase() === CODICE_CCNL_AGRICOLTURA;
}

/** OTI resta bloccato finche tabelle e casi golden non sono distinti da quelli OTD. */
export function eTipoContrattoOti(tipoContratto: string): boolean {
  const valore = tipoContratto.trim().toLowerCase();
  return valore === "oti" || valore.includes("indeterminat");
}

/** Controlli immediati della form; il backend ripete le stesse invarianti sui dati autorevoli. */
export function validaDatiCedolinoAgricolo(dati: DatiMensiliCedolinoAgricolo): string[] {
  const errori: string[] = [];
  if (!Number.isInteger(dati.anno) || dati.anno < 2000 || dati.anno > 2100
      || !Number.isInteger(dati.mese) || dati.mese < 1 || dati.mese > 12) {
    errori.push("Scegli un periodo valido.");
    return errori;
  }

  const giorniMese = new Date(Date.UTC(dati.anno, dati.mese, 0)).getUTCDate();
  if (!Number.isFinite(dati.oreOrdinarie) || dati.oreOrdinarie < 0)
    errori.push("Le ore lavorate non possono essere negative.");
  if (!Number.isInteger(dati.giornateLavorate)
      || dati.giornateLavorate < 0 || dati.giornateLavorate > giorniMese)
    errori.push(`Le giornate lavorate devono essere comprese tra 0 e ${giorniMese}.`);
  if (!Number.isInteger(dati.giorniDetrazione)
      || dati.giorniDetrazione < 0 || dati.giorniDetrazione > giorniMese)
    errori.push(`I giorni di detrazione devono essere compresi tra 0 e ${giorniMese}.`);
  if (!Number.isFinite(dati.unaTantum) || dati.unaTantum < 0)
    errori.push("Le altre competenze non possono essere negative.");
  if (!Number.isFinite(dati.detrazioniGiaGoduteAnno) || dati.detrazioniGiaGoduteAnno < 0)
    errori.push("Le detrazioni gia godute non possono essere negative.");
  if (!Number.isFinite(dati.arrotondamentoRiportato))
    errori.push("Il riporto arrotondamenti non e valido.");
  if (dati.redditoPresuntoAnnuo === null
      || !Number.isFinite(dati.redditoPresuntoAnnuo)
      || dati.redditoPresuntoAnnuo <= 0)
    errori.push("Indica il reddito annuo presunto reale del rapporto.");
  return errori;
}
