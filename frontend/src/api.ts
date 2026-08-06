// Client API tipizzato verso il backend Paghe SaaS.

import { dimenticaAziendaCorrente } from "./aziendaCorrente";
import { buildMobile, condividiDocumento, piattaformaNativa } from "./mobile/piattaforma";
import {
  accessoInScadenza,
  cancellaSessioneLocale,
  getAccessToken,
  rinnovaSessione,
  type SessioneAccesso,
} from "./sessione";

// In sviluppo Vite e API usano porte diverse; nel pacchetto cloud Railway la SPA
// viene servita dalla stessa API e quindi usa URL relativi (stessa origine).
const BASE = (import.meta.env.VITE_API_URL as string | undefined)?.trim()
  || (import.meta.env.DEV ? "http://localhost:5171" : "");

if (import.meta.env.VITE_MOBILE_APP === "1" && (!BASE || !BASE.startsWith("https://")))
  throw new Error("Configurazione mobile non valida: VITE_API_URL deve essere un indirizzo HTTPS.");

export function getToken(): string | null {
  return getAccessToken();
}
export async function pulisciAccesso(): Promise<void> {
  dimenticaAziendaCorrente();
  await cancellaSessioneLocale();
}

export function getRuolo(): string | null {
  return localStorage.getItem("ruolo");
}
export function setRuolo(ruolo: string | null): void {
  if (ruolo) localStorage.setItem("ruolo", ruolo);
  else localStorage.removeItem("ruolo");
}

/**
 * Utility per avviare il download nel browser di un file Blob (PDF, XML, zip, ecc.).
 * Gestisce l'ancora DOM nel document.body ed il rilascio differito dell'object URL
 * per piena compatibilità con Safari su Mac, iOS, Chrome e Firefox.
 */
export async function scaricaBlob(blob: Blob, filename: string): Promise<void> {
  if (piattaformaNativa) {
    await condividiDocumento(blob, filename);
    return;
  }
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.style.display = "none";
  document.body.appendChild(a);
  a.click();
  setTimeout(() => {
    a.remove();
    URL.revokeObjectURL(url);
  }, 1000);
}

/** Errore API con il corpo della risposta (es. { serveCodice2fa: true } dal login). */
export class ApiError extends Error {
  readonly status: number;
  readonly corpo?: Record<string, unknown>;

  constructor(messaggio: string, status: number, corpo?: Record<string, unknown>) {
    super(messaggio);
    this.status = status;
    this.corpo = corpo;
  }
}

export async function fetchAutenticata(path: string, options: RequestInit = {}): Promise<Response> {
  const autenticazione = path.startsWith("/api/auth/");
  if (!autenticazione && accessoInScadenza())
    await rinnovaSessione();

  let token = getToken();
  const headers: Record<string, string> = {
    ...((options.headers as Record<string, string>) ?? {}),
  };
  if (!(options.body instanceof FormData) && !headers["Content-Type"])
    headers["Content-Type"] = "application/json";
  if (autenticazione && !buildMobile)
    headers["X-PaghePro-Client"] = "web";
  if (token) headers["Authorization"] = `Bearer ${token}`;

  let res = await fetch(`${BASE}${path}`, {
    ...options,
    headers,
    credentials: buildMobile ? "omit" : "include",
  });

  if (res.status === 401 && token && !autenticazione) {
    await rinnovaSessione();
    token = getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
    res = await fetch(`${BASE}${path}`, {
      ...options,
      headers,
      credentials: buildMobile ? "omit" : "include",
    });
  }
  return res;
}

async function req<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await fetchAutenticata(path, options);

  if (!res.ok) {
    let messaggio = `Errore ${res.status}`;
    let corpo: Record<string, unknown> | undefined;
    try {
      corpo = (await res.json()) as Record<string, unknown>;
      if (corpo?.errore) messaggio = corpo.errore as string;
    } catch {
      /* corpo non JSON */
    }
    throw new ApiError(messaggio, res.status, corpo);
  }
  if (res.status === 204) return undefined as T;
  const ct = res.headers.get("content-type") ?? "";
  return ct.includes("application/json") ? ((await res.json()) as T) : ((await res.text()) as unknown as T);
}

