import { useEffect, useState, type FormEvent } from "react";
import {
  api, scaricaBlob,
  type Azienda, type Persona, type PersonaReq, type TabelleAa710,
} from "../api";
import { salvaAziendaCorrenteId, scegliAziendaPreferita } from "../aziendaCorrente";

const TIPI = [
  { valore: 1, nome: "1 — Inizio attività con attribuzione di codice fiscale e partita IVA" },
  { valore: 2, nome: "2 — Inizio attività con attribuzione di partita IVA (CF già posseduto)" },
  { valore: 3, nome: "3 — Variazione dati" },
  { valore: 4, nome: "4 — Cessazione attività" },
];

// Cariche per cui il modello chiede la data del provvedimento di nomina (istruzioni AA7/10).
const CARICHE_CON_DATA = ["3", "4", "5", "8", "15"];

const personaVuota: PersonaReq = {
  cognome: "", nome: "", codiceFiscale: "",
  dataNascita: "", comuneNascita: "", provinciaNascita: "",
};

export function Moduli() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [persone, setPersone] = useState<Persona[]>([]);
  const [tabelle, setTabelle] = useState<TabelleAa710 | null>(null);
  const [caricamento, setCaricamento] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);
  const [esito, setEsito] = useState<string | null>(null);

  // Nuova persona
  const [mostraFormPersona, setMostraFormPersona] = useState(false);
  const [nuova, setNuova] = useState<PersonaReq>(personaVuota);
  const [salvataggio, setSalvataggio] = useState(false);

  // Compilazione AA7/10
  const [aziendaId, setAziendaId] = useState("");
  const [personaId, setPersonaId] = useState("");
  const [tipo, setTipo] = useState(3);
  const [data, setData] = useState(() => new Date().toISOString().slice(0, 10));
  const [natura, setNatura] = useState("2");
  const [carica, setCarica] = useState("1");
  const [sigla, setSigla] = useState("");
  const [scrittureInSede, setScrittureInSede] = useState(true);
  const [descrizioneAttivita, setDescrizioneAttivita] = useState("");
  const [volumeAffari, setVolumeAffari] = useState("");
  const [dataNomina, setDataNomina] = useState("");
  const [estremi, setEstremi] = useState({ data: "", ufficio: "", numero: "", sottoNumero: "", serie: "" });
  const [generazione, setGenerazione] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const [az, pe, tb] = await Promise.all([api.aziende(), api.persone(), api.tabelleAa710()]);
        setAziende(az);
        setPersone(pe);
        setTabelle(tb);
        const scelta = scegliAziendaPreferita(az);
        if (scelta) setAziendaId(scelta.id);
        if (pe.length > 0) setPersonaId(pe[0].id);
      } catch (e) {
        setErrore(e instanceof Error ? e.message : "Errore di caricamento");
      } finally {
        setCaricamento(false);
      }
    })();
  }, []);

  const azienda = aziende.find((a) => a.id === aziendaId) ?? null;

  async function salvaPersona(e: FormEvent) {
    e.preventDefault();
    setSalvataggio(true);
    setErrore(null);
    try {
      const p = await api.creaPersona({
        ...nuova,
        codiceFiscale: nuova.codiceFiscale.trim().toUpperCase(),
        dataNascita: nuova.dataNascita || undefined,
      });
      const elenco = await api.persone();
      setPersone(elenco);
      setPersonaId(p.id);
      setNuova(personaVuota);
      setMostraFormPersona(false);
      setEsito(`Persona salvata: ${p.cognome} ${p.nome}. Ora la ritrovi in ogni modulo.`);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nel salvataggio");
    } finally {
      setSalvataggio(false);
    }
  }

  async function eliminaPersona(id: string) {
    try {
      await api.eliminaPersona(id);
      setPersone(await api.persone());
      if (personaId === id) setPersonaId("");
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  async function scaricaModulo(e: FormEvent) {
    e.preventDefault();
    setGenerazione(true);
    setErrore(null);
    setEsito(null);
    try {
      const { blob, filename } = await api.compilaAa710({
        aziendaId, personaId,
        tipoDichiarazione: tipo,
        data,
        naturaGiuridica: natura,
        codiceCarica: carica,
        sigla: sigla || undefined,
        scrittureContabiliInSede: scrittureInSede,
        descrizioneAttivita: descrizioneAttivita || undefined,
        volumeAffariPresunto: volumeAffari ? Number(volumeAffari) : undefined,
        dataInizioProcedimento: dataNomina || undefined,
        estremiRegistrazione: tipo === 1 ? {
          data: estremi.data || undefined,
          ufficio: estremi.ufficio || undefined,
          numero: estremi.numero || undefined,
          sottoNumero: estremi.sottoNumero || undefined,
          serie: estremi.serie || undefined,
        } : undefined,
      });
      await scaricaBlob(blob, filename);
      setEsito("Modello AA7/10 ufficiale compilato e scaricato: controllalo, firmalo e presentalo tramite i canali dell'Agenzia delle Entrate (o il tuo intermediario).");
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore nella compilazione");
    } finally {
      setGenerazione(false);
    }
  }

  const setP = (k: keyof PersonaReq) => (e: { target: { value: string } }) =>
    setNuova((f) => ({ ...f, [k]: e.target.value }));

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Moduli ufficiali — AA7/10</h1>
          <p>
            Scegli azienda e persona dagli archivi: il modulo si compila da solo e scarichi
            il <strong>PDF ufficiale dell'Agenzia delle Entrate</strong> (non un fac-simile).
          </p>
        </div>
      </div>

      {errore && <p className="errore">{errore}</p>}
      {esito && <p className="ok" style={{ color: "var(--green, #15803d)", fontWeight: 700 }}>{esito}</p>}

      {caricamento ? (
        <div className="empty-state">Caricamento…</div>
      ) : (
        <>
          {/* ---- Archivio persone ---- */}
          <div className="card-title-row" style={{ marginTop: 8 }}>
            <div>
              <h2 style={{ fontSize: 17, margin: 0 }}>1 · Le tue persone</h2>
              <p style={{ margin: 0 }}>Registra una volta rappresentanti e soci: niente più dati ricopiati a mano.</p>
            </div>
            <button className="btn primary" type="button" onClick={() => setMostraFormPersona((v) => !v)}>
              {mostraFormPersona ? "Chiudi" : "+ Nuova persona"}
            </button>
          </div>

          {mostraFormPersona && (
            <form className="form-grid" onSubmit={salvaPersona} style={{ marginBottom: 8 }}>
              <label><span>Cognome</span><input value={nuova.cognome} onChange={setP("cognome")} required /></label>
              <label><span>Nome</span><input value={nuova.nome} onChange={setP("nome")} required /></label>
              <label><span>Codice fiscale</span><input value={nuova.codiceFiscale} onChange={setP("codiceFiscale")} placeholder="RSSMRA80C15L117V" maxLength={16} required /></label>
              <label><span>Data di nascita</span><input type="date" value={nuova.dataNascita ?? ""} onChange={setP("dataNascita")} /></label>
              <label><span>Comune di nascita</span><input value={nuova.comuneNascita ?? ""} onChange={setP("comuneNascita")} placeholder="Es. Terni" /></label>
              <label><span>Provincia (sigla)</span><input value={nuova.provinciaNascita ?? ""} onChange={setP("provinciaNascita")} placeholder="TR" maxLength={2} /></label>
              <button className="btn primary wide-field" type="submit" disabled={salvataggio}>
                {salvataggio ? "Salvataggio…" : "Salva persona"}
              </button>
            </form>
          )}

          {persone.length === 0 ? (
            <div className="empty-state">Nessuna persona in archivio. Premi “+ Nuova persona”: la inserisci una volta sola.</div>
          ) : (
            <div className="table-wrap" style={{ marginBottom: 14 }}>
              <table className="data-table">
                <thead>
                  <tr><th>Persona</th><th>Codice fiscale</th><th>Nascita</th><th className="right">Azioni</th></tr>
                </thead>
                <tbody>
                  {persone.map((p) => (
                    <tr key={p.id}>
                      <td><strong>{p.cognome} {p.nome}</strong></td>
                      <td className="num">{p.codiceFiscale}</td>
                      <td>{p.dataNascita ? new Date(p.dataNascita).toLocaleDateString("it-IT") : "—"}{p.comuneNascita ? ` · ${p.comuneNascita}${p.provinciaNascita ? ` (${p.provinciaNascita})` : ""}` : ""}</td>
                      <td className="right">
                        <button className="mini-action" type="button" onClick={() => eliminaPersona(p.id)} style={{ color: "var(--red)" }}>Elimina</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* ---- Compilazione AA7/10 ---- */}
          <div className="card-title-row">
            <div>
              <h2 style={{ fontSize: 17, margin: 0 }}>2 · Compila il modello AA7/10</h2>
              <p style={{ margin: 0 }}>
                Inizio attività, variazione dati o cessazione ai fini IVA per società ed enti
                (soggetti diversi dalle persone fisiche).
              </p>
            </div>
          </div>

          <form className="form-grid" onSubmit={scaricaModulo}>
            <label>
              <span>Azienda (dati già in archivio)</span>
              <select value={aziendaId} onChange={(e) => { setAziendaId(e.target.value); salvaAziendaCorrenteId(e.target.value); }} required>
                <option value="" disabled>Scegli l'azienda…</option>
                {aziende.map((a) => <option key={a.id} value={a.id}>{a.ragioneSociale}</option>)}
              </select>
            </label>
            <label>
              <span>Rappresentante (dall'archivio persone)</span>
              <select value={personaId} onChange={(e) => setPersonaId(e.target.value)} required>
                <option value="" disabled>Scegli la persona…</option>
                {persone.map((p) => <option key={p.id} value={p.id}>{p.cognome} {p.nome} — {p.codiceFiscale}</option>)}
              </select>
            </label>
            <label className="wide-field">
              <span>Tipo di dichiarazione (quadro A)</span>
              <select value={tipo} onChange={(e) => setTipo(Number(e.target.value))}>
                {TIPI.map((t) => <option key={t.valore} value={t.valore}>{t.nome}</option>)}
              </select>
            </label>
            <label>
              <span>{tipo === 3 ? "Data variazione" : tipo === 4 ? "Data cessazione" : "Data inizio attività"}</span>
              <input type="date" value={data} onChange={(e) => setData(e.target.value)} required />
            </label>
            <label>
              <span>Natura giuridica (tabella ufficiale)</span>
              <select value={natura} onChange={(e) => setNatura(e.target.value)}>
                {tabelle?.natureGiuridiche.map((n) => (
                  <option key={n.codice} value={n.codice}>{n.codice} — {n.descrizione}</option>
                ))}
              </select>
            </label>
            <label>
              <span>Carica del rappresentante</span>
              <select value={carica} onChange={(e) => setCarica(e.target.value)}>
                {tabelle?.codiciCarica.map((c) => (
                  <option key={c.codice} value={c.codice}>{c.codice} — {c.descrizione}</option>
                ))}
              </select>
            </label>
            {CARICHE_CON_DATA.includes(carica) && (
              <label>
                <span>Data del provvedimento di nomina</span>
                <input type="date" value={dataNomina} onChange={(e) => setDataNomina(e.target.value)} />
              </label>
            )}
            <label><span>Sigla (facoltativa)</span><input value={sigla} onChange={(e) => setSigla(e.target.value)} placeholder="Es. Alfa Srl" /></label>
            <label>
              <span>Descrizione attività</span>
              <input value={descrizioneAttivita} onChange={(e) => setDescrizioneAttivita(e.target.value)}
                placeholder={azienda?.settore ?? "Es. Produzione di software"} />
            </label>
            {(tipo === 1 || tipo === 2) && (
              <label><span>Volume d'affari presunto (€)</span><input type="number" min={0} step={1} value={volumeAffari} onChange={(e) => setVolumeAffari(e.target.value)} /></label>
            )}
            <label className="wide-field" style={{ flexDirection: "row", alignItems: "center", gap: 8, display: "flex" }}>
              <input type="checkbox" style={{ width: 18, height: 18, minHeight: 0 }} checked={scrittureInSede} onChange={(e) => setScrittureInSede(e.target.checked)} />
              <span>Le scritture contabili sono conservate presso la sede</span>
            </label>

            {tipo === 1 && (
              <>
                <p className="wide-field" style={{ margin: "2px 0 0", fontSize: 12.5, fontWeight: 800, color: "var(--muted)" }}>
                  Estremi di registrazione dell'atto costitutivo (facoltativi, v. istruzioni)
                </p>
                <label><span>Data registrazione</span><input type="date" value={estremi.data} onChange={(e) => setEstremi((s) => ({ ...s, data: e.target.value }))} /></label>
                <label><span>Ufficio</span><input value={estremi.ufficio} onChange={(e) => setEstremi((s) => ({ ...s, ufficio: e.target.value }))} maxLength={3} placeholder="TR3" /></label>
                <label><span>Numero</span><input value={estremi.numero} onChange={(e) => setEstremi((s) => ({ ...s, numero: e.target.value }))} maxLength={6} /></label>
                <label><span>Sotto numero</span><input value={estremi.sottoNumero} onChange={(e) => setEstremi((s) => ({ ...s, sottoNumero: e.target.value }))} maxLength={3} /></label>
                <label><span>Serie</span><input value={estremi.serie} onChange={(e) => setEstremi((s) => ({ ...s, serie: e.target.value }))} maxLength={2} /></label>
              </>
            )}

            {azienda && (
              <p className="wide-field" style={{ margin: 0, fontSize: 12.5, color: "var(--muted)" }}>
                Dal profilo azienda verranno usati: denominazione «{azienda.ragioneSociale}», sede{" "}
                {azienda.indirizzo ?? "—"}, {azienda.cap ?? ""} {azienda.comune ?? ""} ({azienda.provincia ?? "—"})
                {azienda.codiceAteco ? `, codice attività ${azienda.codiceAteco}` : ""}
                {tipo >= 3 ? `, P.IVA ${azienda.partitaIva ?? "MANCANTE — aggiungila in Aziende"}` : ""}
                {tipo >= 2 ? `, CF ${azienda.codiceFiscale ?? "MANCANTE — aggiungilo in Aziende"}` : ""}.
              </p>
            )}

            <button className="btn primary wide-field" type="submit" disabled={generazione || !aziendaId || !personaId}>
              {generazione ? "Compilazione…" : "⬇︎ Scarica il modello AA7/10 ufficiale compilato"}
            </button>
          </form>

          <p style={{ fontSize: 12.5, color: "var(--muted)", marginTop: 12 }}>
            ℹ️ Il file scaricato è il modello ufficiale AdE 2025 con i tuoi dati scritti nelle caselle.
            Per le <strong>nuove imprese</strong> iscritte al Registro Imprese l'apertura IVA passa da{" "}
            <strong>ComUnica</strong> (vedi Pratiche): l'AA7/10 da solo serve per variazioni, cessazioni
            ed enti non tenuti all'iscrizione (associazioni, condomini, enti). La presentazione resta
            sui canali ufficiali dell'Agenzia o tramite intermediario abilitato.
          </p>
        </>
      )}
    </section>
  );
}
