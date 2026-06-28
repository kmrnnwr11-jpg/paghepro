// Client API tipizzato verso il backend Paghe SaaS.

const BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? "http://localhost:5171";

export function getToken(): string | null {
  return localStorage.getItem("token");
}
export function setToken(token: string | null): void {
  if (token) localStorage.setItem("token", token);
  else localStorage.removeItem("token");
}

async function req<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...((options.headers as Record<string, string>) ?? {}),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetch(`${BASE}${path}`, { ...options, headers });

  // Sessione non valida/scaduta: pulisci il token e ricarica (riparte l'accesso automatico).
  if (res.status === 401 && token && !path.startsWith("/api/auth/")) {
    setToken(null);
    location.reload();
  }

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
  if (res.status === 204) return undefined as T;
  const ct = res.headers.get("content-type") ?? "";
  return ct.includes("application/json") ? ((await res.json()) as T) : ((await res.text()) as unknown as T);
}

// --- Tipi ---
export interface AuthResponse { token: string; tenantId: string; ruolo: string }
export interface Azienda { id: string; ragioneSociale: string }
export interface NuovaAzienda {
  ragioneSociale: string; partitaIva?: string; indirizzo: string;
  cap: string; comune: string; provincia: string;
}
export interface Livello { codice: string; descrizione: string; minimoTabellare: number }
export interface Rapporto {
  codiceCcnl: string; livello: string; qualifica: string; numeroMensilita: number;
  scattiAnzianita: number; regione: string; codiceCatastaleComune: string;
}
export interface Cartellino { anno: number; mese: number; oreStraordinario: number }
export interface VoceCedolino { codice: string; descrizione: string; tipo: string; importo: number }
export interface Cedolino {
  totaleCompetenze: number; totaleTrattenute: number; nettoInBusta: number;
  costoAzienda: number; quotaTfr: number; righe: VoceCedolino[];
}
export interface CedolinoArchiviato {
  id: string; anno: number; mese: number;
  nettoInBusta: number; costoAzienda: number;
  imponibileIrpef?: number; irpefNetta?: number;
}
export interface PresenzaRiga {
  rapportoId: string; nominativo: string; oreStraordinario: number; elaborato: boolean; netto: number;
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
  registra: (ragioneSociale: string, email: string, password: string) =>
    req<AuthResponse>("/api/auth/registra", {
      method: "POST",
      body: JSON.stringify({ ragioneSociale, email, password }),
    }),
  login: (email: string, password: string) =>
    req<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),
  aziende: () => req<Azienda[]>("/api/aziende"),
  creaAzienda: (a: NuovaAzienda) =>
    req<{ id: string; ragioneSociale: string }>("/api/aziende", {
      method: "POST",
      body: JSON.stringify(a),
    }),
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
  archiviaCedolino: (rapportoId: string, cartellino: Cartellino) =>
    req<CedolinoArchiviato>(`/api/rapporti/${rapportoId}/cedolini`, {
      method: "POST",
      body: JSON.stringify(cartellino),
    }),
  rapportoDettaglio: (rapportoId: string) =>
    req<RapportoDettaglio>(`/api/rapporti/${rapportoId}`),
  presenzeMese: (aziendaId: string, anno: number, mese: number) =>
    req<PresenzeMese>(`/api/aziende/${aziendaId}/presenze/${anno}/${mese}`),

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

    const res = await fetch(`${BASE}/api/firma/cades`, { method: "POST", body: form, headers });
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
    const res = await fetch(`${BASE}/api/firma/verifica`, { method: "POST", body: form, headers });
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
};

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
