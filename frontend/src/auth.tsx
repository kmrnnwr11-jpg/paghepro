import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState,
  type ReactNode,
} from "react";
import { api } from "./api";
import { dimenticaAziendaCorrente } from "./aziendaCorrente";
import { buildMobile } from "./mobile/piattaforma";
import {
  applicaSessione,
  getAccessToken,
  rinnovaSessione,
  terminaSessione,
  type SessioneAccesso,
} from "./sessione";

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
  esci: () => Promise<void>;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTok] = useState<string | null>(null);
  const [ingressoInCorso, setIngressoInCorso] = useState(true);

  const applica = useCallback(async (sessione: SessioneAccesso) => {
    localStorage.removeItem(USCITA_ESPLICITA); // sei dentro: al prossimo avvio non chiedere di nuovo
    await applicaSessione(sessione);
    setTok(sessione.token);
    setIngressoInCorso(false);
  }, []);

  const accedi = useCallback(
    async (email: string, password: string, codice2fa?: string) => {
      const r = await api.login(email, password, codice2fa);
      await applica(r);
    },
    [applica],
  );

  const registrati = useCallback(
    async (ragioneSociale: string, email: string, password: string) => {
      const r = await api.registra(ragioneSociale, email, password);
      await applica(r);
    },
    [applica],
  );

  const provaDemo = useCallback(async () => {
    if (buildMobile)
      throw new Error("La demo automatica non crea account nella versione mobile. Accedi o registrati.");
    // Demo isolata per browser: entra nel proprio account demo, o lo crea al volo la prima volta.
    const email = emailDemoDiQuestoBrowser();
    let r;
    try {
      r = await api.login(email, DEMO_PASSWORD);
    } catch {
      r = await api.registra("Prova demo", email, DEMO_PASSWORD);
    }
    await applica(r);
  }, [applica]);

  const esci = useCallback(async () => {
    // Uscita voluta: ricordala, così al prossimo avvio mostriamo il login invece di rientrare.
    localStorage.setItem(USCITA_ESPLICITA, "1");
    dimenticaAziendaCorrente();
    try {
      await terminaSessione();
    } finally {
      setTok(null);
      setIngressoInCorso(false);
    }
  }, []);

  // All'avvio prova prima la sessione protetta. Solo nel web, se non esiste e l'utente non
  // aveva premuto Esci, può ancora creare lo spazio demo isolato.
  const avviato = useRef(false);
  useEffect(() => {
    if (avviato.current) return;
    avviato.current = true;
    void (async () => {
      try {
        const sessione = await rinnovaSessione();
        setTok(sessione.token);
      } catch {
        if (!buildMobile && localStorage.getItem(USCITA_ESPLICITA) !== "1") {
          try {
            await provaDemo();
            return;
          } catch {
            // Motore non raggiungibile: mostra comunque la schermata di accesso.
          }
        }
      } finally {
        setIngressoInCorso(false);
      }
    })();
  }, [provaDemo]);

  useEffect(() => {
    const sincronizza = () => setTok(getAccessToken());
    window.addEventListener("paghepro:sessione", sincronizza);
    return () => window.removeEventListener("paghepro:sessione", sincronizza);
  }, []);

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
