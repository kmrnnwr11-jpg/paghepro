import type { DatiVisura, NuovaAzienda } from "./api";

export type ErroriAzienda = Partial<Record<keyof NuovaAzienda, string>>;

export interface OpzioniValidazioneAzienda {
  datiContributiviProvvisori?: boolean;
}

const VALORI_DISPARI_CODICE_FISCALE = [
  1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18,
  20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23,
];

function indiceCarattereFiscale(carattere: string): number {
  return /\d/.test(carattere)
    ? Number(carattere)
    : carattere.charCodeAt(0) - "A".charCodeAt(0);
}

/** Controllo formale P.IVA: check digit mod 10 e codice ufficio ammesso. */
export function partitaIvaValida(partitaIva?: string | null): boolean {
  const valore = partitaIva?.trim() ?? "";
  if (!/^\d{11}$/.test(valore)) return false;

  let somma = 0;
  for (let indice = 0; indice < 10; indice += 1) {
    const cifra = Number(valore[indice]);
    if (indice % 2 === 0) somma += cifra;
    else {
      const doppio = cifra * 2;
      somma += doppio > 9 ? doppio - 9 : doppio;
    }
  }
  const controllo = (10 - (somma % 10)) % 10;
  if (controllo !== Number(valore[10])) return false;

  const ufficio = Number(valore.slice(7, 10));
  return (ufficio >= 1 && ufficio <= 100)
    || ufficio === 120
    || ufficio === 121
    || ufficio === 888
    || ufficio === 999;
}

/** Controllo del carattere finale per CF personale o del check digit per CF numerico. */
export function codiceFiscaleValido(codiceFiscale?: string | null): boolean {
  const valore = codiceFiscale?.trim().toUpperCase() ?? "";
  if (/^\d{11}$/.test(valore)) return partitaIvaValida(valore);
  if (!/^[A-Z0-9]{16}$/.test(valore)) return false;

  let somma = 0;
  for (let indice = 0; indice < 15; indice += 1) {
    const posizioneDispari = indice % 2 === 0;
    const valoreCarattere = indiceCarattereFiscale(valore[indice]);
    somma += posizioneDispari
      ? VALORI_DISPARI_CODICE_FISCALE[valoreCarattere]
      : valoreCarattere;
  }
  return valore[15] === String.fromCharCode("A".charCodeAt(0) + (somma % 26));
}

function profiloDaAteco(codiceAteco?: string | null): Partial<NuovaAzienda> {
  const codice = codiceAteco?.trim();
  if (!codice?.startsWith("01.")) return {};
  return {
    settore: "AGRICOLTURA",
  };
}

function tipoContribuenteDaRagioneSociale(ragioneSociale?: string | null): string | undefined {
  return /\bS\.?R\.?L\.?\b/i.test(ragioneSociale ?? "") ? "Societa" : undefined;
}

function scomponiNominativo(nominativo?: string | null): Pick<Partial<NuovaAzienda>, "legaleRappresentanteNome" | "legaleRappresentanteCognome"> {
  const parti = nominativo?.replace(/\s+/g, " ").trim().split(" ").filter(Boolean) ?? [];
  if (parti.length < 2) return {};
  return {
    legaleRappresentanteCognome: parti.slice(0, -1).join(" "),
    legaleRappresentanteNome: parti.at(-1),
  };
}

function valoreNonVuoto(valore?: string | null): string | undefined {
  const pulito = valore?.trim();
  return pulito ? pulito : undefined;
}

/**
 * Estrazione deterministica dal testo incollato. Non inventa identificativi ufficiali:
 * i campi INPS/INAIL mancanti restano vuoti e devono essere completati dall'operatore.
 */