// --- Tipi ---
export interface AuthResponse extends SessioneAccesso {}
export interface StudioSintesi {
  id: string; ragioneSociale: string; sospeso: boolean; numeroUtenti: number; creatoIl: string;
}
export interface VoceRegistroAudit {
  id: string; utenteId?: string | null; utente: string;
  categoria: "Accesso" | "ModificaDati" | string;
  azione: string; entita: string; chiaveEntita: string;
  campiModificati?: string | null; metodoHttp?: string | null;
  percorsoTemplate?: string | null; esito?: string | null;
  statusCode?: number | null; correlationId?: string | null; quandoUtc: string;
}
export interface PaginaRegistroAudit {
  voci: VoceRegistroAudit[]; pagina: number; dimensione: number; totale: number;
}
export interface FiltriRegistroAudit {
  pagina?: number; dimensione?: number; categoria?: string; azione?: string;
  entita?: string; daUtc?: string; aUtc?: string;
}
export interface PolicyRetention {
  categoria: string; titolo: string; finalita: string; decorrenza: string; azione: string;
  avvertenza: string; versione: number; giorniConservazione?: number | null;
  attiva: boolean; approvata: boolean; baseGiuridica?: string | null;
  riferimentoDecisione?: string | null; istruzioniBackup?: string | null; creataUtc?: string | null;
}
export interface SalvaPolicyRetention {
  versioneAttesa: number; giorniConservazione?: number | null; attiva: boolean; approvata: boolean;
  baseGiuridica?: string | null; riferimentoDecisione?: string | null; istruzioniBackup?: string | null;
}
export interface AnteprimaRetention {
  id: string; categoria: string; versionePolicy: number; sogliaUtc: string;
  candidati: number; bloccati: number; elaborabili: number; creataUtc: string; scadeUtc: string;
  codiceConferma: string; azione: string; nota: string;
}
export interface BloccoConservazione {
  id: string; categoria: string; motivoCodice: string; creatoUtc: string; revocatoUtc?: string | null;
}
export interface RicevutaRetention {
  id: string; categoria: string; versionePolicy: number; sogliaUtc: string;
  candidatiAnteprima: number; bloccati: number; elaborati: number; restantiStimati: number;
  esito: string; iniziataUtc: string; completataUtc: string; correlationId?: string | null; digest: string;
}
export interface DatiVisura {
  ragioneSociale?: string | null; partitaIva?: string | null; codiceFiscale?: string | null;
  indirizzo?: string | null; cap?: string | null; comune?: string | null; provincia?: string | null;
  codiceAteco?: string | null; legaleRappresentante?: string | null; nota?: string;
}
export interface Azienda {
  id: string; ragioneSociale: string;
  partitaIva?: string | null; codiceFiscale?: string | null; indirizzo?: string; cap?: string; comune?: string; provincia?: string;
  codiceCatastale?: string | null; telefono?: string | null; email?: string | null; pec?: string | null;
  codiceDestinatarioSdi?: string | null; settore?: string | null; codiceAteco?: string | null;
  matricolaInps?: string | null; codiceSedeInps?: string | null; cscInps?: string | null; patInail?: string | null;
  ccnlDefault?: string | null; livelloDefault?: string | null; iban?: string | null;
  legaleRappresentanteNome?: string | null; legaleRappresentanteCognome?: string | null; legaleRappresentanteCodiceFiscale?: string | null;
  canalePredefinito?: string | null; haDipendenti?: boolean; tipoContribuente?: string | null; assoggettataIva?: boolean;
}
export interface NuovaAzienda {
  ragioneSociale: string; partitaIva?: string; codiceFiscale?: string; indirizzo: string;
  cap: string; comune: string; provincia: string; codiceCatastale?: string; nazione?: string; regimeFiscale?: string;
  telefono?: string; email?: string; pec?: string; codiceDestinatarioSdi?: string; settore?: string; codiceAteco?: string;
  matricolaInps?: string; codiceSedeInps?: string; cscInps?: string; patInail?: string; ccnlDefault?: string; livelloDefault?: string;
  iban?: string; legaleRappresentanteNome?: string; legaleRappresentanteCognome?: string; legaleRappresentanteCodiceFiscale?: string;
  canalePredefinito?: string; haDipendenti?: boolean; tipoContribuente?: string; assoggettataIva?: boolean;
}
export interface EsitoCaricamentoScadenza {
  aziendaTrovata: boolean; ragioneSociale?: string | null;
  scadenza?: string | null; numero?: string | null;
  promemoriaCreatoId?: string | null; errore?: string | null;
}
export interface Livello { codice: string; descrizione: string; minimoTabellare: number }
export interface Rapporto {
  codiceCcnl: string; livello: string; qualifica: string; numeroMensilita: number;
  scattiAnzianita: number; regione: string; codiceCatastaleComune: string;
  superminimo?: number; partTime?: boolean; percentualePartTime?: number;
}
export interface Cartellino {
  anno: number; mese: number;
  oreStraordinario: number; oreStraordinarioNotturno?: number; oreStraordinarioFestivo?: number;
  giorniFerie?: number; orePermessi?: number; oreExFestivita?: number;
  giorniMalattia?: number; numeroEventoMalattiaAnno?: number; giorniInfortunio?: number;
  oreAssenzaNonRetribuita?: number;
}
export interface VoceCedolino { codice: string; descrizione: string; tipo: string; importo: number }
export interface Cedolino {
  totaleCompetenze: number; totaleTrattenute: number; nettoInBusta: number;
  costoAzienda: number; quotaTfr: number; righe: VoceCedolino[];
}
export interface CedolinoArchiviato {
  id: string; anno: number; mese: number;
  nettoInBusta: number; costoAzienda: number;
  imponibileIrpef?: number; irpefNetta?: number;
  conguaglioIrpef?: number;
  ferieResidue?: number; permessiResidui?: number;
}
export interface ConguaglioAnnuale {
  irpefLordaAnnua: number; detrazioniSpettanti: number; irpefNettaAnnua: number;
  irpefGiaTrattenuta: number; conguaglio: number; aDebito: boolean;
}
export interface TfrRivalutazione {
  anno: number; fondoAl31DicembrePrecedente: number; variazioneIstat: number;
  coefficiente: number; rivalutazioneLorda: number; impostaSostitutiva: number;
  rivalutazioneNetta: number; fondoRivalutato: number; quoteMaturateAnno: number; fondoFinale: number;
}
export interface PosizioneInail {
  anno: number; pat: string; voceTariffa: string; tassoPerMille: number;
}
export interface InailAutoliquidazione {
  anno: number; pat: string; voceTariffa: string; numeroCedolini: number;
  retribuzioniAnno: number; tassoPerMille: number; regolazione: number;
  rataAnticipata: number; rataGiaVersata: number; totaleDaVersare: number; nota: string;
}
export interface PraticaInail {
  id: string; aziendaId: string; anno: number; pat: string; voceTariffa: string;
  numeroCedolini: number; retribuzioniAnno: number; tassoPerMille: number;
  regolazione: number; rataAnticipata: number; rataGiaVersata: number;
  totaleDaVersare: number; stato: string; creataIl: string;
}
export interface PresenzaRiga {
  rapportoId: string; nominativo: string;
  oreStraordinario: number; oreStraordinarioNotturno: number; oreStraordinarioFestivo: number;
  giorniFerie: number; orePermessi: number; oreExFestivita: number;
  giorniMalattia: number; numeroEventoMalattiaAnno: number; giorniInfortunio: number;
  oreAssenzaNonRetribuita: number; elaborato: boolean; netto: number;
  // Saldi dall'ultimo cedolino elaborato prima del mese (null senza storico).
  ferieResidue?: number | null; permessiResidui?: number | null;
  saldiAnno?: number | null; saldiMese?: number | null;
}
export interface PresenzeMese {
  anno: number; mese: number; totale: number; elaborati: number; righe: PresenzaRiga[];
}
export interface RedditiPfInputReq {
  altriRedditi: number; oneriDeducibili: number; oneriDetraibili19: number; acconti: number;
}
export interface IrapBozza {
  anno: number; ricavi: number; costiEsterni: number; costoLavoroDeducibile: number;
  baseImponibile: number; aliquota: number; imposta: number;
}
export interface ImmobileInputReq {
  descrizione: string; categoriaCatastale: string; renditaCatastale: number;
  percentualePossesso: number; mesiPossesso: number; aliquotaPerMille: number; abitazionePrincipale: boolean;
}
export interface ImuRiga {
  id: string; descrizione: string; categoriaCatastale: string; renditaCatastale: number;
  moltiplicatore: number; baseImponibile: number; esente: boolean; imposta: number;
}
export interface ImuBozza { anno: number; totaleImposta: number; righe: ImuRiga[]; }
export interface IntrastatRiga { paese: string; numeroOperazioni: number; totaleImponibile: number; }
export interface IntrastatBozza { anno: number; totaleCessioni: number; righe: IntrastatRiga[]; }
export interface IresBozza {
  anno: number; haBilancio: boolean; utileEsercizio: number; imposteBilancio: number;
  redditoAnteImposte: number; variazioniNette: number; baseImponibile: number; aliquota: number; imposta: number;
}
export interface RedditiPfBozza {
  rapportoId: string; nominativo: string; codiceFiscale: string; anno: number; numeroCedolini: number;
  altriRedditi: number; oneriDeducibili: number; oneriDetraibili19: number; acconti: number;
  redditoLavoroDipendente: number; ritenuteOperate: number;
  redditoComplessivo: number; redditoImponibile: number; irpefLorda: number;
  detrazioneLavoroDipendente: number; detrazioneOneri: number; irpefNetta: number;
  saldoADebito: number; saldoACredito: number;
}
export interface RapportoDettaglio {
  rapportoId: string; dipendenteId: string;
  nome: string; cognome: string; codiceFiscale: string; iban?: string | null; anonimizzato: boolean;
  codiceCcnl: string; livello: string; qualifica: string;
  numeroMensilita: number; partTime: boolean; percentualePartTime: number;
  scattiAnzianita: number; superminimo: number; regione: string; dataAssunzione: string;
  dataCessazione?: string | null;
}
/** Campi correggibili della scheda dipendente: solo quelli presenti vengono cambiati. */
export interface CorrezioneRapporto {
  nome?: string; cognome?: string; codiceFiscale?: string; iban?: string;
  codiceCcnl?: string; livello?: string; qualifica?: string;
  numeroMensilita?: number; partTime?: boolean; percentualePartTime?: number;
  scattiAnzianita?: number; superminimo?: number;
  regione?: string; codiceCatastaleComune?: string; dataAssunzione?: string;
  fondoTfrIniziale?: number;
  dataCessazione?: string; rimuoviDataCessazione?: boolean;
}
export interface Fattura {
  id: string; numero: string; anno?: number; data?: string;
  cessionarioDenominazione?: string; totaleDocumento: number;
}
export interface RigaFatturaInput {
  descrizione: string; quantita: number; prezzoUnitario: number; aliquotaIva: number;
}
export interface NuovaFattura {
  data: string;
  cessionario: {
    denominazione: string; partitaIva?: string; codiceFiscale?: string;
    indirizzo: string; cap: string; comune: string; provincia: string; nazione?: string;
  };
  righe: RigaFatturaInput[];
}
export interface Trasmissione {
  id: string; stato: string; identificativoSdi?: string | null;
  nomeFileXml?: string; firmato?: boolean; provider?: string; messaggioErrore?: string | null;
}
export interface Delega {
  id: string; aziendaId: string; tipoAdempimento: string; stato: string;
  dataInizio: string; dataFine?: string | null; riferimento?: string | null;
}
export interface AvvisoPagamento {
  id: string; aziendaId: string; cliente?: string | null;
  anno: number; mese: number; descrizione: string; importo: number;
  dataEmissione: string; dataScadenza: string; stato: string;
  metodoPagamento: string; iban?: string | null; causale?: string | null; note?: string | null;
  pagatoIl?: string | null; riferimentoPagamento?: string | null;
  numeroPromemoria: number; ultimoPromemoriaIl?: string | null;
  giorniAllaScadenza: number; scaduto: boolean;
}
export interface NuovoAvvisoPagamento {
  anno: number; mese: number; importo: number; dataScadenza: string;
  descrizione?: string; dataEmissione?: string; metodoPagamento?: string;
  iban?: string; causale?: string; note?: string;
}
export interface StatoAvvisoPagamento {
  stato: string; pagatoIl?: string; riferimentoPagamento?: string; note?: string;
}
export interface Promemoria {
  id: string; aziendaId?: string | null; tipo: string; riferimentoId?: string | null;
  titolo: string; descrizione: string; dataScadenza: string; giorniPreavviso: number;
  stato: string; messaggio: string; giorniAllaScadenza: number; urgenza: string;
}
export interface Durc {
  id: string; aziendaId: string; numero: string;
  dataEmissione: string; dataScadenza: string; regolare: boolean; note?: string | null;
}
export interface NuovoDurc {
  numero: string; dataEmissione: string; dataScadenza?: string; regolare?: boolean; note?: string;
}
export interface NuovoPromemoria {
  titolo: string; dataScadenza: string; aziendaId?: string; descrizione?: string; giorniPreavviso?: number;
}
export interface EsitoInvioPromemoria {
  inviato: boolean; canale: string; destinatario: string; dettaglio: string;
  messaggio: string; testoBreve: string;
}
export interface VoceBilancio {
  codice: string; descrizione: string; voce: string; importo: number;
}
export interface BilancioSintesi {
  id: string; anno: number; stato: string; quadra: boolean;
  valoreDellaProduzione: number; costiDellaProduzione: number; utilePerditaEsercizio: number;
  totaleAttivo: number; totalePassivo: number; patrimonioNetto: number; sbilancioAttivoPassivo: number;
}
export interface BilancioDettaglio extends BilancioSintesi {
  proventiFinanziari: number; oneriFinanziari: number; imposte: number;
  bozza: string; righe: VoceBilancio[];
}
export interface NuovoBilancio { anno: number; voci: VoceBilancio[] }
export interface PrecompilaBilancio { anno: number; voci: VoceBilancio[]; nota: string }
export interface AdempimentoPratica {
  id: string; codice: string; ente: string; descrizione: string;
  obbligatorio: boolean; stato: string; note?: string | null;
}
export interface PraticaComUnica {
  id: string; tipoPratica: string; formaGiuridica: string; regimeContabile: string;
  dataInizioAttivita: string; denominazione: string; codiceFiscale: string;
  partitaIva?: string | null; codiceAteco: string; descrizioneAttivita: string;
  indirizzoSede: string; cap: string; comune: string; provincia: string; pec?: string | null;
  attivitaCommercio: boolean; attivitaArtigiana: boolean; impresaAgricola: boolean;
  haDipendentiPrevisti: boolean; soggettaInail: boolean;
  modelloAgenziaEntrate: string; richiedeComunicazioneUnica: boolean;
  richiedeRegistroImprese: boolean; richiedeCameraCommercio: boolean;
  stato: string; creataIl: string; adempimenti: AdempimentoPratica[];
}
export interface PianoComUnica {
  modelloAgenziaEntrate: string; richiedeComunicazioneUnica: boolean;
  richiedeRegistroImprese: boolean; richiedeCameraCommercio: boolean;
  adempimenti: { codice: string; ente: string; descrizione: string; obbligatorio: boolean; stato: string }[];
  avvertenze: string[];
}
export interface NuovaPraticaComUnica {
  tipoPratica: string; formaGiuridica: string; regimeContabile: string;
  dataInizioAttivita: string; denominazione: string; codiceFiscale: string;
  partitaIva?: string; pec?: string; codiceAteco: string; descrizioneAttivita: string;
  indirizzoSede: string; cap: string; comune: string; provincia: string;
  attivitaCommercio?: boolean; attivitaArtigiana?: boolean; impresaAgricola?: boolean;
  haDipendentiPrevisti?: boolean; soggettaInail?: boolean; aziendaId?: string;
}
export interface DatiPraticaEstratti {
  tipoPratica?: string | null; formaGiuridica?: string | null; regimeContabile?: string | null;
  dataDecorrenza?: string | null; denominazione?: string | null; codiceFiscale?: string | null;
  partitaIva?: string | null; pec?: string | null; codiceAteco?: string | null;
  descrizioneAttivita?: string | null; indirizzo?: string | null; cap?: string | null;
  comune?: string | null; provincia?: string | null;
  attivitaCommercio?: boolean | null; attivitaArtigiana?: boolean | null;
  impresaAgricola?: boolean | null; haDipendentiPrevisti?: boolean | null;
  soggettaInail?: boolean | null;
  campiMancanti: string[];
}
export type FormatoFirma = "cades" | "xades";
export interface EsitoVerifica {
  valido: boolean;
  formato: string;
  firmatario?: string | null;
  dataFirmaUtc?: string | null;
  algoritmoDigest?: string | null;
}

