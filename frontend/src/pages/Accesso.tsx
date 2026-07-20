import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "../api";
import { useAuth } from "../auth";
import { buildMobile } from "../mobile/piattaforma";

type Modo = "accedi" | "registra";

// ID client Google (Google Cloud → OAuth). Se assente, il bottone Google resta informativo.
const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;

interface GoogleId {
  initialize: (c: { client_id: string; callback: (r: { credential: string }) => void }) => void;
  prompt: () => void;
}
type WindowGoogle = Window & { google?: { accounts: { id: GoogleId } } };

// Schermata di ingresso: ogni persona può creare il proprio account gratuito
// (spazio privato isolato), accedere a uno esistente, oppure provare la demo.
export function Accesso() {
  const { accedi, registrati, provaDemo } = useAuth();
  const [modo, setModo] = useState<Modo>("registra");
  const [nome, setNome] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errore, setErrore] = useState<string | null>(null);
  const [attesa, setAttesa] = useState(false);
  const [infoGoogle, setInfoGoogle] = useState<string | null>(null);
  // 2FA: il server ha chiesto il codice a 6 cifre dell'app di autenticazione.
  const [serve2fa, setServe2fa] = useState(false);
  const [codice2fa, setCodice2fa] = useState("");

  // Carica Google Identity Services e prepara il callback (solo se è configurato il client ID).
  useEffect(() => {
    if (buildMobile || !GOOGLE_CLIENT_ID) return;
    const avvia = () => {
      const g = (window as WindowGoogle).google;
      if (!g) return;
      g.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: (r) => {
          void (async () => {
            try {
              await api.googleLogin(r.credential);
              location.reload();
            } catch (err) {
              setErrore(err instanceof Error ? err.message : "Accesso con Google non riuscito.");
            }
          })();
        },
      });
    };
    if ((window as WindowGoogle).google) {
      avvia();
      return;
    }
    const id = "gis-script";
    const esistente = document.getElementById(id);
    if (esistente) {
      esistente.addEventListener("load", avvia);
      return;
    }
    const s = document.createElement("script");
    s.id = id;
    s.src = "https://accounts.google.com/gsi/client";
    s.async = true;
    s.defer = true;
    s.addEventListener("load", avvia);
    document.body.appendChild(s);
  }, []);

  async function invia(e: FormEvent) {
    e.preventDefault();
    setErrore(null);
    setAttesa(true);
    try {
      if (modo === "registra") {
        if (nome.trim().length < 2) throw new Error("Scrivi il tuo nome o il nome della tua attività.");
        if (password.length < 8) throw new Error("La password deve avere almeno 8 caratteri.");
        await registrati(nome.trim(), email.trim(), password);
      } else {
        await accedi(email.trim(), password, serve2fa ? codice2fa.trim() : undefined);
      }
    } catch (err) {
      // L'account ha il 2FA attivo: mostra il campo per il codice a 6 cifre.
      if (err instanceof ApiError && err.corpo?.serveCodice2fa) {
        setServe2fa(true);
        setErrore(
          codice2fa
            ? "Codice non valido: controlla l'app di autenticazione e riprova."
            : "Questo account è protetto: inserisci il codice a 6 cifre dell'app di autenticazione.",
        );
      } else {
        setErrore(err instanceof Error ? err.message : "Qualcosa è andato storto. Riprova.");
      }
    } finally {
      setAttesa(false);
    }
  }

  async function demo() {
    setErrore(null);
    setAttesa(true);
    try {
      await provaDemo();
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Demo non disponibile in questo momento.");
    } finally {
      setAttesa(false);
    }
  }

  return (
    <div className="centro">
      <div className="card accesso-card">
        <div className="accesso-brand">
          <div className="brand-mark">P</div>
          <div>
            <strong>PaghePro</strong>
            <small className="muto">Buste paga, fatture e contabilità — uso personale gratuito</small>
          </div>
        </div>

        <div className="accesso-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={modo === "registra"}
            className={modo === "registra" ? "attivo" : ""}
            onClick={() => { setModo("registra"); setErrore(null); setServe2fa(false); setCodice2fa(""); }}
          >
            Crea account
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={modo === "accedi"}
            className={modo === "accedi" ? "attivo" : ""}
            onClick={() => { setModo("accedi"); setErrore(null); }}
          >
            Accedi
          </button>
        </div>

        <form onSubmit={invia} className="accesso-form">
          {modo === "registra" && (
            <label>
              <span>Nome o nome dell'attività</span>
              <input
                value={nome}
                onChange={(e) => setNome(e.target.value)}
                placeholder="Es. Mario Rossi"
                autoComplete="name"
              />
            </label>
          )}
          <label>
            <span>Email</span>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="tu@esempio.it"
              autoComplete="email"
              required
            />
          </label>
          <label>
            <span>Password</span>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder={modo === "registra" ? "Almeno 8 caratteri" : "La tua password"}
              autoComplete={modo === "registra" ? "new-password" : "current-password"}
              required
            />
          </label>
          {modo === "accedi" && serve2fa && (
            <label>
              <span>Codice di verifica (app di autenticazione)</span>
              <input
                inputMode="numeric"
                pattern="[0-9]*"
                maxLength={6}
                value={codice2fa}
                onChange={(e) => setCodice2fa(e.target.value)}
                placeholder="Es. 123456"
                autoComplete="one-time-code"
                autoFocus
              />
            </label>
          )}

          {errore && <span className="errore">{errore}</span>}

          <button className="btn primary accesso-invia" type="submit" disabled={attesa}>
            {attesa
              ? "Attendere…"
              : modo === "registra"
                ? "Crea il mio account gratis"
                : "Entra"}
          </button>
        </form>

        {!buildMobile && <div className="accesso-sep"><span>oppure</span></div>}

        {!buildMobile && <button
          className="accesso-google"
          type="button"
          onClick={() => {
            if (!GOOGLE_CLIENT_ID) {
              setInfoGoogle(
                "L'accesso con Google sarà attivabile a breve (serve un breve passaggio di configurazione). " +
                  "Per ora accedi con la tua email Gmail e password qui sopra.",
              );
              return;
            }
            (window as WindowGoogle).google?.accounts.id.prompt();
          }}
          disabled={attesa}
        >
          <svg viewBox="0 0 48 48" aria-hidden="true">
            <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
            <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
            <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
            <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
          </svg>
          Continua con Google
        </button>}
        {!buildMobile && infoGoogle && <p className="accesso-info">{infoGoogle}</p>}

        {!buildMobile && <button className="btn secondary accesso-demo" type="button" onClick={demo} disabled={attesa}>
          👀 Prova la demo (senza registrarti)
        </button>}

        <p className="accesso-nota muto">
          Strumento professionale: i valori sono d'esempio e vanno validati con un Consulente del
          Lavoro. L'invio ufficiale (fatture, F24, UniEmens) richiede canali abilitati.
        </p>
      </div>
    </div>
  );
}
