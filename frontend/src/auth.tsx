import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState,
  type ReactNode,
} from "react";
import { api, getToken, setToken } from "./api";

// Password fissa dell'account demo. L'email invece è UNA PER BROWSER (vedi
// emailDemoDiQuestoBrowser): così chi prova la demo ha uno spazio isolato e non
// vede né condivide i dati di altri. Per l'uso vero si usa Accedi / Crea account.
const DEMO_PASSWORD = "demo-password-1";

// Ricorda se l'utente è uscito di PROPOSITO (tasto Esci): in quel caso all'avvio
// mostriamo la schermata di accesso e NON rientriamo in automatico. Senza questo
// flag, invece, l'app si apre subito nello spazio personale (login opzionale).
const USCITA_ESPLICITA = "uscita_esplicita";

// Email demo isolata di questo browser: la crea una volta sola e la riusa.
function emailDemoDiQuestoBrowser(): string {
  const chiave = "demo_email";
  let email = localStorage.getItem(chiave);
  if (!email) {
    const id = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    email = `demo-${id}@paghe.local`;
    localStorage.setItem(chiave, email);
  }
  return email;
}

interface AuthState {
  token: string | null;
  /** true finché stiamo entrando in automatico all'avvio: nessuna schermata di login. */
  ingressoInCorso: boolean;
  /** Accede a un account esistente con email + password (+ codice 2FA se attivo). */
  accedi: (email: string, password: string, codice2fa?: string) => Promise<void>;
  /** Crea un nuovo account personale (spazio privato isolato) ed entra. */
  registrati: (ragioneSociale: string, email: string, password: string) => Promise<void>;
  /** Entra in un account demo isolato (uno per browser), solo per provare. */
  provaDemo: () => Promise<void>;
  /** Esce: cancella il token e torna alla schermata di accesso. */
  esci: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTok] = useState<string | null>(getToken());
  // All'avvio, se non c'è già un token e l'utente non è uscito di proposito, entriamo
  // in automatico nello spazio personale: l'app si apre subito, senza muro di login.
  const [ingressoInCorso, setIngressoInCorso] = useState<boolean>(
    () => !getToken() && localStorage.getItem(USCITA_ESPLICITA) !== "1",
  );

  const applica = useCallback((nuovoToken: string) => {
    localStorage.removeItem(USCITA_ESPLICITA); // sei dentro: al prossimo avvio non chiedere di nuovo
    setToken(nuovoToken);
    setTok(nuovoToken);
    setIngressoInCorso(false);
  }, []);

  const accedi = useCallback(
    async (email: string, password: string, codice2fa?: string) => {
      const r = await api.login(email, password, codice2fa);
      applica(r.token);
    },
    [applica],
  );

  const registrati = useCallback(
    async (ragioneSociale: string, email: string, password: string) => {
      const r = await api.registra(ragioneSociale, email, password);
      applica(r.token);
    },
    [applica],
  );

  const provaDemo = useCallback(async () => {
    // Demo isolata per browser: entra nel proprio account demo, o lo crea al volo la prima volta.
    const email = emailDemoDiQuestoBrowser();
    let r;
    try {
      r = await api.login(email, DEMO_PASSWORD);
    } catch {
      r = await api.registra("Prova demo", email, DEMO_PASSWORD);
    }
    applica(r.token);
  }, [applica]);

  const esci = useCallback(() => {
    // Uscita voluta: ricordala, così al prossimo avvio mostriamo il login invece di rientrare.
    localStorage.setItem(USCITA_ESPLICITA, "1");
    setToken(null);
    setTok(null);
    setIngressoInCorso(false);
  }, []);

  // Ingresso automatico all'avvio (una volta sola): apre subito lo spazio personale.
  const avviato = useRef(false);
  useEffect(() => {
    if (avviato.current || !ingressoInCorso) return;
    avviato.current = true;
    void (async () => {
      try {
        await provaDemo();
      } catch {
        // Motore non raggiungibile: mostra comunque la schermata di accesso.
        setIngressoInCorso(false);
      }
    })();
  }, [ingressoInCorso, provaDemo]);

  const value = useMemo<AuthState>(
    () => ({ token, ingressoInCorso, accedi, registrati, provaDemo, esci }),
    [token, ingressoInCorso, accedi, registrati, provaDemo, esci],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth deve essere usato dentro AuthProvider");
  return ctx;
}
