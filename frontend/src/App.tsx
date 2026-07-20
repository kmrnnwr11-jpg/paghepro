import { useAuth } from "./auth";
import { Accesso } from "./pages/Accesso";
import { Dashboard } from "./pages/Dashboard";

// Modalità "anteprima / vetrina" (build con VITE_VETRINA=1): il motore (backend) non è
// collegato. Mostriamo un avviso chiaro così è evidente che è una dimostrazione, e l'app
// resta comunque installabile (PWA). Nella build normale questo avviso non compare.
const VETRINA = import.meta.env.VITE_VETRINA === "1";

export default function App() {
  const { token, ingressoInCorso } = useAuth();
  return (
    <>
      {VETRINA && (
        <div
          style={{
            position: "sticky",
            top: 0,
            zIndex: 50,
            background: "linear-gradient(135deg, #0e7268, #10a08f)",
            color: "#fff",
            padding: "9px 14px",
            fontSize: 12.5,
            fontWeight: 700,
            textAlign: "center",
            lineHeight: 1.4,
          }}
        >
          🔎 Anteprima dimostrativa — puoi installare l'app; account e calcoli si attivano quando il
          sistema sarà online.
        </div>
      )}
      {/* Con account: l'applicazione. All'avvio si entra da soli (breve apertura);
          la schermata di accesso compare solo se si esce di proposito (login opzionale). */}
      {token ? <Dashboard /> : ingressoInCorso ? <Apertura /> : <Accesso />}
    </>
  );
}

// Breve schermata di apertura durante l'ingresso automatico, al posto del login.
function Apertura() {
  return (
    <div className="centro">
      <div className="card accesso-card" style={{ textAlign: "center" }}>
        <div className="accesso-brand" style={{ justifyContent: "center", marginBottom: 10 }}>
          <div className="brand-mark">P</div>
          <strong>PaghePro</strong>
        </div>
        <p className="muto" style={{ fontSize: 13 }}>Apertura in corso…</p>
      </div>
    </div>
  );
}
