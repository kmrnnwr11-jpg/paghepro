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
};
