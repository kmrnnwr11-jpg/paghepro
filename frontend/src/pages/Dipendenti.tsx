import { useEffect, useState, type FormEvent } from "react";
import { api, type Azienda, type DipendenteRapporto, type Livello } from "../api";
import { CaricaUnilav } from "./CaricaUnilav";
import { ComuneAutocomplete } from "../ComuneAutocomplete";
import { salvaAziendaCorrenteId, scegliAziendaPreferita } from "../aziendaCorrente";

const DEFAULT_CCNL = "COMMERCIO_TERZIARIO";
const DEFAULT_LIVELLO = "3";
const TUTTE_LE_AZIENDE = "__tutte__";

type DipendenteConAzienda = DipendenteRapporto & {
  aziendaId: string;
  aziendaNome: string;
};

async function caricaDipendentiAziende(aziende: Azienda[]): Promise<DipendenteConAzienda[]> {
  const gruppi = await Promise.all(aziende.map(async (azienda) => {
    const dipendenti = await api.dipendenti(azienda.id);
    return dipendenti.map((dipendente) => ({
      ...dipendente,
      aziendaId: azienda.id,
      aziendaNome: azienda.ragioneSociale,
    }));
  }));
  return gruppi.flat();
}

function codiceCcnlAzienda(azienda: Azienda | null): string {
  return azienda?.ccnlDefault?.trim() || DEFAULT_CCNL;
}

function livelloAzienda(azienda: Azienda | null): string {
  return azienda?.livelloDefault?.trim() || DEFAULT_LIVELLO;
}

function nomeCcnl(codice: string): string {
  const nomi: Record<string, string> = {
    COMMERCIO_TERZIARIO: "Commercio",
    TRASPORTI_LOGISTICA: "Logistica",
    EDILIZIA_INDUSTRIA: "Edilizia",
    METALMECCANICI_INDUSTRIA: "Metalmeccanici",
    TURISMO_PUBBLICI_ESERCIZI: "Turismo",
    MULTISERVIZI_PULIZIE: "Multiservizi",
    STUDI_PROFESSIONALI: "Studi professionali",
    ALIMENTARE_INDUSTRIA: "Alimentare",
    AGRICOLTURA_OPERAI: "Agricoltura",
  };
  return nomi[codice] ?? codice;
}

const formVuoto = {
  codiceFiscale: "", nome: "", cognome: "", livello: DEFAULT_LIVELLO, iban: "", qualifica: "Impiegato",
  regione: "", codiceCatastaleComune: "", comuneNome: "",
};

