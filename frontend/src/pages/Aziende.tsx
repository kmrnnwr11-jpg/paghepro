import { useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { api, type Azienda, type NuovaAzienda } from "../api";
import { ComuneAutocomplete } from "../ComuneAutocomplete";

const vuota: NuovaAzienda = {
  ragioneSociale: "", partitaIva: "", indirizzo: "", cap: "", comune: "", provincia: "",
};

export function Aziende() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [form, setForm] = useState<NuovaAzienda>(vuota);
  const [daConfermare, setDaConfermare] = useState<NuovaAzienda | null>(null);
  const [notaVisura, setNotaVisura] = useState<string | null>(null);
  const [visuraInCorso, setVisuraInCorso] = useState(false);
  // Correzione di un'azienda già salvata (es. visura letta male): id in modifica, o null.
  const [inModificaId, setInModificaId] = useState<string | null>(null);

  async function caricaVisura(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    setErrore(null);
    setNotaVisura(null);
    setVisuraInCorso(true);
    try {
      const d = await api.aziendaDaVisura(file);
      setForm({
        ragioneSociale: d.ragioneSociale ?? "",
        partitaIva: d.partitaIva ?? "",
        indirizzo: d.indirizzo ?? "",
        cap: d.cap ?? "",
        comune: d.comune ?? "",
        provincia: d.provincia ?? "",
      });
      setNotaVisura(d.nota ?? "Dati estratti dalla visura: controllali, poi premi «Aggiungi azienda».");
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Non sono riuscito a leggere la visura.");
    } finally {
      setVisuraInCorso(false);
    }
  }

  async function carica() {
    try { setAziende(await api.aziende()); }
    catch (e) { setErrore(e instanceof Error ? e.message : "Errore"); }
  }
  useEffect(() => { void carica(); }, []);

  function chiediConferma(e: FormEvent) {
    e.preventDefault();
    setErrore(null);
    if (inModificaId) { void salvaCorrezione(); return; }
    setDaConfermare(form);
  }

  function iniziaModifica(a: Azienda) {
    setErrore(null);
    setNotaVisura(null);
    setInModificaId(a.id);
    setForm({
      ragioneSociale: a.ragioneSociale,
      partitaIva: a.partitaIva ?? "",
      indirizzo: a.indirizzo ?? "",
      cap: a.cap ?? "",
      comune: a.comune ?? "",
      provincia: a.provincia ?? "",
    });
  }

  function annullaModifica() {
    setInModificaId(null);
    setForm(vuota);
  }

  async function salvaCorrezione() {
    if (!inModificaId) return;
    try {
      await api.correggiAzienda(inModificaId, form);
      annullaModifica();
      await carica();
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Salvataggio non riuscito. Riprova.");
    }
  }

  async function conferma() {
    if (!daConfermare) return;
    try {
      await api.creaAzienda(daConfermare);
      setForm(vuota);
      setDaConfermare(null);
      await carica();
    } catch (err) {
      setErrore(err instanceof Error ? err.message : "Errore");
      setDaConfermare(null);
    }
  }

  const set = (k: keyof NuovaAzienda) => (e: { target: { value: string } }) =>
    setForm({ ...form, [k]: e.target.value });

  return (
    <section className="glass-card form-card">
      <div className="card-title-row">
        <div>
          <h2>Archivio aziende</h2>
          <p>Anagrafiche clienti disponibili per elaborazione paghe e documenti fiscali</p>
        </div>
        <span className="count blue">{aziende.length}</span>
      </div>

      {aziende.length === 0 ? (
        <div className="empty-state">Nessuna azienda ancora registrata.</div>
      ) : (
        <div className="company-list">
          {aziende.map((a) => (
            <div className="company-row" key={a.id}>
              <span>{a.ragioneSociale.slice(0, 2).toUpperCase()}</span>
              <strong>{a.ragioneSociale}</strong>
              <button className="mini-action" type="button" onClick={() => iniziaModifica(a)}>
                ✏️ Modifica
              </button>
              <em>Attiva</em>
            </div>
          ))}
        </div>
      )}

      {!daConfermare && (
        <div style={{ margin: "10px 0 14px" }}>
          <label
            className="btn secondary"
            style={{ cursor: visuraInCorso ? "wait" : "pointer", display: "inline-flex", gap: 8, alignItems: "center" }}
          >
            📄 {visuraInCorso ? "Lettura in corso…" : "Carica visura (PDF o foto) → compila da sola"}
            <input
              type="file"
              accept="application/pdf,.pdf,image/*"
              onChange={caricaVisura}
              disabled={visuraInCorso}
              style={{ display: "none" }}
            />
          </label>
          {notaVisura && (
            <p className="muto" style={{ fontSize: 12, marginTop: 6, lineHeight: 1.5 }}>
              ✅ {notaVisura}
            </p>
          )}
        </div>
      )}

      {daConfermare ? (
        <div className="confirm-panel">
          <p>
            Stai per aggiungere <b>{daConfermare.ragioneSociale}</b>
            {daConfermare.comune ? <> · {daConfermare.comune} ({daConfermare.provincia})</> : null}.
          </p>
          <div className="actions-row">
            <button className="btn primary" type="button" onClick={conferma}>
              Conferma
            </button>
            <button className="btn secondary" type="button" onClick={() => setDaConfermare(null)}>
              Annulla
            </button>
          </div>
        </div>
      ) : (
        <form className="form-grid" onSubmit={chiediConferma}>
          {inModificaId && (
            <p className="muto wide-field" style={{ fontSize: 12.5 }}>
              ✏️ Stai correggendo un'azienda già salvata: sistemare i campi e premere «Salva correzioni».
            </p>
          )}
          <label>
            <span>Nome azienda</span>
            <input placeholder="Es. Rossi Srl" value={form.ragioneSociale} onChange={set("ragioneSociale")} required />
          </label>
          <label>
            <span>Partita IVA <small>(facoltativa)</small></span>
            <input placeholder="11 cifre" value={form.partitaIva} onChange={set("partitaIva")} />
          </label>
          <label className="wide-field">
            <span>Indirizzo</span>
            <input placeholder="Via e numero" value={form.indirizzo} onChange={set("indirizzo")} required />
          </label>
          <label>
            <span>Comune</span>
            <ComuneAutocomplete
              valore={form.comune}
              onSelect={(c) => setForm((f) => ({ ...f, comune: c.nome, provincia: c.prov, cap: c.cap }))}
            />
          </label>
          <label>
            <span>CAP</span>
            <input value={form.cap} onChange={set("cap")} required />
          </label>
          <label>
            <span>Provincia</span>
            <input value={form.provincia} onChange={set("provincia")} required maxLength={2} />
          </label>
          {inModificaId ? (
            <div className="actions-row wide-field">
              <button className="btn primary" type="submit">Salva correzioni</button>
              <button className="btn secondary" type="button" onClick={annullaModifica}>Annulla</button>
            </div>
          ) : (
            <button className="btn primary wide-field" type="submit">
              Aggiungi azienda
            </button>
          )}
        </form>
      )}
      {errore && <p className="errore">{errore}</p>}
    </section>
  );
}
