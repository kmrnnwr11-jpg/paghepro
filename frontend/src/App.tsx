import { useAuth } from "./auth";
import { Accesso } from "./pages/Accesso";
import { Dashboard } from "./pages/Dashboard";

// Modalità "anteprima / vetrina" (build con VITE_VETRINA=1): il motore (backend) non è
// collegato. Mostriamo un avviso chiaro così è evidente che è una dimostrazione, e l'app
// resta comunque installabile (PWA). Nella build normale questo avviso non compare.
const VETRINA = import.meta.env.VITE_VETRINA === "1";

export default function App() {
  const { token } = useAuth();
  return (
    <>
      {VETRINA && (
        <div
          style={{
            position: "sticky",
            top: 0,
            zIndex: 50,
            background: "linear-gradient(135deg, #2563eb, #6d28d9)",
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
      {/* Senza account: schermata di accesso. Con account: l'applicazione. */}
      {token ? <Dashboard /> : <Accesso />}
    </>
  );
}
