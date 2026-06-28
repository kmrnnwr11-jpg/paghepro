import { useEffect, useState, type FormEvent } from "react";
import { api, type Azienda, type ImuBozza } from "../api";

const euro = (n: number) => n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const categorie = ["A/2", "A/3", "A/10", "B/1", "C/1", "C/2", "C/3", "C/6", "D/1", "D/5"];
const formVuoto = { descrizione: "", categoriaCatastale: "A/2", renditaCatastale: 0, percentualePossesso: 100, mesiPossesso: 12, aliquotaPerMille: 8.6, abitazionePrincipale: false };

export function Imu() {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [anno, setAnno] = useState(2026);
  const [b, setB] = useState<ImuBozza | null>(null);
  const [form, setForm] = useState(formVuoto);
  const [mostraForm, setMostraForm] = useState(false);
  const [caricamento, setCaricamento] = useState(true);
  const [salvataggio, setSalvataggio] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        let aziende = await api.aziende();
        if (aziende.length === 0) {
          await api.creaAzienda({
            ragioneSociale: "Rossi S.r.l.", partitaIva: "01567890123",
            indirizzo: "Via Roma 10", cap: "20121", comune: "Milano", provincia: "MI",
          });
          aziende = await api.aziende();
        }
        setAzienda(aziende[0] ?? null);
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  async function ricarica() {
    if (!azienda) return;
    setB(await api.imuBozza(azienda.id, anno));
  }
  useEffect(() => {
    if (!azienda) { setB(null); return; }
    ricarica().catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, [azienda, anno]);

  async function aggiungi(e: FormEvent) {
    e.preventDefault();
    if (!azienda) return;
    setSalvataggio(true);
    setErrore(null);
    try {
      await api.aggiungiImmobile(azienda.id, form);
      setForm(formVuoto);
      setMostraForm(false);
      await ricarica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel salvataggio");
    } finally {
      setSalvataggio(false);
    }
  }

  async function elimina(id: string) {
    try { await api.eliminaImmobile(id); await ricarica(); }
    catch (e) { setErrore(e instanceof Error ? e.message : "Errore"); }
  }

  const set = (k: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [k]: typeof f[k] === "number" ? Number(e.target.value) : e.target.value }));

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>IMU — immobili</h1>
          <p>Anagrafica immobili e calcolo IMU{azienda ? ` · ${azienda.ragioneSociale}` : ""} — Fase 6 dichiarazioni</p>
        </div>
        <div className="actions-row" style={{ alignItems: "center" }}>
          <label style={{ display: "flex", gap: 6, alignItems: "center", fontSize: 12, fontWeight: 800, color: "var(--muted)" }}>
            Anno
            <input type="number" min={2000} max={2100} value={anno} onChange={(e) => setAnno(Number(e.target.value))} style={{ width: 90 }} />
          </label>
          <button className="btn primary" type="button" onClick={() => setMostraForm((v) => !v)}>
            {mostraForm ? "Chiudi" : "+ Nuovo immobile"}
          </button>
        </div>
      </div>

      {mostraForm && (
        <form className="form-grid" onSubmit={aggiungi} style={{ marginBottom: 4 }}>
          <label className="wide-field"><span>Descrizione</span><input value={form.descrizione} onChange={set("descrizione")} placeholder="Es. Ufficio Via Roma 10" required /></label>
          <label>
            <span>Categoria catastale</span>
            <select value={form.categoriaCatastale} onChange={set("categoriaCatastale")}>
              {categorie.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
          <label><span>Rendita catastale (€)</span><input type="number" step="0.01" value={form.renditaCatastale} onChange={set("renditaCatastale")} required /></label>
          <label><span>% possesso</span><input type="number" step="0.01" value={form.percentualePossesso} onChange={set("percentualePossesso")} /></label>
          <label><span>Mesi possesso</span><input type="number" min={0} max={12} value={form.mesiPossesso} onChange={set("mesiPossesso")} /></label>
          <label><span>Aliquota (‰)</span><input type="number" step="0.01" value={form.aliquotaPerMille} onChange={set("aliquotaPerMille")} /></label>
          <label className="wide-field" style={{ flexDirection: "row", alignItems: "center", gap: 8, display: "flex" }}>
            <input type="checkbox" style={{ width: 18, height: 18, minHeight: 0 }} checked={form.abitazionePrincipale} onChange={(e) => setForm((f) => ({ ...f, abitazionePrincipale: e.target.checked }))} />
            <span>Abitazione principale (esente, salvo categorie di lusso A/1-A/8-A/9)</span>
          </label>
          <button className="btn primary wide-field" type="submit" disabled={salvataggio}>{salvataggio ? "Salvataggio…" : "Salva immobile"}</button>
        </form>
      )}

      {errore && <p className="errore">{errore}</p>}

      {caricamento ? (
        <div className="empty-state">Caricamento…</div>
      ) : !b || b.righe.length === 0 ? (
        <div className="empty-state">Nessun immobile. Premi “+ Nuovo immobile” per aggiungerne uno e calcolare l'IMU.</div>
      ) : (
        <>
          <div className="table-wrap">
            <table className="data-table spacious">
              <thead>
                <tr>
                  <th>Immobile</th><th>Cat.</th><th className="right">Rendita</th><th className="right">Molt.</th>
                  <th className="right">Base imp.</th><th className="right">IMU</th><th className="right">Azioni</th>
                </tr>
              </thead>
              <tbody>
                {b.righe.map((r) => (
                  <tr key={r.id}>
                    <td><strong>{r.descrizione || "—"}</strong></td>
                    <td>{r.categoriaCatastale}</td>
                    <td className="right num">{euro(r.renditaCatastale)} €</td>
                    <td className="right num">{r.moltiplicatore}</td>
                    <td className="right num">{euro(r.baseImponibile)} €</td>
                    <td className="right num">{r.esente ? <span className="status-pill green">Esente</span> : <strong>{euro(r.imposta)} €</strong>}</td>
                    <td className="right"><button className="mini-action" type="button" onClick={() => elimina(r.id)} style={{ color: "var(--red)" }}>Elimina</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="accounting-total">
            <span>IMU totale {b.anno}</span>
            <strong>{euro(b.totaleImposta)} €</strong>
            <em>{b.righe.length} immobili · calcolo semplificato, da validare con un Commercialista</em>
          </div>
        </>
      )}
    </section>
  );
}