// Replica lato client la convenzione di nome file del backend (NomeFileFirmato).
function nomeFileFirmato(nome: string, formato: FormatoFirma): string {
  if (formato === "xades") {
    return nome.toLowerCase().endsWith(".xml")
      ? `${nome.slice(0, -4)}-firmato.xml`
      : `${nome}-firmato.xml`;
  }
  return `${nome}.p7m`;
}

// --- Endpoint ---
export const api = {
  registra: async (ragioneSociale: string, email: string, password: string) => {
    const r = await req<AuthResponse>(buildMobile ? "/api/auth/mobile/registra" : "/api/auth/registra", {
      method: "POST",
      body: JSON.stringify({ ragioneSociale, email, password }),
    });
    setRuolo(r.ruolo); // memorizza il ruolo (es. superadmin) per mostrare il pannello giusto
    return r;
  },
  login: async (email: string, password: string, codice2fa?: string) => {
    const r = await req<AuthResponse>(buildMobile ? "/api/auth/mobile/login" : "/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password, codice2fa }),
    });
    setRuolo(r.ruolo);
    return r;
  },

  // Accesso con Google: invia il "credential" (ID token) e riceve il nostro token.
  googleLogin: async (credential: string) => {
    const r = await req<AuthResponse>("/api/auth/google", {
      method: "POST",
      body: JSON.stringify({ credential }),
    });
    setRuolo(r.ruolo);
    return r;
  },

  // --- Super-admin di piattaforma: gestione/sospensione studi ---
  elencaStudi: () => req<StudioSintesi[]>("/api/admin/studi"),
  sospendiStudio: (id: string) =>
    req<{ id: string; sospeso: boolean }>(`/api/admin/studi/${id}/sospendi`, { method: "POST" }),
  riattivaStudio: (id: string) =>
    req<{ id: string; sospeso: boolean }>(`/api/admin/studi/${id}/riattiva`, { method: "POST" }),
  registroAttivita: (filtri: FiltriRegistroAudit = {}) => {
    const query = new URLSearchParams();
    Object.entries(filtri).forEach(([chiave, valore]) => {
      if (valore !== undefined && valore !== "") query.set(chiave, String(valore));
    });
    return req<PaginaRegistroAudit>(`/api/audit${query.size > 0 ? `?${query}` : ""}`);
  },

  // --- Conservazione dati GDPR (solo admin dello studio): policy, blocchi, anteprime, esecuzioni ---
  retentionPolicy: () => req<PolicyRetention[]>("/api/retention"),
  salvaPolicyRetention: (categoria: string, dati: SalvaPolicyRetention) =>
    req<PolicyRetention>(`/api/retention/${categoria}`, { method: "PUT", body: JSON.stringify(dati) }),
  anteprimaRetention: (categoria: string) =>
    req<AnteprimaRetention>(`/api/retention/${categoria}/anteprima`, { method: "POST" }),
  blocchiConservazione: () => req<BloccoConservazione[]>("/api/retention/blocchi"),
  creaBloccoConservazione: (categoria: string, motivoCodice: string) =>
    req<BloccoConservazione>("/api/retention/blocchi", {
      method: "POST",
      body: JSON.stringify({ categoria, motivoCodice }),
    }),
  revocaBloccoConservazione: (id: string) =>
    req<BloccoConservazione>(`/api/retention/blocchi/${id}/revoca`, { method: "POST" }),
  esecuzioniRetention: () => req<RicevutaRetention[]>("/api/retention/esecuzioni"),
  eseguiRetention: (dati: {
    anteprimaId: string; chiaveIdempotenza: string; codiceConferma: string; dimensioneLotto?: number;
  }) => req<RicevutaRetention>("/api/retention/esegui", { method: "POST", body: JSON.stringify(dati) }),
  aziende: () => req<Azienda[]>("/api/aziende"),
  creaDatasetDemo: () => req<{ aziende: number; dipendenti: number }>("/api/demo/dataset", { method: "POST" }),
  // Correzione anagrafica azienda (es. dopo una visura letta male): invia solo i campi da cambiare.
  correggiAzienda: (id: string, campi: Partial<NuovaAzienda>) =>
    req<{ id: string; ragioneSociale: string }>(`/api/aziende/${id}`, {
      method: "PATCH",
      body: JSON.stringify(campi),
    }),
  creaAzienda: (a: NuovaAzienda) =>
    req<{ id: string; ragioneSociale: string }>("/api/aziende", {
      method: "POST",
      body: JSON.stringify(a),
    }),
  eliminaAzienda: (id: string) =>
    req<void>(`/api/aziende/${id}`, { method: "DELETE" }),
  eliminaAziendeBulk: (ids: string[]) =>
    req<{ eliminate: number }>("/api/aziende/elimina-bulk", {
      method: "POST",
      body: JSON.stringify({ ids }),
    }),
  // --- Logo aziendale (PNG/JPEG, facoltativo): compare in testata della busta paga ---
  caricaLogoAzienda: async (id: string, file: File): Promise<void> => {
    const form = new FormData();
    form.append("file", file);
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata(`/api/aziende/${id}/logo`, { method: "POST", body: form, headers });
    if (!res.ok) {
      const e = await res.json().catch(() => ({}));
      throw new Error((e as { errore?: string }).errore ?? `Errore ${res.status}`);
    }
  },
  // Object URL dell'immagine del logo, o null se l'azienda non ne ha uno.
  logoAzienda: async (id: string): Promise<string | null> => {
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata(`/api/aziende/${id}/logo`, { headers });
    if (!res.ok) return null;
    return URL.createObjectURL(await res.blob());
  },
  rimuoviLogoAzienda: (id: string) => req<void>(`/api/aziende/${id}/logo`, { method: "DELETE" }),
  // Certificazione Unica (CU) di un dipendente per anno: PDF sul modello ufficiale AdE.
  certificazioneUnicaPdf: async (rapportoId: string, anno: number): Promise<{ blob: Blob; filename: string }> => {
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata(`/api/rapporti/${rapportoId}/cu/${anno}/pdf`, { headers });
    if (!res.ok) {
      const e = await res.json().catch(() => ({}));
      throw new Error((e as { errore?: string }).errore ?? `Errore ${res.status}`);
    }
    const dispo = res.headers.get("Content-Disposition") ?? "";
    const match = /filename="?([^"]+)"?/.exec(dispo);
    return { blob: await res.blob(), filename: match?.[1] ?? `certificazione-unica-${anno}.pdf` };
  },
  livelli: (codice: string) =>
    req<{ codice: string; descrizione: string; livelli: Livello[] }>(
      `/api/ccnl/${codice}/livelli`,
    ),
  calcolaCedolino: (rapporto: Rapporto, cartellino: Cartellino) =>
    req<Cedolino>("/api/cedolini/calcola", {
      method: "POST",
      body: JSON.stringify({ rapporto, cartellino }),
    }),
  // Cedolini archiviati per dipendente (rapporto): alimentano CU e 770.
  cedoliniArchiviati: (rapportoId: string) =>
    req<CedolinoArchiviato[]>(`/api/rapporti/${rapportoId}/cedolini`),
  // forza=true: ricalcolo confermato di un mese con F24 già inviato (correzione errore).
  archiviaCedolino: (rapportoId: string, cartellino: Cartellino, forza = false) =>
    req<CedolinoArchiviato>(`/api/rapporti/${rapportoId}/cedolini${forza ? "?forza=true" : ""}`, {
      method: "POST",
      body: JSON.stringify(cartellino),
    }),
  // Scarica il PDF di un cedolino archiviato (rigenerato dal backend).
  cedolinoPdf: async (id: string): Promise<{ blob: Blob; filename: string }> => {
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata(`/api/cedolini/${id}/pdf`, { headers });
    if (!res.ok) {
      let messaggio = `Errore ${res.status}`;
      try {
        const body = await res.json();
        if (body?.errore) messaggio = body.errore as string;
      } catch {
        /* corpo non JSON */
      }
      throw new Error(messaggio);
    }
    const dispo = res.headers.get("Content-Disposition") ?? "";
    const match = /filename="?([^"]+)"?/.exec(dispo);
    const filename = match?.[1] ?? `cedolino-${id}.pdf`;
    return { blob: await res.blob(), filename };
  },
  rapportoDettaglio: (rapportoId: string) =>
    req<RapportoDettaglio>(`/api/rapporti/${rapportoId}`),
  // Correzione della scheda (es. dopo un import UNILAV/OCR letto male): invia solo i campi da cambiare.
  correggiRapporto: (rapportoId: string, campi: CorrezioneRapporto) =>
    req<{ aggiornato: boolean }>(`/api/rapporti/${rapportoId}`, {
      method: "PATCH",
      body: JSON.stringify(campi),
    }),
  presenzeMese: (aziendaId: string, anno: number, mese: number) =>
    req<PresenzeMese>(`/api/aziende/${aziendaId}/presenze/${anno}/${mese}`),

  // --- Fine anno: conguaglio IRPEF, rivalutazione TFR, autoliquidazione INAIL ---
  conguaglioAnnuale: (rapportoId: string, anno: number) =>
    req<ConguaglioAnnuale>(`/api/rapporti/${rapportoId}/conguaglio?anno=${anno}`),
  tfrRivalutazione: (rapportoId: string, anno: number, variazioneIstat?: number) =>
    req<TfrRivalutazione>(
      `/api/rapporti/${rapportoId}/tfr/rivalutazione?anno=${anno}`
      + (variazioneIstat !== undefined ? `&variazioneIstat=${variazioneIstat}` : "")),
  // Posizione INAIL dell'anno; null se non ancora registrata (404).
  inailPosizione: async (aziendaId: string, anno: number): Promise<PosizioneInail | null> => {
    try {
      return await req<PosizioneInail>(`/api/aziende/${aziendaId}/inail/posizione?anno=${anno}`);
    } catch {
      return null;
    }
  },
  salvaPosizioneInail: (aziendaId: string, p: PosizioneInail) =>
    req<PosizioneInail>(`/api/aziende/${aziendaId}/inail/posizione`, {
      method: "POST",
      body: JSON.stringify(p),
    }),
  inailAutoliquidazione: (aziendaId: string, anno: number, rataVersata?: number) =>
    req<InailAutoliquidazione>(
      `/api/aziende/${aziendaId}/inail/autoliquidazione?anno=${anno}`
      + (rataVersata ? `&rataVersata=${rataVersata}` : "")),
  // Calcola e archivia l'autoliquidazione come pratica (stato Generata), riusabile da storico/export.
  salvaAutoliquidazioneInail: (aziendaId: string, anno: number, rataGiaVersata?: number) =>
    req<PraticaInail>(`/api/aziende/${aziendaId}/inail/autoliquidazione/salva`, {
      method: "POST",
      body: JSON.stringify({ anno, rataGiaVersata: rataGiaVersata ?? 0 }),
    }),
  autoliquidazioniInail: (aziendaId: string) =>
    req<PraticaInail[]>(`/api/aziende/${aziendaId}/inail/autoliquidazioni`),
  cambiaStatoInail: (id: string, stato: string) =>
    req<PraticaInail>(`/api/inail/${id}/stato`, { method: "POST", body: JSON.stringify({ stato }) }),
  scaricaBozzaInail: async (id: string): Promise<{ blob: Blob; filename: string }> => {
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata(`/api/inail/${id}/bozza`, { headers });
    if (!res.ok) throw new Error(`Errore ${res.status}`);
    const match = /filename="?([^";]+)"?/.exec(res.headers.get("content-disposition") ?? "");
    return { blob: await res.blob(), filename: match?.[1] ?? "autoliquidazione-inail.txt" };
  },

  // --- Dichiarazioni: Fase 4 Redditi PF / 730 (bozza) ---
  redditiPf: (rapportoId: string, anno: number) =>
    req<RedditiPfBozza>(`/api/rapporti/${rapportoId}/redditi-pf/${anno}`),
  salvaRedditiPf: (rapportoId: string, anno: number, input: RedditiPfInputReq) =>
    req<RedditiPfBozza>(`/api/rapporti/${rapportoId}/redditi-pf/${anno}`, {
      method: "PUT",
      body: JSON.stringify(input),
    }),

  // --- Dichiarazioni: Fase 5 IRAP (bozza) ---
  irapBozza: (aziendaId: string, anno: number) =>
    req<IrapBozza>(`/api/aziende/${aziendaId}/irap/${anno}`),

  // --- Dichiarazioni: Fase 5 IRES (Redditi società) bozza ---
  iresBozza: (aziendaId: string, anno: number, variazioni: number) =>
    req<IresBozza>(`/api/aziende/${aziendaId}/ires/${anno}?variazioni=${variazioni}`),

  // --- Dichiarazioni: Fase 6 IMU (immobili + calcolo) ---
  imuBozza: (aziendaId: string, anno: number) =>
    req<ImuBozza>(`/api/aziende/${aziendaId}/imu/${anno}`),
  intrastatBozza: (aziendaId: string, anno: number) =>
    req<IntrastatBozza>(`/api/aziende/${aziendaId}/intrastat/${anno}`),
  aggiungiImmobile: (aziendaId: string, i: ImmobileInputReq) =>
    req<{ id: string }>(`/api/aziende/${aziendaId}/immobili`, { method: "POST", body: JSON.stringify(i) }),
  eliminaImmobile: (id: string) =>
    req<void>(`/api/immobili/${id}`, { method: "DELETE" }),

  // --- Fatturazione e trasmissione SdI ---
  fatture: (aziendaId: string) => req<Fattura[]>(`/api/aziende/${aziendaId}/fatture`),
  creaFattura: (aziendaId: string, f: NuovaFattura) =>
    req<{ id: string; numero: string; totaleDocumento: number }>(
      `/api/aziende/${aziendaId}/fatture`,
      { method: "POST", body: JSON.stringify(f) },
    ),
  trasmetti: (fatturaId: string) =>
    req<Trasmissione>(`/api/fatture/${fatturaId}/trasmetti`, { method: "POST" }),
  // GET trasmissione: 404 (mai trasmessa) → null invece di errore.
  trasmissione: async (fatturaId: string): Promise<Trasmissione | null> => {
    try {
      return await req<Trasmissione>(`/api/fatture/${fatturaId}/trasmissione`);
    } catch {
      return null;
    }
  },
  registraRicevuta: (trasmissioneId: string, tipo: string, codiceEsito?: string) =>
    req<{ id: string; stato: string }>(`/api/trasmissioni/${trasmissioneId}/ricevute`, {
      method: "POST",
      body: JSON.stringify({ tipo, codiceEsito }),
    }),
  aggiornaEsito: (trasmissioneId: string) =>
    req<{ id: string; stato: string }>(`/api/trasmissioni/${trasmissioneId}/aggiorna-esito`, {
      method: "POST",
    }),
  fatturaXml: (fatturaId: string) => req<string>(`/api/fatture/${fatturaId}/xml`),

  // --- Firma digitale (CAdES .p7m / XAdES .xml) ---
  // Firma un file: con certificato software (pfx + password) oppure con smart card/CNS (pin).
  // Risponde con il documento firmato come blob da scaricare.
  firmaCades: async (
    file: File,
    opts: { pfx?: File; password?: string; pin?: string; formato?: FormatoFirma } = {},
  ): Promise<{ blob: Blob; filename: string }> => {
    const formato: FormatoFirma = opts.formato ?? "cades";
    const form = new FormData();
    form.append("file", file);
    form.append("formato", formato);
    if (opts.pfx) form.append("pfx", opts.pfx);
    if (opts.password) form.append("password", opts.password);
    if (opts.pin) form.append("pin", opts.pin);

    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;

    const res = await fetchAutenticata("/api/firma/cades", { method: "POST", body: form, headers });
    if (!res.ok) {
      let messaggio = `Errore ${res.status}`;
      try {
        const body = await res.json();
        if (body?.errore) messaggio = body.errore as string;
      } catch {
        /* corpo non JSON */
      }
      throw new Error(messaggio);
    }
    return { blob: await res.blob(), filename: nomeFileFirmato(file.name, formato) };
  },

  // Verifica un documento firmato (.p7m o XML): validità, formato, firmatario, data.
  verificaFirma: async (file: File | Blob, nome = "documento"): Promise<EsitoVerifica> => {
    const form = new FormData();
    form.append("file", file, nome);
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata("/api/firma/verifica", { method: "POST", body: form, headers });
    if (!res.ok) throw new Error(`Errore ${res.status}`);
    return (await res.json()) as EsitoVerifica;
  },

  // --- Deleghe intermediario ---
  deleghe: () => req<Delega[]>("/api/deleghe"),
  creaDelega: (d: { aziendaId: string; tipoAdempimento: string; dataInizio: string; riferimento?: string }) =>
    req<Delega>("/api/deleghe", { method: "POST", body: JSON.stringify(d) }),

  // --- Avvisi pagamento clienti / promemoria commercialista ---
  avvisiPagamento: (filtro: { aziendaId?: string; stato?: string; scaduti?: boolean } = {}) => {
    const qs = new URLSearchParams();
    if (filtro.aziendaId) qs.set("aziendaId", filtro.aziendaId);
    if (filtro.stato) qs.set("stato", filtro.stato);
    if (filtro.scaduti) qs.set("scaduti", "true");
    const suffix = qs.toString() ? `?${qs}` : "";
    return req<AvvisoPagamento[]>(`/api/avvisi-pagamento${suffix}`);
  },
  avvisiPagamentoAzienda: (aziendaId: string) =>
    req<AvvisoPagamento[]>(`/api/aziende/${aziendaId}/avvisi-pagamento`),
  creaAvvisoPagamento: (aziendaId: string, avviso: NuovoAvvisoPagamento) =>
    req<AvvisoPagamento>(`/api/aziende/${aziendaId}/avvisi-pagamento`, {
      method: "POST",
      body: JSON.stringify(avviso),
    }),
  creaAvvisiPagamentoMese: (avviso: NuovoAvvisoPagamento) =>
    req<{ creati: number; avvisi: AvvisoPagamento[] }>("/api/avvisi-pagamento/mese", {
      method: "POST",
      body: JSON.stringify(avviso),
    }),
  promemoriaPagamento: (giorni = 7) =>
    req<AvvisoPagamento[]>(`/api/avvisi-pagamento/promemoria?giorni=${giorni}`),
  testoAvvisoPagamento: (id: string) =>
    req<{ avviso: AvvisoPagamento; testo: string }>(`/api/avvisi-pagamento/${id}/avviso`),
  registraPromemoriaPagamento: (id: string) =>
    req<{ avviso: AvvisoPagamento; testo: string }>(`/api/avvisi-pagamento/${id}/promemoria`, {
      method: "POST",
    }),
  cambiaStatoAvvisoPagamento: (id: string, stato: StatoAvvisoPagamento) =>
    req<AvvisoPagamento>(`/api/avvisi-pagamento/${id}/stato`, {
      method: "POST",
      body: JSON.stringify(stato),
    }),

  // --- Bilancio d'esercizio (Stato Patrimoniale + Conto Economico, schema CEE) ---
  bilanci: (aziendaId: string) => req<BilancioSintesi[]>(`/api/aziende/${aziendaId}/bilanci`),
  bilancio: (id: string) => req<BilancioDettaglio>(`/api/bilanci/${id}`),
  creaBilancio: (aziendaId: string, b: NuovoBilancio) =>
    req<BilancioSintesi>(`/api/aziende/${aziendaId}/bilanci`, {
      method: "POST",
      body: JSON.stringify(b),
    }),
  creaBilancioEsempio: (aziendaId: string, anno: number) =>
    req<BilancioSintesi>(`/api/aziende/${aziendaId}/bilanci/esempio`, {
      method: "POST",
      body: JSON.stringify({ anno }),
    }),
  precompilaBilancio: (aziendaId: string, anno: number) =>
    req<PrecompilaBilancio>(`/api/aziende/${aziendaId}/bilanci/precompila?anno=${anno}`),
  cambiaStatoBilancio: (id: string, stato: string) =>
    req<{ id: string; stato: string }>(`/api/bilanci/${id}/stato`, {
      method: "POST",
      body: JSON.stringify({ stato }),
    }),
  // Scarica il fascicolo PDF del bilancio (SP + CE + dettaglio conti).
  bilancioPdf: async (id: string): Promise<{ blob: Blob; filename: string }> => {
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata(`/api/bilanci/${id}/pdf`, { headers });
    if (!res.ok) {
      let messaggio = `Errore ${res.status}`;
      try {
        const body = await res.json();
        if (body?.errore) messaggio = body.errore as string;
      } catch {
        /* corpo non JSON */
      }
      throw new Error(messaggio);
    }
    const dispo = res.headers.get("Content-Disposition") ?? "";
    const match = /filename="?([^"]+)"?/.exec(dispo);
    const filename = match?.[1] ?? `bilancio-${id}.pdf`;
    return { blob: await res.blob(), filename };
  },

  // --- Pratiche ComUnica (apertura/variazione/cessazione P.IVA e impresa) ---
  pratiche: () => req<PraticaComUnica[]>("/api/pratiche/apertura-partita-iva"),
  pratica: (id: string) => req<PraticaComUnica>(`/api/pratiche/apertura-partita-iva/${id}`),
  simulaPratica: (p: NuovaPraticaComUnica) =>
    req<PianoComUnica>("/api/pratiche/apertura-partita-iva/simula", {
      method: "POST",
      body: JSON.stringify(p),
    }),
  creaPratica: (p: NuovaPraticaComUnica) =>
    req<PraticaComUnica>("/api/pratiche/apertura-partita-iva", {
      method: "POST",
      body: JSON.stringify(p),
    }),
  aggiornaAdempimentoPratica: (praticaId: string, adempimentoId: string, stato: string, note?: string) =>
    req<AdempimentoPratica>(`/api/pratiche/apertura-partita-iva/${praticaId}/adempimenti/${adempimentoId}`, {
      method: "PATCH",
      body: JSON.stringify({ stato, note }),
    }),
  cambiaStatoPratica: (id: string, stato: string) =>
    req<PraticaComUnica>(`/api/pratiche/apertura-partita-iva/${id}/stato`, {
      method: "POST",
      body: JSON.stringify({ stato }),
    }),
  distintaPratica: (id: string) =>
    req<{ distinta: string }>(`/api/pratiche/apertura-partita-iva/${id}/distinta`),
  analizzaTestoPratica: (testo: string) =>
    req<DatiPraticaEstratti>("/api/pratiche/apertura-partita-iva/analizza-testo", {
      method: "POST",
      body: JSON.stringify({ testo }),
    }),

  // --- Scadenzario / promemoria (DURC, F24, manuali) ---
  scadenzario: () => req<Promemoria[]>("/api/scadenzario"),
  rigeneraScadenzario: () =>
    req<{ aggiornati: number }>("/api/scadenzario/rigenera", { method: "POST" }),
  creaPromemoria: (p: NuovoPromemoria) =>
    req<Promemoria>("/api/scadenzario", { method: "POST", body: JSON.stringify(p) }),
  cambiaStatoPromemoria: (id: string, stato: string) =>
    req<Promemoria>(`/api/scadenzario/${id}/stato`, {
      method: "POST",
      body: JSON.stringify({ stato }),
    }),
  durcAzienda: (aziendaId: string) => req<Durc[]>(`/api/aziende/${aziendaId}/durc`),
  registraDurc: (aziendaId: string, d: NuovoDurc) =>
    req<Durc>(`/api/aziende/${aziendaId}/durc`, { method: "POST", body: JSON.stringify(d) }),
  // Invia/prepara un promemoria su un canale: "Email" | "Sms" | "WhatsApp" | "Manuale" (omesso = default).
  inviaPromemoria: (id: string, canale?: string, destinatario?: string) =>
    req<EsitoInvioPromemoria>(`/api/scadenzario/${id}/invia`, {
      method: "POST",
      body: JSON.stringify({ canale, destinatario }),
    }),
  inviaAutomatiche: () =>
    req<{ inviati: number; saltati: number }>("/api/scadenzario/invia-automatiche", { method: "POST" }),
  caricaDocumentoScadenza: async (file: File, aziendaId?: string): Promise<EsitoCaricamentoScadenza> => {
    const form = new FormData();
    form.append("file", file);
    if (aziendaId) form.append("aziendaId", aziendaId);
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata("/api/documenti-scadenze", { method: "POST", body: form, headers });
    if (!res.ok) { const e = await res.json().catch(() => ({})); throw new Error((e as { errore?: string }).errore ?? `Errore ${res.status}`); }
    return (await res.json()) as EsitoCaricamentoScadenza;
  },

  // --- Configurazione SdI self-service (collega provider) ---
  configSdi: () => req<ConfigSdi>("/api/configurazione-sdi"),
  salvaConfigSdi: (c: SalvaConfigSdi) =>
    req<ConfigSdi>("/api/configurazione-sdi", { method: "PUT", body: JSON.stringify(c) }),
  verificaConfigSdi: () =>
    req<{ ok: boolean; messaggio: string }>("/api/configurazione-sdi/verifica", { method: "POST" }),

  // --- Dipendenti (anagrafiche reali per azienda) ---
  dipendenti: (aziendaId: string) =>
    req<DipendenteRapporto[]>(`/api/aziende/${aziendaId}/dipendenti`),
  creaDipendente: (aziendaId: string, d: CreaDipendente) =>
    req<{ rapportoId: string; dipendenteId: string; nome: string; cognome: string }>(
      `/api/aziende/${aziendaId}/dipendenti`,
      { method: "POST", body: JSON.stringify(d) },
    ),

  // --- Azienda da visura camerale (PDF): estrae i dati da confermare ---
  aziendaDaVisura: async (file: File): Promise<DatiVisura> => {
    const form = new FormData();
    form.append("file", file);
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata("/api/aziende/da-visura", { method: "POST", body: form, headers });
    if (!res.ok) {
      let messaggio = `Errore ${res.status}`;
      try {
        const body = await res.json();
        if (body?.errore) messaggio = body.errore as string;
      } catch {
        /* corpo non JSON */
      }
      throw new Error(messaggio);
    }
    return (await res.json()) as DatiVisura;
  },

  // --- Import UNILAV da PDF: legge il PDF e registra datore + lavoratore + rapporto ---
  importUnilavPdf: async (file: File): Promise<EsitoImportUnilav> => {
    const form = new FormData();
    form.append("file", file);
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata("/api/unilav/pdf", { method: "POST", body: form, headers });
    if (!res.ok) {
      let messaggio = `Errore ${res.status}`;
      try {
        const body = await res.json();
        if (body?.errore) messaggio = body.errore as string;
      } catch {
        /* corpo non JSON */
      }
      throw new Error(messaggio);
    }
    return (await res.json()) as EsitoImportUnilav;
  },

  // --- Import UNILAV da testo incollato: stessa registrazione, senza file ---
  importUnilavTesto: (
    testo: string,
    opzioni?: { aziendaId?: string; ccnlDefault?: string; livelloDefault?: string },
  ): Promise<EsitoImportUnilav> =>
    req<EsitoImportUnilav>("/api/unilav/testo", {
      method: "POST",
      body: JSON.stringify({
        testo,
        aziendaId: opzioni?.aziendaId || null,
        ccnlDefault: opzioni?.ccnlDefault || null,
        livelloDefault: opzioni?.livelloDefault || null,
      }),
    }),

  // --- Dichiarazioni: Fase 1 Certificazione Unica (bozza) ---
  cuBozza: (rapportoId: string, anno: number) =>
    req<CuBozza>(`/api/rapporti/${rapportoId}/cu/${anno}`),

  // --- Dichiarazioni: Fase 3 LIPE / IVA (bozza) ---
  lipeBozza: (aziendaId: string, anno: number, trimestre: number) =>
    req<LipeBozza>(`/api/aziende/${aziendaId}/lipe/${anno}/${trimestre}`),

  // --- Dichiarazioni: Fase 2 Modello 770 (bozza) ---
  mod770Bozza: (aziendaId: string, anno: number) =>
    req<Mod770Bozza>(`/api/aziende/${aziendaId}/modello770/${anno}`),

  // --- Dichiarazioni: Fase 3 IVA annuale (bozza) ---
  ivaAnnualeBozza: (aziendaId: string, anno: number) =>
    req<IvaAnnualeBozza>(`/api/aziende/${aziendaId}/iva-annuale/${anno}`),

  // --- Archivio persone (rappresentanti, soci): riusate per compilare i moduli ufficiali ---
  persone: () => req<Persona[]>("/api/persone"),
  creaPersona: (p: PersonaReq) =>
    req<Persona>("/api/persone", { method: "POST", body: JSON.stringify(p) }),
  aggiornaPersona: (id: string, p: PersonaReq) =>
    req<Persona>(`/api/persone/${id}`, { method: "PUT", body: JSON.stringify(p) }),
  eliminaPersona: (id: string) =>
    req<void>(`/api/persone/${id}`, { method: "DELETE" }),

  // --- Modello ufficiale AA7/10 (PDF Agenzia delle Entrate compilato) ---
  tabelleAa710: () => req<TabelleAa710>("/api/moduli/aa7-10/tabelle"),
  compilaAa710: async (richiesta: CompilaAa710Req): Promise<{ blob: Blob; filename: string }> => {
    const token = getToken();
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetchAutenticata("/api/moduli/aa7-10/pdf", {
      method: "POST",
      headers,
      body: JSON.stringify(richiesta),
    });
    if (!res.ok) {
      let messaggio = `Errore ${res.status}`;
      try {
        const body = await res.json();
        if (body?.errore) messaggio = body.errore as string;
        else if (Array.isArray(body?.errori)) messaggio = (body.errori as string[]).join(" ");
      } catch {
        /* corpo non JSON */
      }
      throw new Error(messaggio);
    }
    const match = /filename="?([^";]+)"?/.exec(res.headers.get("content-disposition") ?? "");
    return { blob: await res.blob(), filename: match?.[1] ?? "AA7-10.pdf" };
  },

  // --- Governor AI (G2): mandati, accessi governati, revoca, trasparenza ---

  aiEmettiGrant: (richiesta: RichiestaGrantAi) =>
    req<GrantAiEmesso>("/api/ai/governor/grants", { method: "POST", body: JSON.stringify(richiesta) }),

  /** Ogni lettura di Aurora passa da qui: 403 = negato (il motivo arriva nel corpo). */
  aiAccesso: async (lease: string, risorsa: string, operazione: string): Promise<EsitoAccessoAi> => {
    try {
      return await req<EsitoAccessoAi>("/api/ai/governor/accesso", {
        method: "POST",
        body: JSON.stringify({ lease, risorsa, operazione }),
      });
    } catch (e) {
      if (e instanceof ApiError && e.status === 403 && e.corpo && "consentito" in e.corpo)
        return e.corpo as unknown as EsitoAccessoAi;
      throw e;
    }
  },

  /** Query broker: permesso + dati minimizzati in un passo; scope dal mandato, non dal client. */
  aiQuery: async <T = unknown>(lease: string, risorsa: string): Promise<EsitoQueryAi<T>> => {
    try {
      return await req<EsitoQueryAi<T>>("/api/ai/governor/query", {
        method: "POST",
        body: JSON.stringify({ lease, risorsa }),
      });
    } catch (e) {
      if (e instanceof ApiError && e.status === 403 && e.corpo && "consentito" in e.corpo)
        return e.corpo as unknown as EsitoQueryAi<T>;
      throw e;
    }
  },

  aiRevocaGrant: (grantId: string) =>
    req<{ revocato: boolean }>(`/api/ai/governor/grants/${grantId}/revoca`, { method: "POST" }),

  aiArresta: () => req<{ revocati: number }>("/api/ai/governor/arresta", { method: "POST" }),

  aiCosaVede: (grantId: string) =>
    req<CosaVedeAi>(`/api/ai/governor/grants/${grantId}`),

  // --- Posta (email/PEC): caselle collegate self-service, il triage AI resta sempre da confermare ---
  caselleEmail: () => req<CasellaEmail[]>("/api/posta/caselle"),
  creaCasellaEmail: (c: NuovaCasellaEmail) =>
    req<CasellaEmail>("/api/posta/caselle", { method: "POST", body: JSON.stringify(c) }),
  eliminaCasellaEmail: (id: string) =>
    req<void>(`/api/posta/caselle/${id}`, { method: "DELETE" }),
  sincronizzaCasellaEmail: (id: string) =>
    req<EsitoSincronizzazioneEmail>(`/api/posta/caselle/${id}/sincronizza`, { method: "POST" }),
  messaggiEmail: (filtri?: FiltriMessaggiEmail) => {
    const params = new URLSearchParams();
    if (filtri?.casellaId) params.set("casellaId", filtri.casellaId);
    if (filtri?.letto !== undefined) params.set("letto", String(filtri.letto));
    if (filtri?.urgente !== undefined) params.set("urgente", String(filtri.urgente));
    if (filtri?.pubblicita !== undefined) params.set("pubblicita", String(filtri.pubblicita));
    if (filtri?.soloProposte) params.set("soloProposte", "true");
    const qs = params.toString();
    return req<MessaggioEmail[]>(`/api/posta/messaggi${qs ? `?${qs}` : ""}`);
  },
  corpoMessaggioEmail: (id: string) =>
    req<{ corpo: string | null; errore: string | null }>(`/api/posta/messaggi/${id}/corpo`),
  marcaLettoEmail: (id: string, letto: boolean) =>
    req<void>(`/api/posta/messaggi/${id}/letto`, { method: "POST", body: JSON.stringify({ letto }) }),
  proponiTriageEmail: (id: string) =>
    req<MessaggioEmail>(`/api/posta/messaggi/${id}/proponi-ai`, { method: "POST" }),
  confermaTriageEmail: (id: string, urgente?: boolean | null, pubblicita?: boolean | null) =>
    req<void>(`/api/posta/messaggi/${id}/conferma`, {
      method: "POST",
      body: JSON.stringify({ urgente, pubblicita }),
    }),
};

