// Anteprima busta paga: niente più dati finti. La busta reale si genera dalla pagina Cedolini.
export function BustaPaga({ onRicalcola }: { onRicalcola: () => void }) {
  return (
    <div>
      <div className="page-heading" style={{ alignItems: "center" }}>
        <div className="emp-id-top">
          <h1>Busta paga</h1>
        </div>
      </div>

      <div className="glass-card" style={{ padding: 28, maxWidth: 680 }}>
        <p style={{ fontSize: 15, fontWeight: 700, marginBottom: 6 }}>Nessuna busta da mostrare.</p>
        <p className="muto" style={{ lineHeight: 1.6, marginBottom: 16 }}>
          Per vedere una busta paga con i <strong>dati reali</strong>: aggiungi un'azienda dalla
          pagina <strong>Aziende</strong>, inserisci i <strong>Dipendenti</strong>, poi calcola un
          cedolino dalla pagina <strong>Cedolini</strong>. La busta comparirà lì (con PDF
          scaricabile).
        </p>
        <button className="btn secondary" type="button" onClick={onRicalcola}>
          ↻ Torna al calcolo
        </button>
      </div>
    </div>
  );
}
