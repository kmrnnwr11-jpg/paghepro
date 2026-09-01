import { useEffect, useState, type FormEvent } from "react";
import { ExternalLink, Settings2, ShieldCheck, Sparkles } from "lucide-react";
import { api } from "../api";
import {
  applicaAspetto,
  preferenzaLocale,
  salvaPreferenzaOvunque,
  sincronizzaPreferenzeDalServer,
} from "../preferenze";

const COLLEGAMENTI: { hash: string; titolo: string; descrizione: string }[] = [
  { hash: "aziende", titolo: "Scheda azienda", descrizione: "Dati fiscali, INPS/INAIL, CCNL, logo dei documenti" },
  { hash: "collega-sdi", titolo: "Collega Fatturazione", descrizione: "Credenziali del canale SdI" },
  { hash: "utenti", titolo: "Utenti e permessi", descrizione: "Chi può fare cosa nello studio" },
  { hash: "audit", titolo: "Registro attività", descrizione: "Chi ha letto o modificato cosa" },
  { hash: "retention", titolo: "Conservazione dati", descrizione: "Cancellazione e retention GDPR" },
  { hash: "scadenzario", titolo: "Scadenze e promemoria", descrizione: "DURC, F24 e messaggi precompilati" },
];

export function Impostazioni() {
  const [pronto, setPronto] = useState(false);
  const [stile, setStile] = useState("classico");
  const [fontDocumenti, setFontDocumenti] = useState("lato");
  const [dicitura, setDicitura] = useState("");
  const [dicituraSalvata, setDicituraSalvata] = useState<string | null>(null);
  const [risposteBrevi, setRisposteBrevi] = useState("0");
  const [scala, setScala] = useState("normale");
  const [fontUi, setFontUi] = useState("sistema");
  const [densita, setDensita] = useState("comoda");
  const [ai, setAi] = useState<{ provider: string; attivo: boolean; consumoMese: number; limiteMensile: number } | null>(null);

  // Sicurezza
  const [pwAttuale, setPwAttuale] = useState("");
  const [pwNuova, setPwNuova] = useState("");
  const [pwConferma, setPwConferma] = useState("");
  const [esitoPassword, setEsitoPassword] = useState<string | null>(null);
  const [dueFattori, setDueFattori] = useState<{ segreto: string; uriOtpauth: string } | null>(null);
  const [codice2fa, setCodice2fa] = useState("");
  const [esito2fa, setEsito2fa] = useState<string | null>(null);

  useEffect(() => {
    let attivo = true;
    (async () => {
      await sincronizzaPreferenzeDalServer();
      if (!attivo) return;
      setStile(preferenzaLocale("stile_busta", "classico") || "classico");
      setFontDocumenti(preferenzaLocale("font_documenti", "lato") || "lato");
      setDicitura(preferenzaLocale("dicitura_studio", ""));
      setRisposteBrevi(preferenzaLocale("risposte_brevi_aurora", "0"));
      setScala(preferenzaLocale("scala_testo", "normale") || "normale");
      setFontUi(preferenzaLocale("font_ui", "sistema") || "sistema");
      setDensita(preferenzaLocale("densita", "comoda") || "comoda");
      setPronto(true);
      api.statoAi().then((stato) => { if (attivo) setAi(stato); }).catch(() => { /* informativo */ });
    })();
    return () => { attivo = false; };
  }, []);

  function preferenza(chiave: string, valore: string, applica = false) {
    salvaPreferenzaOvunque(chiave, valore);
    if (applica) applicaAspetto();
  }

  async function inviaPassword(evento: FormEvent) {
    evento.preventDefault();
    setEsitoPassword(null);
    if (pwNuova !== pwConferma) { setEsitoPassword("La conferma non coincide con la nuova password."); return; }
    try {
      await api.cambiaPassword(pwAttuale, pwNuova);
      setEsitoPassword("Password cambiata.");
      setPwAttuale(""); setPwNuova(""); setPwConferma("");
    } catch (e) {
      setEsitoPassword(e instanceof Error ? e.message : "Impossibile cambiare la password.");
    }
  }

  async function inizia2fa() {
    setEsito2fa(null);
    try { setDueFattori(await api.dueFattoriInizia()); }
    catch (e) { setEsito2fa(e instanceof Error ? e.message : "Impossibile iniziare il 2FA."); }
  }

  async function attiva2fa() {
    setEsito2fa(null);
    try {
      await api.dueFattoriAttiva(codice2fa.trim());
      setEsito2fa("Verifica in due passaggi ATTIVA. Conserva il segreto in un posto sicuro.");
      setDueFattori(null); setCodice2fa("");
    } catch (e) { setEsito2fa(e instanceof Error ? e.message : "Codice non valido."); }
  }

  async function disattiva2fa() {
    setEsito2fa(null);
    try {
      await api.dueFattoriDisattiva(codice2fa.trim());
      setEsito2fa("Verifica in due passaggi disattivata.");
      setCodice2fa("");
    } catch (e) { setEsito2fa(e instanceof Error ? e.message : "Codice non valido."); }
  }

  if (!pronto)
    return <section className="glass-card page-card"><div className="empty-state">Caricamento preferenze…</div></section>;

  return (
    <section className="glass-card page-card">
      <div className="card-title-row">
        <div>
          <h1>Impostazioni</h1>
          <p>Preferenze sincronizzate col tuo account: ti seguono su ogni dispositivo collegato.</p>
        </div>
      </div>

      <div className="card-title-row" style={{ marginTop: 6 }}>
        <div><h2><Settings2 size={15} aria-hidden="true" /> Documenti</h2></div>
      </div>
      <div className="form-grid">
        <label>
          <span>Stile predefinito della busta paga</span>
          <select value={stile} onChange={(e) => { setStile(e.target.value); preferenza("stile_busta", e.target.value); }}>
            <option value="classico">Classico (prospetto professionale)</option>
            <option value="colori">Colori (riquadri colorati)</option>
            <option value="editoriale">Editoriale (impaginazione estesa)</option>
          </select>
        </label>
        <label>
          <span>Font dei documenti PDF</span>
          <select value={fontDocumenti} onChange={(e) => { setFontDocumenti(e.target.value); preferenza("font_documenti", e.target.value); }}>
            <option value="lato">Lato (predefinito)</option>
            <option value="carlito">Carlito (stile Calibri)</option>
            <option value="plex">IBM Plex Sans</option>
          </select>
          <small style={{ display: "block", marginTop: 4, opacity: .7 }}>
            Font liberi incorporati nel PDF. Cambia solo l'aspetto: i numeri restano identici.
            I documenti chiusi restano come sono stati congelati.
          </small>
        </label>
        <label className="wide-field">
          <span>Dicitura del tuo studio a piè di pagina (facoltativa, max 90 caratteri)</span>
          <div style={{ display: "flex", gap: 8 }}>
            <input type="text" maxLength={90} value={dicitura} placeholder="es. Elaborato da Studio Rossi — Piacenza"
              style={{ flex: 1 }}
              onChange={(e) => { setDicitura(e.target.value); setDicituraSalvata(null); }} />
            <button className="btn secondary" type="button"
              onClick={() => { preferenza("dicitura_studio", dicitura.trim()); setDicituraSalvata("Dicitura salvata."); }}>
              Salva
            </button>
          </div>
          {dicituraSalvata && <small role="status" style={{ display: "block", marginTop: 4 }}>{dicituraSalvata}</small>}
          <small style={{ display: "block", marginTop: 4, opacity: .7 }}>
            Compare in fondo a buste e prospetti, accanto alle diciture obbligatorie — mai al posto loro.
          </small>
        </label>
      </div>

      <div className="card-title-row" style={{ marginTop: 18 }}>
        <div><h2>Aspetto dell'interfaccia</h2></div>
      </div>
      <div className="form-grid">
        <label>
          <span>Dimensione del testo</span>
          <select value={scala} onChange={(e) => { setScala(e.target.value); preferenza("scala_testo", e.target.value, true); }}>
            <option value="normale">Normale</option>
            <option value="grande">Grande</option>
            <option value="molto-grande">Molto grande</option>
          </select>
        </label>
        <label>
          <span>Font dell'interfaccia</span>
          <select value={fontUi} onChange={(e) => { setFontUi(e.target.value); preferenza("font_ui", e.target.value, true); }}>
            <option value="sistema">Sistema</option>
            <option value="lato">Lato (lo stesso dei documenti)</option>
          </select>
        </label>
        <label>
          <span>Densità</span>
          <select value={densita} onChange={(e) => { setDensita(e.target.value); preferenza("densita", e.target.value, true); }}>
            <option value="comoda">Comoda</option>
            <option value="compatta">Compatta (più righe a schermo)</option>
          </select>
        </label>
      </div>

      <div className="card-title-row" style={{ marginTop: 18 }}>
        <div><h2><Sparkles size={15} aria-hidden="true" /> Aurora</h2></div>
      </div>
      <div className="form-grid">
        <label>
          <span>Stile delle risposte</span>
          <select value={risposteBrevi} onChange={(e) => { setRisposteBrevi(e.target.value); preferenza("risposte_brevi_aurora", e.target.value); }}>
            <option value="0">Risposte normali</option>
            <option value="1">Risposte brevi (2-3 frasi)</option>
          </select>
          <small style={{ display: "block", marginTop: 4, opacity: .7 }}>
            Cambia solo lo stile, mai permessi o strumenti. Il consenso pieno si gestisce dal pannello Aurora.
          </small>
        </label>
        <div className="empty-state">
          {ai
            ? <>Assistente: <strong>{ai.attivo ? ai.provider : "spento"}</strong>
              {ai.attivo && <> · consumo mese {ai.consumoMese}/{ai.limiteMensile === 0 ? "∞" : ai.limiteMensile}</>}</>
            : "Stato assistente non disponibile."}
        </div>
      </div>

      <div className="card-title-row" style={{ marginTop: 18 }}>
        <div><h2><ShieldCheck size={15} aria-hidden="true" /> Sicurezza dell'account</h2></div>
      </div>
      <div className="form-grid">
        <form onSubmit={(e) => void inviaPassword(e)} className="wide-field" style={{ display: "grid", gap: 8 }}>
          <span><strong>Cambia password</strong></span>
          <input type="password" autoComplete="current-password" placeholder="Password attuale"
            value={pwAttuale} onChange={(e) => setPwAttuale(e.target.value)} />
          <input type="password" autoComplete="new-password" placeholder="Nuova password (min 8 caratteri)"
            value={pwNuova} onChange={(e) => setPwNuova(e.target.value)} />
          <input type="password" autoComplete="new-password" placeholder="Ripeti la nuova password"
            value={pwConferma} onChange={(e) => setPwConferma(e.target.value)} />
          <div><button className="btn secondary" type="submit" disabled={!pwAttuale || !pwNuova}>Cambia password</button></div>
          {esitoPassword && <small role="status">{esitoPassword}</small>}
        </form>

        <div className="wide-field" style={{ display: "grid", gap: 8 }}>
          <span><strong>Verifica in due passaggi (2FA)</strong></span>
          {!dueFattori ? (
            <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
              <button className="btn secondary" type="button" onClick={() => void inizia2fa()}>Attiva 2FA</button>
              <input type="text" inputMode="numeric" placeholder="codice a 6 cifre" style={{ maxWidth: 150 }}
                value={codice2fa} onChange={(e) => setCodice2fa(e.target.value)} />
              <button className="btn secondary" type="button" disabled={!codice2fa.trim()}
                onClick={() => void disattiva2fa()}>Disattiva col codice</button>
            </div>
          ) : (
            <div style={{ display: "grid", gap: 6 }}>
              <small>1. Apri l'app di autenticazione (Google Authenticator o simile) e aggiungi un account con questo segreto:</small>
              <code style={{ userSelect: "all" }}>{dueFattori.segreto}</code>
              <small>oppure apri il collegamento: <a href={dueFattori.uriOtpauth}>{dueFattori.uriOtpauth.slice(0, 60)}…</a></small>
              <small>2. Inserisci il codice a 6 cifre che l'app mostra e conferma:</small>
              <div style={{ display: "flex", gap: 8 }}>
                <input type="text" inputMode="numeric" placeholder="codice" style={{ maxWidth: 150 }}
                  value={codice2fa} onChange={(e) => setCodice2fa(e.target.value)} />
                <button className="btn primary" type="button" disabled={!codice2fa.trim()}
                  onClick={() => void attiva2fa()}>Conferma e attiva</button>
              </div>
            </div>
          )}
          {esito2fa && <small role="status">{esito2fa}</small>}
        </div>
      </div>

      <div className="card-title-row" style={{ marginTop: 18 }}>
        <div>
          <h2>Configurazione dello studio</h2>
          <p>Ogni voce apre la pagina dedicata dove i dati si modificano e si salvano davvero.</p>
        </div>
      </div>
      <div className="settings-grid">
        {COLLEGAMENTI.map((voce) => (
          <article className="settings-card" key={voce.hash}
            style={{ cursor: "pointer" }}
            onClick={() => { window.location.hash = `#${voce.hash}`; }}>
            <div>
              <strong>{voce.titolo}</strong>
              <span>{voce.descrizione}</span>
            </div>
            <em className="status-pill blue"><ExternalLink size={12} aria-hidden="true" /> Apri</em>
          </article>
        ))}
      </div>
    </section>
  );
}