// --- Tipi del Governor AI ---

export interface RichiestaGrantAi {
  purposeCode: string;
  modalita: "Stretta" | "Guidata";
  scopeLevel: "S0" | "S1" | "S2";
  aziendaId?: string | null;
  periodo?: string | null;
  risorse: string[];
  operazioni: string[];
  durataMinuti?: number;
  maxLetture?: number;
}

export interface ManifestGrantAi {
  grantId: string;
  purposeCode: string;
  modalita: string;
  scopeLevel: string;
  aziendaId: string | null;
  periodo: string | null;
  risorse: string[];
  operazioni: string[];
  autonomiaMax: string;
  scadeUtc: string;
  maxLetture: number;
  lettureUsate: number;
  stato: string;
  descrizione: string;
}

export interface GrantAiEmesso {
  manifest: ManifestGrantAi;
  lease: string;
}

export interface EsitoAccessoAi {
  consentito: boolean;
  motivo: string | null;
  lettureRimanenti: number;
}

export interface CosaVedeAi {
  manifest: ManifestGrantAi;
  eventi: { sequenza: number; evento: string; dettaglio: string; motivo: string | null; quandoUtc: string }[];
}

export interface EsitoQueryAi<T = unknown> {
  consentito: boolean;
  motivo: string | null;
  risorsa: string;
  righe: T[];
  totale: number;
  lettureRimanenti: number;
}