export function Dipendenti({ onApri }: { onApri: (rapportoId: string) => void }) {
  const [tutteAziende, setTutteAziende] = useState<Azienda[]>([]);
  const [azienda, setAzienda] = useState<Azienda | null>(null);
  const [filtroAziendaId, setFiltroAziendaId] = useState(TUTTE_LE_AZIENDE);
  const [lista, setLista] = useState<DipendenteConAzienda[]>([]);
  const [livelli, setLivelli] = useState<Livello[]>([]);
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
          try {
            await api.creaDatasetDemo();
            aziende = await api.aziende();
          } catch {
            // Account non demo: resta il normale archivio vuoto.
          }
        }
        setTutteAziende(aziende);
        const a = scegliAziendaPreferita(aziende);
        if (a) salvaAziendaCorrenteId(a.id);
        setLista(await caricaDipendentiAziende(aziende));
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!azienda) return;
    let vivo = true;
    const codice = codiceCcnlAzienda(azienda);
    const livelloDefault = livelloAzienda(azienda);
    setForm((f) => ({ ...f, livello: livelloDefault }));
    setLivelli([]);
    api.livelli(codice)
      .then((r) => {
        if (!vivo) return;
        setLivelli(r.livelli);
        setForm((f) => {
          const livelloOk = r.livelli.some(l => l.codice === f.livello);
          return { ...f, livello: livelloOk ? f.livello : (r.livelli[0]?.codice ?? livelloDefault) };
        });
      })
      .catch(() => { if (vivo) setLivelli([]); });
    return () => { vivo = false; };
  }, [azienda?.id, azienda?.ccnlDefault, azienda?.livelloDefault]);

  async function cambiaAzienda(id: string) {
    setFiltroAziendaId(id);
    const a = tutteAziende.find(x => x.id === id) ?? null;
    setAzienda(a);
    if (a) salvaAziendaCorrenteId(a.id);
    setErrore(null);
    setMostraForm(false);
    setLista([]);
    setCaricamento(true);
    try {
      const aziendeDaCaricare = a ? [a] : tutteAziende;
      setLista(await caricaDipendentiAziende(aziendeDaCaricare));
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore di caricamento");
    } finally {
      setCaricamento(false);
    }
  }

  async function aggiungi(e: FormEvent) {
    e.preventDefault();
    if (!azienda) {
      setErrore("Prima aggiungi un'azienda oppure importa un UNILAV: ogni dipendente deve essere collegato a un datore di lavoro.");
      return;
    }
    setSalvataggio(true);
    setErrore(null);
    try {
      await api.creaDipendente(azienda.id, {
        codiceFiscale: form.codiceFiscale.trim().toUpperCase(),
        nome: form.nome.trim(),
        cognome: form.cognome.trim(),
        codiceCcnl: codiceCcnlAzienda(azienda),
        livello: form.livello || livelloAzienda(azienda),
        iban: form.iban.trim() || undefined,
        qualifica: form.qualifica,
        regione: form.regione || undefined,
        codiceCatastaleComune: form.codiceCatastaleComune || undefined,
      });
      setLista(await caricaDipendentiAziende([azienda]));
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

  // Dopo l'import UNILAV: porta la lista sull'azienda (datore) creata/trovata dal PDF.
  const dopoImport = async (az: { id: string; ragioneSociale: string }) => {
    setAzienda(az);
    setFiltroAziendaId(az.id);
    setErrore(null);
    salvaAziendaCorrenteId(az.id);
    setTutteAziende((correnti) =>
      correnti.some((a) => a.id === az.id) ? correnti : [...correnti, az]);
    try {
      const [aziende, dipendenti] = await Promise.all([
        api.aziende(),
        api.dipendenti(az.id),
      ]);
      setTutteAziende(aziende);
      setLista(dipendenti.map((dipendente) => ({
        ...dipendente,
        aziendaId: az.id,
        aziendaNome: az.ragioneSociale,
      })));
    } catch {
      try {
        const dipendenti = await api.dipendenti(az.id);
        setLista(dipendenti.map((dipendente) => ({
          ...dipendente,
          aziendaId: az.id,
          aziendaNome: az.ragioneSociale,
        })));
      } catch { /* ignora */ }
    }
  };

  return (
    <>
      <CaricaUnilav onImportato={dopoImport} />
      <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Dipendenti</h1>
          <p>Archivio lavoratori — dal database</p>
        </div>
        <button
          className="btn primary"
          type="button"
          onClick={() => {
            if (!azienda) {
              setErrore(tutteAziende.length === 0
                ? "Prima aggiungi un'azienda oppure importa un UNILAV: poi potrai salvare il dipendente."
                : "Seleziona una singola azienda dal menu prima di aggiungere un dipendente.");
              setMostraForm(false);
              return;
            }
            setErrore(null);
            setMostraForm((v) => !v);
          }}
        >
          {mostraForm ? "Chiudi" : "+ Nuovo dipendente"}
        </button>
      </div>

      {/* selettore azienda */}
      {tutteAziende.length === 0 ? (
        <div className="empty-state" style={{ marginBottom: 16 }}>
          Nessuna azienda registrata.{" "}
          <a href="#" onClick={e => { e.preventDefault(); window.location.hash = "aziende"; }}>
            Aggiungi un'azienda prima
          </a>.
        </div>
      ) : (
        <div style={{ marginBottom: 16, display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <label style={{ fontSize: 13, fontWeight: 600, whiteSpace: "nowrap" }}>Azienda:</label>
          <select
            value={filtroAziendaId}
            onChange={e => { void cambiaAzienda(e.target.value); }}
            style={{ padding: "7px 12px", borderRadius: 8, border: "1px solid rgba(139, 115, 85, 0.45)",
              background: "#fffdf6", boxShadow: "var(--incavo)", color: "var(--text)", fontSize: 14, flex: 1, maxWidth: 400 }}>
            <option value={TUTTE_LE_AZIENDE}>Tutte le aziende ({tutteAziende.length})</option>
            {tutteAziende.map(a => (
              <option key={a.id} value={a.id}>{a.ragioneSociale}</option>
            ))}
          </select>
        </div>
      )}

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
            <span>Livello CCNL {nomeCcnl(codiceCcnlAzienda(azienda))}</span>
            <select value={form.livello} onChange={set("livello")}>
              {livelli.length === 0
                ? <option value={livelloAzienda(azienda)}>{livelloAzienda(azienda)}</option>
                : livelli.map((l) => <option key={l.codice} value={l.codice}>{l.codice} — {l.descrizione}</option>)}
            </select>
          </label>
          <label>
            <span>IBAN <small>(facoltativo)</small></span>
            <input value={form.iban} onChange={set("iban")} placeholder="IT.." />
          </label>
          <label className="wide-field">
            <span>Comune di residenza <small>(per le addizionali regionale e comunale)</small></span>
            <ComuneAutocomplete
              valore={form.comuneNome}
              placeholder="Es. Milano"
              onSelect={(c) =>
                setForm((f) => ({ ...f, comuneNome: c.nome, codiceCatastaleComune: c.cat, regione: c.reg }))}
            />
          </label>
          <button className="btn primary wide-field" type="submit" disabled={salvataggio || !azienda}>
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
                <th>Azienda</th>
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
                  <td>{d.aziendaNome}</td>
                  <td className="num">{d.dipendenteId.slice(0, 6).toUpperCase()}</td>
                  <td>{d.qualifica}</td>
                  <td>{d.livello}</td>
                  <td>{nomeCcnl(d.codiceCcnl)}</td>
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
    </>
  );
}

function iniziali(nominativo: string): string {
  const parti = nominativo.trim().split(/\s+/).filter(Boolean);
  if (parti.length === 0) return "—";
  return parti.slice(0, 2).map((p) => p[0]?.toUpperCase() ?? "").join("");
}
