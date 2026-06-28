import { useEffect, useState, type FormEvent } from "react";
import { api, type Azienda, type DipendenteRapporto, type Livello } from "../api";

const CCNL = "COMMERCIO_TERZIARIO";

const formVuoto = { codiceFiscale: "", nome: "", cognome: "", livello: "3", iban: "", qualifica: "Impiegato" };

export function Dipendenti({ onApri }: { onApri: (rapportoId: string) => void }) {
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [lista, setLista] = useState<DipendenteRapporto[]>([]);
  const [livelli, setLivelli] = useState<Livello[]>([]);
  const [form, setForm] = useState(formVuoto);
  const [mostraForm, setMostraForm] = useState(false);
  const [caricamento, setCaricamento] = useState(true);
  const [salvataggio, setSalvataggio] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        // Contesto azienda: usa la prima azienda del tenant, creandone una demo se non esiste.
        let aziende = await api.aziende();
        if (aziende.length === 0) {
          await api.creaAzienda({
            ragioneSociale: "Rossi S.r.l.", partitaIva: "01567890123",
            indirizzo: "Via Roma 10", cap: "20121", comune: "Milano", provincia: "MI",
          });
          aziende = await api.aziende();
        }
        const a = aziende[0] ?? null;
        setAzienda(a);
        if (a) setLista(await api.dipendenti(a.id));
        api.livelli(CCNL).then((r) => setLivelli(r.livelli)).catch(() => { /* ignora */ });
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  async function aggiungi(e: FormEvent) {
    e.preventDefault();
    if (!azienda) return;
    setSalvataggio(true);
    setErrore(null);
    try {
      await api.creaDipendente(azienda.id, {
        codiceFiscale: form.codiceFiscale.trim().toUpperCase(),
        nome: form.nome.trim(),
        cognome: form.cognome.trim(),
        codiceCcnl: CCNL,
        livello: form.livello,
        iban: form.iban.trim() || undefined,
        qualifica: form.qualifica,
      });
      setLista(await api.dipendenti(azienda.id));
      setForm(formVuoto);
      setMostraForm(false);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel salvataggio");
    } finally {
      setSalvataggio(false);
    }
  }

  const set = (k: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Dipendenti</h1>
          <p>
            Archivio lavoratori reali{azienda ? ` · ${azienda.ragioneSociale}` : ""} — dal database
          </p>
        </div>
        <button className="btn primary" type="button" onClick={() => setMostraForm((v) => !v)}>
          {mostraForm ? "Chiudi" : "+ Nuovo dipendente"}
        </button>
      </div>

      {mostraForm && (
        <form className="form-grid" onSubmit={aggiungi} style={{ marginBottom: 4 }}>
          <label>
            <span>Codice fiscale</span>
            <input value={form.codiceFiscale} onChange={set("codiceFiscale")} placeholder="RSSMRA80A01F205X" required maxLength={16} />
          </label>
          <label>
            <span>Qualifica</span>
            <select value={form.qualifica} onChange={set("qualifica")}>
              <option>Impiegato</option>
              <option>Operaio</option>
              <option>Quadro</option>
              <option>Dirigente</option>
              <option>Apprendista</option>
            </select>
          </label>
          <label>
            <span>Nome</span>
            <input value={form.nome} onChange={set("nome")} required />
          </label>
          <label>
            <span>Cognome</span>
            <input value={form.cognome} onChange={set("cognome")} required />
          </label>
          <label>
            <span>Livello CCNL Commercio</span>
            <select value={form.livello} onChange={set("livello")}>
              {livelli.length === 0
                ? <option value="3">3</option>
                : livelli.map((l) => <option key={l.codice} value={l.codice}>{l.codice} — {l.descrizione}</option>)}
            </select>
          </label>
          <label>
            <span>IBAN <small>(facoltativo)</small></span>
            <input value={form.iban} onChange={set("iban")} placeholder="IT.." />
          </label>
          <button className="btn primary wide-field" type="submit" disabled={salvataggio}>
            {salvataggio ? "Salvataggio…" : "Salva dipendente"}
          </button>
        </form>
      )}

      {errore && <p className="errore">{errore}</p>}

      {caricamento ? (
        <div className="empty-state">Caricamento…</div>
      ) : lista.length === 0 ? (
        <div className="empty-state">Nessun dipendente ancora. Premi “+ Nuovo dipendente” per aggiungerne uno reale.</div>
      ) : (
        <div className="table-wrap">
          <table className="data-table spacious">
            <thead>
              <tr>
                <th>Dipendente</th>
                <th>Matricola</th>
                <th>Qualifica</th>
                <th>Livello</th>
                <th>CCNL</th>
                <th className="right">Azioni</th>
              </tr>
            </thead>
            <tbody>
              {lista.map((d) => (
                <tr key={d.rapportoId} style={{ cursor: "pointer" }} onClick={() => onApri(d.rapportoId)}>
                  <td>
                    <div className="person-cell">
                      <span>{iniziali(d.nominativo)}</span>
                      <strong>{d.nominativo || "—"}</strong>
                    </div>
                  </td>
                  <td className="num">{d.dipendenteId.slice(0, 6).toUpperCase()}</td>
                  <td>{d.qualifica}</td>
                  <td>{d.livello}</td>
                  <td>{d.codiceCcnl === CCNL ? "Commercio" : d.codiceCcnl}</td>
                  <td className="right">
                    <button className="mini-action" type="button" onClick={(e) => { e.stopPropagation(); onApri(d.rapportoId); }}>
                      Apri
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function iniziali(nominativo: string): string {
  const parti = nominativo.trim().split(/\s+/).filter(Boolean);
  if (parti.length === 0) return "—";
  return parti.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? "").join("");
}