export interface RigaScadenzaAi { titolo: string; urgenza: string; giorniAllaScadenza: number }
export interface RigaAziendaAi {
  ragioneSociale: string;
  matricolaInpsPresente: boolean;
  patInailPresente: boolean;
  codiceCatastalePresente: boolean;
  ibanPresente: boolean;
  recapitoFiscalePresente: boolean;
}
export interface RigaDipendenteAi { nominativo: string; codiceCcnl: string | null; livello: string | null }
export interface RigaCartellinoAi {
  nominativo: string;
  elaborato: boolean;
  giorniMalattia: number;
  numeroEventoMalattiaAnno: number;
}

export interface Persona {
  id: string; cognome: string; nome: string; codiceFiscale: string;
  sesso?: string | null; dataNascita?: string | null;
  comuneNascita?: string | null; provinciaNascita?: string | null;
  indirizzoResidenza?: string | null; capResidenza?: string | null;
  comuneResidenza?: string | null; provinciaResidenza?: string | null;
  telefono?: string | null; email?: string | null;
}
export interface PersonaReq {
  cognome: string; nome: string; codiceFiscale: string;
  sesso?: string; dataNascita?: string;
  comuneNascita?: string; provinciaNascita?: string;
  indirizzoResidenza?: string; capResidenza?: string;
  comuneResidenza?: string; provinciaResidenza?: string;
  telefono?: string; email?: string;
}
export interface VoceTabellaAa710 { codice: string; descrizione: string; }
export interface TabelleAa710 {
  natureGiuridiche: VoceTabellaAa710[];
  codiciCarica: VoceTabellaAa710[];
}
export interface CompilaAa710Req {
  aziendaId: string; personaId: string;
  tipoDichiarazione: number; data: string;
  naturaGiuridica: string; codiceCarica?: string;
  sigla?: string; scrittureContabiliInSede?: boolean;
  descrizioneAttivita?: string; volumeAffariPresunto?: number;
  dataInizioProcedimento?: string;
  estremiRegistrazione?: {
    data?: string; ufficio?: string; numero?: string;
    sottoNumero?: string; serie?: string;
  };
}

