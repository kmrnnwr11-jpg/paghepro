import { useEffect, useState, type FormEvent } from "react";
import { api, type Azienda, type DipendenteRapporto, type RedditiPfBozza } from "../api";
import { scegliAziendaPreferita } from "../aziendaCorrente";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function RedditiPf() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [anno, setAnno] = useState(2026);
  const [b, setB] = useState<RedditiPfBozza | null>(null);
  // input modificabili
  const [altri, setAltri] = useState(0);
  const [deduc, setDeduc] = useState(0);
  const [detr19, setDetr19] = useState(0);
  const [acconti, setAcconti] = useState(0);
  const [caricamento, setCaricamento] = useState(true);
  const [salvataggio, setSalvataggio] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const aziende = await api.aziende();
        const a = scegliAziendaPreferita(aziende);
        setAzienda(a);
        if (a) {
          const d = await api.dipendenti(a.id);
          setDipendenti(d);
          if (d.length > 0) setRapportoId(d[0].rapportoId);
        }
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  function applica(bozza: RedditiPfBozza) {
    setB(bozza);
    setAltri(bozza.altriRedditi);
    setDeduc(bozza.oneriDeducibili);
    setDetr19(bozza.oneriDetraibili19);
    setAcconti(bozza.acconti);
  }

  useEffect(() => {
    if (!rapportoId) { setB(null); return; }
    api.redditiPf(rapportoId, anno)
      .then(applica)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [rapportoId, anno]);

  async function calcola(e: FormEvent) {
    e.preventDefault();
    if (!rapportoId) return;
    setSalvataggio(true);
    setErrore(null);
    try {
      const aggiornata = await api.salvaRedditiPf(rapportoId, anno, {
        altriRedditi: altri, oneriDeducibili: deduc, oneriDetraibili19: detr19, acconti,
      });
      setB(aggiornata);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel calcolo");
    } finally {
      setSalvataggio(false);
    }
  }

  return (
    <div className="module-layout wide">
      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h1>Redditi PF / 730</h1>
            <p>Bozza dichiarazione del contribuente{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 4 dichiarazioni</p>
          </div>
          <span className="status-pill amber">Bozza</span>
        </div>

        {caricamento ? (
          <div className="empty-state">Caricamento…</div>
        ) : dipendenti.length === 0 ? (
          <div className="empty-state">Nessun dipendente. Aggiungine in “Dipendenti” e genera i cedolini.</div>
        ) : (
          <>
            <div className="form-grid">
              <label>
                <span>Contribuente</span>
                <select value={rapportoId} onChange={(e) => setRapportoId(e.target.value)}>
                  {dipendenti.map((d) => <option key={d.rapportoId} value={d.rapportoId}>{d.nominativo || "—"}</option>)}
                </select>
              </label>
              <label>
                <span>Anno d'imposta</span>
                <input type="number" min={2000} max={2100} value={anno} onChange={(e) => setAnno(Number(e.target.value))} />
              </label>
            </div>

            {b && (
              <>
                <div className="result-summary" style={{ gridTemplateColumns: "repeat(2, minmax(0,1fr))" }}>
                  <div>
                    <span>Reddito lavoro dipendente (dai cedolini)</span>
                    <strong className="num">{euro(b.redditoLavoroDipendente)} €</strong>
                  </div>
                  <div>
                    <span>Ritenute IRPEF già operate</span>
                    <strong className="num">{euro(b.ritenuteOperate)} €</strong>
                  </div>
                </div>

                <form className="form-grid" onSubmit={calcola}>
                  <label>
                    <span>Altri redditi</span>
                    <input type="number" step="0.01" value={altri} onChange={(e) => setAltri(Number(e.target.value))} />
                  </label>
                  <label>
                    <span>Oneri deducibili</span>
                    <input type="number" step="0.01" value={deduc} onChange={(e) => setDeduc(Number(e.target.value))} />
                  </label>
                  <label>
                    <span>Oneri detraibili 19%</span>
                    <input type="number" step="0.01" value={detr19} onChange={(e) => setDetr19(Number(e.target.value))} />
                  </label>
                  <label>
                    <span>Acconti IRPEF versati</span>
                    <input type="number" step="0.01" value={acconti} onChange={(e) => setAcconti(Number(e.target.value))} />
                  </label>
                  <button className="btn primary wide-field" type="submit" disabled={salvataggio}>
                    {salvataggio ? "Calcolo…" : "Calcola e salva bozza"}
                  </button>
                </form>

                <div className="table-wrap">
                  <table className="data-table compact">
                    <tbody>
                      <tr><td>Reddito complessivo</td><td className="right num">{euro(b.redditoComplessivo)} €</td></tr>
                      <tr><td>− Oneri deducibili</td><td className="right num">{euro(b.oneriDeducibili)} €</td></tr>
                      <tr><td><strong>Reddito imponibile</strong></td><td className="right num"><strong>{euro(b.redditoImponibile)} €</strong></td></tr>
                      <tr><td>IRPEF lorda (scaglioni)</td><td className="right num">{euro(b.irpefLorda)} €</td></tr>
                      <tr><td>− Detrazione lavoro dipendente</td><td className="right num">{euro(b.detrazioneLavoroDipendente)} €</td></tr>
                      <tr><td>− Detrazione oneri (19%)</td><td className="right num">{euro(b.detrazioneOneri)} €</td></tr>
                      <tr><td><strong>IRPEF netta</strong></td><td className="right num"><strong>{euro(b.irpefNetta)} €</strong></td></tr>
                      <tr><td>− Ritenute già operate</td><td className="right num">{euro(b.ritenuteOperate)} €</td></tr>
                      <tr><td>− Acconti versati</td><td className="right num">{euro(b.acconti)} €</td></tr>
                    </tbody>
                  </table>
                </div>

                <div className="accounting-total" style={{ borderColor: b.saldoADebito > 0 ? "rgba(185,28,28,.2)" : "rgba(22,163,74,.2)" }}>
                  <span>{b.saldoADebito > 0 ? "Saldo IRPEF a debito" : "Saldo IRPEF a credito"}</span>
                  <strong style={{ color: b.saldoADebito > 0 ? "var(--red)" : "var(--ok-testo)" }}>
                    {euro(b.saldoADebito > 0 ? b.saldoADebito : b.saldoACredito)} €
                  </strong>
                  <em>{b.numeroCedolini} cedolini · anno {b.anno}</em>
                </div>
              </>
            )}
          </>
        )}

        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Cos'è questa schermata</h2>
        <div className="empty-state" style={{ marginBottom: 12 }}>
          Bozza <b>730 / Redditi PF</b>: parte dal reddito da lavoro dei cedolini, aggiunge i tuoi dati e calcola l'IRPEF dovuta.
        </div>
        <div className="rail-metric"><span>Livello</span><strong>A · Bozza (fatto)</strong></div>
        <div className="rail-metric"><span>File ufficiale AE</span><strong>Da fare</strong></div>
        <div className="rail-metric"><span>Invio Entratel</span><strong>Da fare 🔑</strong></div>
        <p className="muto" style={{ fontSize: 12, marginTop: 10, fontWeight: 600 }}>
          Calcolo dichiarativo semplificato (scaglioni IRPEF + detrazioni base). Da validare con un Commercialista.
        </p>
      </aside>
    </div>
  );
}
