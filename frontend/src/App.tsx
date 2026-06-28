import { useAuth } from "./auth";
import { Accesso } from "./pages/Accesso";
import { Dashboard } from "./pages/Dashboard";

export default function App() {
  const { token } = useAuth();
  // Senza account: schermata di accesso. Con account: l'applicazione.
  if (!token) return <Accesso />;
  return <Dashboard />;
}
