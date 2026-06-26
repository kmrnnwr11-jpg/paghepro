import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, getToken, setToken } from "./api";

// Credenziali dell'account demo: l'app vi accede in automatico, senza pagina di login.
const DEMO_EMAIL = "demo@paghe.local";
const DEMO_PASSWORD = "demo-password-1";

interface AuthState {
  token: string | null;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTok] = useState<string | null>(getToken());
  const [pronto, setPronto] = useState<boolean>(!!getToken());
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    if (token) return;
    let annullato = false;
    (async () => {
      try {
        let r;
        try {
          r = await api.login(DEMO_EMAIL, DEMO_PASSWORD);
        } catch {
          r = await api.registra("Account Demo", DEMO_EMAIL, DEMO_PASSWORD);
        }
        if (!annullato) {
          setToken(r.token);
          setTok(r.token);
        }
      } catch (e) {
        if (!annullato) setErrore(e instanceof Error ? e.message : "Errore di accesso");
      } finally {
        if (!annullato) setPronto(true);
      }
    })();
    return () => { annullato = true; };
  }, [token]);

  const value = useMemo<AuthState>(() => ({ token }), [token]);

  if (!pronto) {
    return (
      <div className="centro">
        <div className="card">{errore ? <span className="errore">{errore}</span> : "Caricamento…"}</div>
      </div>
    );
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth deve essere usato dentro AuthProvider");
  return ctx;
}
