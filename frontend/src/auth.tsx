import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { api, getToken, setToken } from "./api";

// Credenziali dell'account demo condiviso: permettono a chiunque di provare l'app
// senza registrarsi. Per l'uso personale "ognuno il suo spazio" si usa Accedi / Crea account.
const DEMO_EMAIL = "demo@paghe.local";
const DEMO_PASSWORD = "demo-password-1";

interface AuthState {
  token: string | null;
  /** Accede a un account esistente con email + password (+ codice 2FA se attivo). */
  accedi: (email: string, password: string, codice2fa?: string) => Promise<void>;
  /** Crea un nuovo account personale (spazio privato isolato) ed entra. */
  registrati: (ragioneSociale: string, email: string, password: string) => Promise<void>;
  /** Entra nell'account demo condiviso, solo per provare. */
  provaDemo: () => Promise<void>;
  /** Esce: cancella il token e torna alla schermata di accesso. */
  esci: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTok] = useState<string | null>(getToken());

  const applica = useCallback((nuovoToken: string) => {
    setToken(nuovoToken);
    setTok(nuovoToken);
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
    // L'account demo potrebbe non esistere ancora: in tal caso lo si crea al volo.
    let r;
    try {
      r = await api.login(DEMO_EMAIL, DEMO_PASSWORD);
    } catch {
      r = await api.registra("Account Demo", DEMO_EMAIL, DEMO_PASSWORD);
    }
    applica(r.token);
  }, [applica]);

  const esci = useCallback(() => {
    setToken(null);
    setTok(null);
  }, []);

  const value = useMemo<AuthState>(
    () => ({ token, accedi, registrati, provaDemo, esci }),
    [token, accedi, registrati, provaDemo, esci],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth deve essere usato dentro AuthProvider");
  return ctx;
}
