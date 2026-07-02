import { useEffect, useState } from "react";
import { api, type Azienda } from "../api";

// Elaborazione cedolini: collegata ai dati reali (niente più dipendenti finti).
// Mostra le aziende reali; il calcolo vero dei cedolini si fa dalla pagina Cedolini.
export function Elaborazione() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [caricamento, setCaricamento] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setAziende(await api.aziende());
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore nel caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  return (
    <div className="detail-layout">
      <div className="page-heading">
        <div>
          <h1>Elaborazione cedolini</h1>
          <p>Calcolo dei cedolini del mese · solo dati reali</p>
        </div>
      </div>

      {errore && <p className="errore">{errore}</p>}

      {caricamento ? (
        <div className="glass-card" style={{ padding: 24 }}>Caricamento…</div>
      ) : aziende.length === 0 ? (
        <div className="glass-card" style={{ padding: 28 }}>
          <p style={{ fontSize: 15, fontWeight: 700, marginBottom: 6 }}>Nessuna azienda ancora.</p>
          <p className="muto" style={{ lineHeight: 1.6 }}>
            Per elaborare i cedolini: <strong>1)</strong> aggiungi un'azienda dalla pagina{" "}
            <strong>Aziende</strong>, <strong>2)</strong> inserisci i <strong>Dipendenti</strong>,{" "}
            <strong>3)</strong> genera le buste dalla pagina <strong>Cedolini</strong>.
          </p>
        </div>
      ) : (
        <div className="glass-card" style={{ padding: 24 }}>
          <p style={{ marginBottom: 12 }}>
            Aziende disponibili: <strong>{aziende.length}</strong>. Apri i{" "}
            <strong>Dipendenti</strong> di un'azienda e genera i cedolini reali dalla pagina{" "}
            <strong>Cedolini</strong>.
          </p>
          <div className="company-list">
            {aziende.map((a) => (
              <div className="company-row" key={a.id}>
                <span>{a.ragioneSociale.slice(0, 2).toUpperCase()}</span>
                <strong>{a.ragioneSociale}</strong>
                <em>Attiva</em>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
