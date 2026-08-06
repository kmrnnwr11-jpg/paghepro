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

// Password fissa dello spazio ospite. L'email invece è UNA PER BROWSER (vedi
// emailOspiteDiQuestoBrowser): così chi entra come ospite ha uno spazio isolato e non
// vede né condivide i dati di altri. Per l'uso vero si usa Accedi / Crea account.
const PASSWORD_OSPITE = "demo-password-1";
const NOME_OSPITE = "Ospite";
const CHIAVE_EMAIL_OSPITE = "demo_email";

// Ricorda se l'utente è uscito di PROPOSITO (tasto Esci): in quel caso all'avvio
// mostriamo la schermata di accesso e NON rientriamo in automatico. Senza questo
// flag, invece, l'app si apre subito nello spazio personale (login opzionale).
const USCITA_ESPLICITA = "uscita_esplicita";

// Apre un nuovo spazio ospite per questo browser e lo ricorda.
function nuovaEmailOspite(): string {
  const id = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const email = `demo-${id}@paghe.local`;
  localStorage.setItem(CHIAVE_EMAIL_OSPITE, email);
  return email;
}

// Spazio ospite isolato di questo browser: lo crea una volta sola e lo riusa.
function emailOspiteDiQuestoBrowser(): string {
  return localStorage.getItem(CHIAVE_EMAIL_OSPITE) ?? nuovaEmailOspite();
}

/** true se l'account è uno spazio ospite aperto senza registrazione. */
export function eAccountOspite(email: string): boolean {
  return /^demo-.+@paghe\.local$/i.test(email.trim());
}

interface AuthState {
  token: string | null;
  /** true finché stiamo entrando in automatico all'avvio: nessuna schermata di login. */
  ingressoInCorso: boolean;
  /** Accede a un account esistente con email + password (+ codice 2FA se attivo). */
  accedi: (email: string, password: string, codice2fa?: string) => Promise<void>;
  /** Crea un nuovo account personale (spazio privato isolato) ed entra. */
  registrati: (ragioneSociale: string, email: string, password: string) => Promise<void>;
  /** Entra come ospite, senza registrarsi: spazio isolato, uno per browser. */
  entraComeOspite: () => Promise<void>;
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

  const entraComeOspite = useCallback(async () => {
    if (buildMobile)
      throw new Error("L'ingresso come ospite non è disponibile nella versione mobile. Accedi o registrati.");
    // Spazio ospite isolato per browser: entra nel proprio, o lo crea al volo la prima volta.
    const apri = async (email: string) => {
      try {
        return await api.login(email, PASSWORD_OSPITE);
      } catch {
        return await api.registra(NOME_OSPITE, email, PASSWORD_OSPITE);
      }
    };
    let r;
    try {
      r = await apri(emailOspiteDiQuestoBrowser());
    } catch {
      // Lo spazio ospite ricordato non è più utilizzabile (account rimosso, email già
      // presa, errore del server): ne apriamo uno nuovo invece di lasciare bloccati.
      r = await apri(nuovaEmailOspite());
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
  // aveva premuto Esci, può ancora aprire lo spazio ospite isolato.
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
            await entraComeOspite();
            return;
          } catch {
            // Motore non raggiungibile: mostra comunque la schermata di accesso.
          }
        }
      } finally {
        setIngressoInCorso(false);
      }
    })();
  }, [entraComeOspite]);

  useEffect(() => {
    const sincronizza = () => setTok(getAccessToken());
    window.addEventListener("paghepro:sessione", sincronizza);
    return () => window.removeEventListener("paghepro:sessione", sincronizza);
  }, []);

  const value = useMemo<AuthState>(
    () => ({ token, ingressoInCorso, accedi, registrati, entraComeOspite, esci }),
    [token, ingressoInCorso, accedi, registrati, entraComeOspite, esci],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth deve essere usato dentro AuthProvider");
  return ctx;
}