export interface IvaAnnualeBozza {
  anno: number; numeroLiquidazioni: number;
  totaleIvaVendite: number; totaleIvaAcquistiDetraibile: number;
  totaleIvaDovuta: number; totaleIvaCredito: number; saldoAnnuale: number;
  periodi: LipeModulo[];
}

export interface Mod770Percipiente {
  nominativo: string; codiceFiscale: string;
  ritenuteIrpef: number; addizionaleRegionale: number; addizionaleComunale: number;
}
export interface Mod770Bozza {
  anno: number; numeroPercipienti: number; numeroCedolini: number;
  totaleRitenuteIrpef: number; totaleAddizionaleRegionale: number; totaleAddizionaleComunale: number;
  percipienti: Mod770Percipiente[];
}

export interface LipeModulo {
  periodo: string; ivaEsigibile: number; ivaDetratta: number; ivaDovuta: number; ivaCredito: number;
}
export interface LipeBozza {
  anno: number; trimestre: number; moduli: LipeModulo[]; totaleDovuta: number; totaleCredito: number;
}

export interface CuBozza {
  codiceFiscale: string; nominativo: string; anno: number; numeroCedolini: number;
  totaleImponibileIrpef: number; totaleRitenuteIrpef: number;
  totaleAddizionaleRegionale: number; totaleAddizionaleComunale: number;
  totaleImponibileInps: number; totaleNetto: number;
}

