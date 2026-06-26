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

  // --- Firma digitale CAdES (.p7m) ---
  // Firma un file: con certificato software (pfx + password) oppure con smart card/CNS (pin).
  // Risponde con la busta .p7m come blob da scaricare.
  firmaCades: async (
    file: File,
    opts: { pfx?: File; password?: string; pin?: string } = {},
  ): Promise<{ blob: Blob; filename: string }> => {
    const form = new FormData();
    form.append("file", file);
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
    const blob = await res.blob();
    // Il backend nomina il file aggiungendo ".p7m" al nome originale.
    return { blob, filename: `${file.name}.p7m` };
  },

  // --- Deleghe intermediario ---
  deleghe: () => req<Delega[]>("/api/deleghe"),
  creaDelega: (d: { aziendaId: string; tipoAdempimento: string; dataInizio: string; riferimento?: string }) =>
    req<Delega>("/api/deleghe", { method: "POST", body: JSON.stringify(d) }),
};
