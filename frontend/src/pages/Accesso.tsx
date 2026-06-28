import { useState, type FormEvent } from "react";
import { useAuth } from "../auth";

type Modo = "accedi" | "registra";

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
        await accedi(email.trim(), password);
      }
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Qualcosa è andato storto. Riprova.");
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
            onClick={() => { setModo("registra"); setErrore(null); }}
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

          {errore && <span className="errore">{errore}</span>}

          <button className="btn primary accesso-invia" type="submit" disabled={attesa}>
            {attesa
              ? "Attendere…"
              : modo === "registra"
                ? "Crea il mio account gratis"
                : "Entra"}
          </button>
        </form>

        <div className="accesso-sep"><span>oppure</span></div>

        <button
          className="accesso-google"
          type="button"
          onClick={() =>
            setInfoGoogle(
              "L'accesso con Google sarà attivabile a breve (serve un breve passaggio di configurazione). " +
                "Per ora accedi con la tua email Gmail e password qui sopra.",
            )
          }
          disabled={attesa}
        >
          <svg viewBox="0 0 48 48" aria-hidden="true">
            <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
            <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
            <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
            <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
          </svg>
          Continua con Google
        </button>
        {infoGoogle && <p className="accesso-info">{infoGoogle}</p>}

        <button className="btn secondary accesso-demo" type="button" onClick={demo} disabled={attesa}>
          👀 Prova la demo (senza registrarti)
        </button>

        <p className="accesso-nota muto">
          Strumento professionale: i valori sono d'esempio e vanno validati con un Consulente del
          Lavoro. L'invio ufficiale (fatture, F24, UniEmens) richiede canali abilitati.
        </p>
      </div>
    </div>
  );
}