export interface DipendenteRapporto {
  rapportoId: string; dipendenteId: string; nominativo: string;
  codiceCcnl: string; livello: string; qualifica: string;
}
export interface CreaDipendente {
  codiceFiscale: string; nome: string; cognome: string;
  codiceCcnl: string; livello: string; iban?: string; qualifica?: string;
  regione?: string; codiceCatastaleComune?: string;
  // Inquadramento e retribuzione (default lato server se omessi).
  dataAssunzione?: string; tipoContratto?: string; oreSettimanali?: number;
  numeroMensilita?: number; partTime?: boolean; percentualePartTime?: number;
  scattiAnzianita?: number; superminimo?: number;
}

/** Un dato che manca (o va confermato) dopo l'import, con dove completarlo e dove trovarlo. */
export interface CampoDaCompletare {
  campo: string;
  dove: "azienda" | "dipendente" | "rapporto" | string;
  riferimentoId?: string | null;
  perche: string;
  doveTrovarlo: string;
}

export interface EsitoImportUnilav {
  azienda: {
    id: string; ragioneSociale: string;
    partitaIva?: string | null; codiceFiscale?: string | null; creata: boolean;
  };
  rapporto: {
    id: string;
    dipendente: { id: string; nome: string; cognome: string; codiceFiscale: string };
    codiceCcnl: string; livello: string; qualifica: string;
    tipoContratto?: string; partTime?: boolean; percentualePartTime?: number;
    oreSettimanali?: number;
    dataAssunzione: string; dataCessazione?: string | null; lavoratoreGiaPresente: boolean;
  };
  avvertenze: string[];
  daCompletare: CampoDaCompletare[];
}

