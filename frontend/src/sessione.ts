import { SecureSession } from "@paghepro/secure-session";
import { buildMobile } from "./mobile/piattaforma";
import { pulisciCachePreferenzeCorrenti } from "./preferenze";

const BASE = (import.meta.env.VITE_API_URL as string | undefined)?.trim()
  || (import.meta.env.DEV ? "http://localhost:5171" : "");
const MARGINE_RINNOVO_MS = 45_000;

export interface SessioneAccesso {
  token: string;
  scadeUtc: string;
  tenantId: string;
  ruolo: string;
  refreshToken?: string;
  refreshScadeUtc?: string;
}

let accessToken: string | null = null;
let accessScadeMs = 0;
let rinnovoInCorso: Promise<SessioneAccesso> | null = null;

function utenteDalToken(token: string): string | null {
  try {
    const parte = token.split(".")[1];
    if (!parte) return null;
    const base64 = parte.replace(/-/g, "+").replace(/_/g, "/")
      .padEnd(Math.ceil(parte.length / 4) * 4, "=");
    const payload = JSON.parse(atob(base64)) as { sub?: unknown };
    return typeof payload.sub === "string" && payload.sub.trim() ? payload.sub.trim() : null;
  } catch {
    return null;
  }
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function accessoInScadenza(): boolean {
  return !!accessToken && Date.now() + MARGINE_RINNOVO_MS >= accessScadeMs;
}

export async function applicaSessione(sessione: SessioneAccesso): Promise<void> {
  const utenteId = utenteDalToken(sessione.token);
  if (!utenteId) throw new Error("La sessione non identifica l'utente.");
  if (buildMobile) {
    if (!sessione.refreshToken)
      throw new Error("Il server non ha restituito la sessione mobile sicura.");
    // Prima rendiamo persistente il nuovo refresh ruotato, poi pubblichiamo il JWT in memoria.
    await SecureSession.set({ value: sessione.refreshToken });
  }
  accessToken = sessione.token;
  accessScadeMs = Date.parse(sessione.scadeUtc);
  localStorage.setItem("ruolo", sessione.ruolo);
  localStorage.setItem("tenant_corrente", sessione.tenantId);
  localStorage.setItem("utente_corrente", utenteId);
  window.dispatchEvent(new CustomEvent("paghepro:sessione", { detail: { autenticato: true } }));
}

export async function cancellaSessioneLocale(): Promise<void> {
  accessToken = null;
  accessScadeMs = 0;
  pulisciCachePreferenzeCorrenti();
  localStorage.removeItem("ruolo");
  localStorage.removeItem("tenant_corrente");
  localStorage.removeItem("utente_corrente");
  // Rimuove anche l'eventuale residuo della vecchia implementazione vulnerabile.
  localStorage.removeItem("token");
  if (buildMobile) await SecureSession.clear().catch(() => undefined);
  window.dispatchEvent(new CustomEvent("paghepro:sessione", { detail: { autenticato: false } }));
}

async function leggiErrore(res: Response): Promise<Error> {
  try {
    const corpo = await res.json() as { errore?: string };
    return new Error(corpo.errore || `Errore ${res.status}`);
  } catch {
    return new Error(`Errore ${res.status}`);
  }
}

async function eseguiRinnovo(): Promise<SessioneAccesso> {
  let res: Response;
  if (buildMobile) {
    const { value } = await SecureSession.get();
    if (!value) throw new Error("Nessuna sessione salvata.");
    res = await fetch(`${BASE}/api/auth/mobile/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: value }),
    });
  } else {
    res = await fetch(`${BASE}/api/auth/refresh`, {
      method: "POST",
      credentials: "include",
      headers: { "X-PaghePro-Client": "web" },
    });
  }

  if (!res.ok) throw await leggiErrore(res);
  const sessione = await res.json() as SessioneAccesso;
  await applicaSessione(sessione);
  return sessione;
}

async function rinnovoCoordinato(): Promise<SessioneAccesso> {
  if (!buildMobile && typeof navigator !== "undefined" && navigator.locks) {
    // Il cookie è condiviso tra schede: il Web Lock impedisce che due schede consumino
    // contemporaneamente lo stesso refresh monouso.
    return navigator.locks.request("paghepro-refresh", () => eseguiRinnovo());
  }
  return eseguiRinnovo();
}

export function rinnovaSessione(): Promise<SessioneAccesso> {
  if (!rinnovoInCorso) {
    rinnovoInCorso = rinnovoCoordinato()
      .catch(async (errore) => {
        await cancellaSessioneLocale();
        throw errore;
      })
      .finally(() => { rinnovoInCorso = null; });
  }
  return rinnovoInCorso;
}

export async function terminaSessione(): Promise<void> {
  try {
    if (buildMobile) {
      const { value } = await SecureSession.get();
      if (value) {
        await fetch(`${BASE}/api/auth/mobile/logout`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken: value }),
        });
      }
    } else {
      await fetch(`${BASE}/api/auth/logout`, {
        method: "POST",
        credentials: "include",
        headers: { "X-PaghePro-Client": "web" },
      });
    }
  } finally {
    await cancellaSessioneLocale();
  }
}
