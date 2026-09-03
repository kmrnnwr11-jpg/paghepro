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
  if (!Number.isFinite(dati.giorniTrasferta)
      || dati.giorniTrasferta < 0 || dati.giorniTrasferta > giorniMese)
    errori.push(`I giorni di trasferta devono essere compresi tra 0 e ${giorniMese}.`);
  if (!Number.isFinite(dati.indennitaTrasfertaGiornaliera) || dati.indennitaTrasfertaGiornaliera < 0)
    errori.push("L'indennita di trasferta giornaliera non puo essere negativa.");
  if (!Number.isFinite(dati.rimborsiSpeseDocumentate) || dati.rimborsiSpeseDocumentate < 0)
    errori.push("I rimborsi spese documentati non possono essere negativi.");
  if (dati.trasferte && dati.trasferte.length > 0) {
    const invalido = dati.trasferte.some((t) =>
      !Number.isFinite(t.giorni) || t.giorni < 0
      || !Number.isFinite(t.importoGiorno) || t.importoGiorno < 0);
    if (invalido)
      errori.push("I gruppi di trasferta non possono avere giorni o importi negativi.");
    else if (dati.trasferte.reduce((somma, t) => somma + t.giorni, 0) > giorniMese)
      errori.push(`I giorni di trasferta dei gruppi superano i ${giorniMese} giorni del mese.`);
  }
  if (!Number.isFinite(dati.detrazioniGiaGoduteAnno) || dati.detrazioniGiaGoduteAnno < 0)
    errori.push("Le detrazioni gia godute non possono essere negative.");
  if (!Number.isFinite(dati.arrotondamentoRiportato))
    errori.push("Il riporto arrotondamenti non e valido.");
  // Il reddito presunto può restare vuoto: il server lo prende dalla scheda del rapporto,
  // dal default aziendale o lo stima dal contratto (tracciandone l'origine). Se però viene
  // digitato, deve essere un numero positivo.
  if (dati.redditoPresuntoAnnuo !== null
      && (!Number.isFinite(dati.redditoPresuntoAnnuo) || dati.redditoPresuntoAnnuo <= 0))
    errori.push("Il reddito annuo presunto, se indicato, deve essere un importo positivo.");
  return errori;
}

/** Settimana agricola standard: 39 ore su 6 giorni (CCNL operai agricoli e florovivaisti). */
export const GIORNI_SETTIMANA_AGRICOLA = 6;

/** Ore al giorno dal contratto: ore settimanali ÷ giorni della settimana agricola. */
export function oreGiornaliereDaContratto(oreSettimanali: number | undefined, giorniSettimana = GIORNI_SETTIMANA_AGRICOLA): number | null {
  if (!oreSettimanali || !Number.isFinite(oreSettimanali) || oreSettimanali <= 0) return null;
  return Math.round((oreSettimanali / giorniSettimana) * 100) / 100;
}

/** Ore del mese proposte: giornate lavorate × ore al giorno. Restano modificabili dall'utente. */
export function oreDaGiornate(giornate: number, oreGiorno: number): number | null {
  if (!Number.isFinite(giornate) || !Number.isFinite(oreGiorno) || giornate < 0 || oreGiorno <= 0) return null;
  return Math.round(giornate * oreGiorno * 100) / 100;
}

function dataItaliana(valore?: string | null): Date | null {
  if (!valore) return null;
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(valore.trim());
  if (m) return new Date(Date.UTC(Number(m[3]), Number(m[2]) - 1, Number(m[1])));
  const iso = /^(\d{4})-(\d{2})-(\d{2})/.exec(valore.trim());
  return iso ? new Date(Date.UTC(Number(iso[1]), Number(iso[2]) - 1, Number(iso[3]))) : null;
}

/** Giorni di calendario del mese coperti dal rapporto (dall'assunzione alla cessazione). */
export function giorniDetrazioneNelMese(anno: number, mese: number, dataAssunzione?: string | null, dataCessazione?: string | null): number | null {
  if (!Number.isInteger(anno) || !Number.isInteger(mese) || mese < 1 || mese > 12) return null;
  const inizioMese = Date.UTC(anno, mese - 1, 1);
  const fineMese = Date.UTC(anno, mese, 0);
  const assunzione = dataItaliana(dataAssunzione)?.getTime() ?? inizioMese;
  const cessazione = dataItaliana(dataCessazione)?.getTime() ?? fineMese;
  const da = Math.max(inizioMese, assunzione);
  const a = Math.min(fineMese, cessazione);
  if (a < da) return 0;
  return Math.round((a - da) / 86_400_000) + 1;
}

/**
 * Riassunto leggibile dei valori compilati nella pagina agricola, pubblicato come contesto
 * di schermata per Aurora (dato non fidato lato server). Solo i campi compilati entrano
 * nel testo: niente zeri di default, niente valori inventati.
 */
export function riassuntoPaginaAgricola(dati: {
  lavoratore?: string | null;
  anno: string;
  mese: string;
  giornate: string;
  oreMese: string;
  oreGiorno: string;
  redditoPresuntoAnnuo: string;
  trasferte: { giorni: string; importo: string }[];
  giorniTrasferta?: string;
  indennitaTrasferta?: string;
  rimborsiDocumentati?: string;
  netto?: number | null;
}): string {
  const num = (valore?: string) => Number((valore ?? "").replace(",", "."));
  const parti: string[] = [];
  if (dati.lavoratore?.trim()) parti.push(`lavoratore ${dati.lavoratore.trim()}`);
  parti.push(`periodo ${dati.mese}/${dati.anno}`);
  if (dati.giornate.trim()) parti.push(`giornate lavorate ${dati.giornate}`);
  if (dati.oreMese.trim()) parti.push(`ore del mese ${dati.oreMese}`);
  if (dati.oreGiorno.trim()) parti.push(`ore al giorno ${dati.oreGiorno}`);
  // Il reddito presunto è obbligatorio per il calcolo: se manca lo si dichiara mancante,
  // così Aurora non lo dà per compilato.
  parti.push(dati.redditoPresuntoAnnuo.trim()
    ? `reddito annuo presunto ${dati.redditoPresuntoAnnuo} EUR`
    : "reddito annuo presunto non ancora indicato");
  const gruppi = dati.trasferte.filter((g) => num(g.giorni) > 0 && num(g.importo) > 0);
  if (gruppi.length > 0)
    parti.push(`trasferte ${gruppi.map((g) => `${g.giorni} giorni a ${g.importo} EUR`).join(" + ")}`);
  else if (num(dati.giorniTrasferta) > 0 && num(dati.indennitaTrasferta) > 0)
    parti.push(`trasferta ${dati.giorniTrasferta} giorni a ${dati.indennitaTrasferta} EUR`);
  if (num(dati.rimborsiDocumentati) > 0)
    parti.push(`rimborsi spese documentati ${dati.rimborsiDocumentati} EUR`);
  if (dati.netto != null) parti.push(`netto calcolato in anteprima ${dati.netto.toFixed(2)} EUR`);
  return `${parti.join("; ")}.`;
}