export function parsaTestoAzienda(testo: string): Partial<NuovaAzienda> {
  const r: Partial<NuovaAzienda> = {};
  const pulito = testo.replace(/\r/g, "");

  const nome = pulito.match(/(?:^|\n)\s*(?:Denominazione|Ragione Sociale|Intestazione)\s*:\s*([^\n]+)/i);
  if (nome) r.ragioneSociale = nome[1].trim();

  const codiceCombinato = pulito.match(/Codice\s+Fiscale\s*\/\s*Partita\s+IVA\s*:\s*(\d{11})/i);
  if (codiceCombinato) {
    r.codiceFiscale = codiceCombinato[1];
    r.partitaIva = codiceCombinato[1];
  } else {
    const piva = pulito.match(/Partita\s*IVA\s*[:/]?\s*(\d{11})/i);
    if (piva) r.partitaIva = piva[1];
    const cf = pulito.match(/Codice\s+Fiscale\s*[:/]?\s*([A-Z0-9]{11,16})/i);
    if (cf) r.codiceFiscale = cf[1].toUpperCase();
  }

  const sede = pulito.match(/(?:^|\n)\s*Sede\s+Legale\s*[:/]?\s*([^\n]+)/i)?.[1]?.trim();
  if (sede) {
    const completa = sede.match(/^(.+?)[,\s]+(?:CAP\s*)?(\d{5})[,\s]+([^,(]+?)\s*\(([A-Z]{2})\)/i);
    if (completa) {
      r.indirizzo = completa[1].trim().replace(/,$/, "");
      r.cap = completa[2];
      r.comune = completa[3].trim();
      r.provincia = completa[4].toUpperCase();
    } else {
      r.indirizzo = sede;
      const cap = sede.match(/\b(\d{5})\b/);
      const provincia = sede.match(/\(([A-Z]{2})\)/i);
      if (cap) r.cap = cap[1];
      if (provincia) r.provincia = provincia[1].toUpperCase();
    }
  }

  const pec = pulito.match(/(?:^|\n)\s*PEC\s*[:/]?\s*([\w.+-]+@[\w.-]+\.[a-z]{2,})/i);
  if (pec) r.pec = pec[1].toLowerCase();
  const email = pulito.match(/(?:^|\n)\s*(?:Email|E-mail)\s*[:/]?\s*([\w.+-]+@[\w.-]+\.[a-z]{2,})/i);
  if (email) r.email = email[1].toLowerCase();
  const telefono = pulito.match(/(?:^|\n)\s*(?:Tel(?:efono)?|Cell(?:ulare)?|Mobile)\s*[:/]?\s*(\+?[\d\s\-()]{8,})/i);
  if (telefono) r.telefono = telefono[1].replace(/\s+/g, "").trim();

  const ateco = pulito.match(/Codice\s+ATECO\s*:\s*(\d{2}(?:\.\d{2}){1,2})/i);
  if (ateco) r.codiceAteco = ateco[1];

  r.tipoContribuente = tipoContribuenteDaRagioneSociale(r.ragioneSociale);
  Object.assign(r, profiloDaAteco(r.codiceAteco));

  const rigaAmministratore = pulito.match(/(?:^|\n)\s*Amministratore\s+Delegato\s*:\s*([^\n]+)/i)?.[1];
  const nominativo = rigaAmministratore?.match(/^([A-ZÀ-ÖØ-Ý' -]+?)(?:\s*\(|,|$)/i)?.[1];
  Object.assign(r, scomponiNominativo(nominativo));
  const cfAmministratore = rigaAmministratore?.match(/C\.?\s*F\.?\s*:?\s*([A-Z0-9]{16})/i)?.[1];
  if (cfAmministratore) r.legaleRappresentanteCodiceFiscale = cfAmministratore.toUpperCase();

  if (/ancora\s+alle\s+proprie\s+dipendenze|data\s+di\s+assunzione|dati\s+del\s+lavoratore/i.test(pulito)) {
    r.haDipendenti = true;
  }

  return r;
}

/** Applica tutti i campi effettivamente restituiti dal lettore PDF, inclusi quelli prima ignorati. */
export function applicaDatiVisura(form: NuovaAzienda, dati: DatiVisura): NuovaAzienda {
  const profilo = profiloDaAteco(dati.codiceAteco);
  const nominativoRappresentante = dati.legaleRappresentante?.split(/[,(]/, 1)[0];
  const rappresentante = scomponiNominativo(nominativoRappresentante);
  const tipoContribuente = tipoContribuenteDaRagioneSociale(dati.ragioneSociale);
  return {
    ...form,
    ...profilo,
    ...rappresentante,
    ragioneSociale: valoreNonVuoto(dati.ragioneSociale) ?? form.ragioneSociale,
    partitaIva: valoreNonVuoto(dati.partitaIva) ?? form.partitaIva,
    codiceFiscale: valoreNonVuoto(dati.codiceFiscale) ?? form.codiceFiscale,
    indirizzo: valoreNonVuoto(dati.indirizzo) ?? form.indirizzo,
    cap: valoreNonVuoto(dati.cap) ?? form.cap,
    comune: valoreNonVuoto(dati.comune) ?? form.comune,
    provincia: valoreNonVuoto(dati.provincia)?.toUpperCase() ?? form.provincia,
    codiceAteco: valoreNonVuoto(dati.codiceAteco) ?? form.codiceAteco,
    tipoContribuente: tipoContribuente ?? form.tipoContribuente,
  };
}

/**
 * Dati contributivi che mancano a un'azienda con dipendenti. Il server rifiuta F24 e UniEmens
 * finché questo elenco non è vuoto: serve a mostrarlo nell'archivio invece di scoprirlo
 * al momento dell'adempimento.
 */
export function datiContributiviMancanti(a: {
  haDipendenti?: boolean;
  matricolaInps?: string | null; codiceSedeInps?: string | null; cscInps?: string | null;
  patInail?: string | null; ccnlDefault?: string | null; livelloDefault?: string | null;
  codiciAutorizzazioneInps?: string | null;
  codiceFondoIntegrazioneSalariale?: string | null;
  codiceAgevolazioneContributiva?: string | null;
  aliquotaInpsDipendente?: number | null; aliquotaInpsDatore?: number | null;
  fonteProfiloContributivo?: string | null;
  riferimentoVisuraCamerale?: string | null; statoAlboArtigiani?: string | null;
  riferimentoVerificaApplicabilitaCcnl?: string | null;
  riferimentoTerzoElementoTerritoriale?: string | null;
  provincia?: string | null;
}): string[] {
  if (!a.haDipendenti) return [];
  const mancanti: string[] = [];
  if (!a.matricolaInps?.trim()) mancanti.push("matricola INPS");
  if (!a.codiceSedeInps?.trim()) mancanti.push("codice sede INPS");
  if (!a.cscInps?.trim()) mancanti.push("CSC INPS");
  if (!a.patInail?.trim()) mancanti.push("P.A.T. INAIL");
  if (!a.ccnlDefault?.trim()) mancanti.push("CCNL");
  if (!a.livelloDefault?.trim()) mancanti.push("livello CCNL");
  if (!a.codiciAutorizzazioneInps?.trim()) mancanti.push("codici autorizzazione INPS / NESSUNO");
  if (!a.codiceFondoIntegrazioneSalariale?.trim()) mancanti.push("FIS o altro fondo / NESSUNO");
  if (!a.codiceAgevolazioneContributiva?.trim()) mancanti.push("agevolazione / NESSUNA");
  if (!(a.aliquotaInpsDipendente && a.aliquotaInpsDipendente > 0)) mancanti.push("aliquota INPS dipendente");
  if (!(a.aliquotaInpsDatore && a.aliquotaInpsDatore > 0)) mancanti.push("aliquota INPS datore");
  if (!a.fonteProfiloContributivo?.trim()) mancanti.push("fonte profilo contributivo");
  if (a.ccnlDefault === "COMMERCIO_TERZIARIO") {
    if (!a.riferimentoVisuraCamerale?.trim()) mancanti.push("visura camerale verificata");
    if (a.statoAlboArtigiani !== "NonIscritta") mancanti.push("verifica Albo artigiani");
    if (!a.riferimentoVerificaApplicabilitaCcnl?.trim()) mancanti.push("verifica applicabilità H011");
    if (a.provincia?.toUpperCase() === "MI" && !a.riferimentoTerzoElementoTerritoriale?.trim())
      mancanti.push("fonte terzo elemento Milano");
  }
  return mancanti;
}

export function validaAzienda(f: NuovaAzienda, opzioni: OpzioniValidazioneAzienda = {}): ErroriAzienda {
  const e: ErroriAzienda = {};
  if (!f.ragioneSociale.trim()) e.ragioneSociale = "Il nome azienda è obbligatorio.";
  else if (f.ragioneSociale.trim().length < 2) e.ragioneSociale = "Inserisci almeno 2 caratteri.";
  if (!f.partitaIva?.trim() && !f.codiceFiscale?.trim()) {
    e.partitaIva = "Inserisci Partita IVA o Codice Fiscale.";
    e.codiceFiscale = "Serve almeno uno tra Partita IVA e Codice Fiscale.";
  }
  const partitaIva = f.partitaIva?.trim();
  if (partitaIva && !/^\d{11}$/.test(partitaIva)) {
    e.partitaIva = "La Partita IVA deve essere esattamente 11 cifre.";
  } else if (partitaIva && !partitaIvaValida(partitaIva)) {
    e.partitaIva = "La Partita IVA non supera il controllo formale del checksum.";
  }
  const codiceFiscale = f.codiceFiscale?.trim();
  if (codiceFiscale && !/^(\d{11}|[A-Za-z0-9]{16})$/.test(codiceFiscale)) {
    e.codiceFiscale = "Il Codice Fiscale deve essere 11 cifre o 16 caratteri.";
  } else if (codiceFiscale && !codiceFiscaleValido(codiceFiscale)) {
    e.codiceFiscale = "Il Codice Fiscale non supera il controllo formale del checksum.";
  }
  if (f.codiceDestinatarioSdi?.trim() && !/^[A-Za-z0-9]{7}$/.test(f.codiceDestinatarioSdi.trim())) e.codiceDestinatarioSdi = "Il codice SdI deve essere 7 caratteri.";
  if (!f.settore?.trim()) e.settore = "Scegli il settore attività.";
  if (!f.codiceAteco?.trim()) e.codiceAteco = "Il codice ATECO è obbligatorio.";
  else if (!/^\d{2}(\.\d{2}){0,2}$/.test(f.codiceAteco.trim())) e.codiceAteco = "Formato ATECO non valido. Esempio: 62.01.00.";
  if (!f.indirizzo.trim()) e.indirizzo = "L'indirizzo è obbligatorio.";
  else if (f.indirizzo.trim().length < 5) e.indirizzo = "Indirizzo troppo corto.";
  if (!f.comune.trim()) e.comune = "Il comune è obbligatorio.";
  if (!f.cap.trim()) e.cap = "Il CAP è obbligatorio.";
  else if (!/^\d{5}$/.test(f.cap.trim())) e.cap = "Il CAP deve essere 5 cifre.";
  if (!f.provincia.trim()) e.provincia = "La provincia è obbligatoria.";
  else if (!/^[A-Za-z]{2}$/.test(f.provincia.trim())) e.provincia = "La provincia deve essere 2 lettere (es. MI).";
  if (f.email?.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = "Formato email non valido.";
  if (f.pec?.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.pec.trim())) e.pec = "Formato PEC non valido.";
  if (!f.tipoContribuente?.trim()) e.tipoContribuente = "Scegli il tipo contribuente.";
  if (f.iban?.trim() && !/^IT[0-9A-Z]{25}$/i.test(f.iban.replace(/\s+/g, ""))) e.iban = "IBAN italiano non valido o incompleto.";
  if (f.haDipendenti) {
    const provvisori = Boolean(opzioni.datiContributiviProvvisori);
    if (!provvisori && !f.matricolaInps?.trim()) e.matricolaInps = "La matricola INPS serve per F24, UniEmens e cedolini.";
    if (!provvisori && !f.codiceSedeInps?.trim()) e.codiceSedeInps = "Il codice sede INPS è obbligatorio se ha dipendenti.";
    if (!provvisori && !f.cscInps?.trim()) e.cscInps = "Il CSC INPS è obbligatorio se ha dipendenti.";
    else if (f.cscInps?.trim() && !/^\d{5}$/.test(f.cscInps.trim())) e.cscInps = "Il CSC INPS deve essere di 5 cifre.";
    if (!provvisori && !f.patInail?.trim()) e.patInail = "La P.A.T. INAIL è obbligatoria se ha dipendenti.";
    if (!provvisori && !f.ccnlDefault?.trim()) e.ccnlDefault = "Scegli il CCNL predefinito per creare i dipendenti.";
    if (!provvisori && !f.livelloDefault?.trim()) e.livelloDefault = "Indica il livello base del CCNL.";
    if (!provvisori && !f.codiciAutorizzazioneInps?.trim())
      e.codiciAutorizzazioneInps = "Indica i CA effettivi oppure scrivi NESSUNO.";
    if (!provvisori && !f.codiceFondoIntegrazioneSalariale?.trim())
      e.codiceFondoIntegrazioneSalariale = "Indica FIS/altro fondo oppure scrivi NESSUNO.";
    if (!provvisori && !f.codiceAgevolazioneContributiva?.trim())
      e.codiceAgevolazioneContributiva = "Indica il codice reale oppure scrivi NESSUNA.";
    if (!provvisori && !(f.aliquotaInpsDipendente && f.aliquotaInpsDipendente > 0))
      e.aliquotaInpsDipendente = "Inserisci l'aliquota dipendente del profilo contributivo reale.";
    if (!provvisori && !(f.aliquotaInpsDatore && f.aliquotaInpsDatore > 0))
      e.aliquotaInpsDatore = "Inserisci l'aliquota datore del profilo contributivo reale.";
    if (!provvisori && !f.fonteProfiloContributivo?.trim())
      e.fonteProfiloContributivo = "Indica il documento da cui deriva il profilo contributivo.";

    const soloZeri = (valore?: string) => Boolean(valore?.trim() && /^[0/ .-]+$/.test(valore.trim()));
    if (soloZeri(f.matricolaInps)) e.matricolaInps = "Non usare 000000: in modalità prova lascia vuota la matricola INPS.";
    if (soloZeri(f.codiceSedeInps)) e.codiceSedeInps = "Non usare 000000: in modalità prova lascia vuoto il codice sede INPS.";
    if (soloZeri(f.cscInps)) e.cscInps = "Non usare 00000: in modalità prova lascia vuoto il CSC INPS.";
    if (soloZeri(f.patInail)) e.patInail = "Non usare 000000: in modalità prova lascia vuota la P.A.T. INAIL.";
  }
  return e;
}