export interface ConfigSdi {
  modalita: string; provider: string; baseUrl: string; apiKeyImpostata: boolean;
  endpointInvio: string; endpointEsito: string; campoId: string; campoStato: string;
  firmaLocale: boolean; attivo: boolean;
}
export interface SalvaConfigSdi {
  modalita: string; provider?: string; baseUrl?: string; apiKey?: string | null;
  endpointInvio?: string; endpointEsito?: string; campoId?: string; campoStato?: string;
  firmaLocale: boolean; attivo: boolean;
}

// --- Tipi Posta (email/PEC) ---

export interface CasellaEmail {
  id: string; etichetta: string; tipo: string; host: string; porta: number; utente: string;
  ssl: boolean; attiva: boolean; passwordImpostata: boolean; ultimaSincronizzazioneIl: string | null;
}
export interface NuovaCasellaEmail {
  etichetta: string; tipo: string; host: string; porta: number;
  utente: string; password?: string; ssl: boolean; attiva: boolean;
}
export interface EsitoSincronizzazioneEmail { nuoviMessaggi: number; errore: string | null }
export interface MessaggioEmail {
  id: string; casellaId: string; mittente: string; oggetto: string; dataRicezione: string;
  anteprima: string; letto: boolean; urgente: boolean | null; pubblicita: boolean | null;
  urgenteProposto: boolean | null; pubblicitaProposto: boolean | null;
  confidenzaUrgente: number | null; confidenzaPubblicita: number | null; fonteProposta: string | null;
}
export interface FiltriMessaggiEmail {
  casellaId?: string; letto?: boolean; urgente?: boolean; pubblicita?: boolean; soloProposte?: boolean;
}
