import { useEffect, useMemo, useState } from "react";
import { api, type CasellaEmail, type MessaggioEmail, type NuovaCasellaEmail } from "../api";

type FiltroPosta = "tutti" | "nonLetti" | "urgenti" | "pubblicita" | "daConfermare";

function dataOraIt(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : d.toLocaleString("it-IT", { dateStyle: "short", timeStyle: "short" });
}

function proposteInAttesa(m: MessaggioEmail): boolean {
  return m.urgente === null && m.pubblicita === null
    && (m.urgenteProposto !== null || m.pubblicitaProposto !== null);
}

export function Posta() {
  const [caselle, setCaselle] = useState<CasellaEmail[]>([]);
  const [messaggi, setMessaggi] = useState<MessaggioEmail[]>([]);
  const [filtro, setFiltro] = useState<FiltroPosta>("tutti");
  const [caricando, setCaricando] = useState(false);
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [sincronizzando, setSincronizzando] = useState<string | null>(null);

  const [selezionato, setSelezionato] = useState<MessaggioEmail | null>(null);
  const [corpo, setCorpo] = useState<string | null>(null);
  const [corpoInCaricamento, setCorpoInCaricamento] = useState(false);
  const [proponendo, setProponendo] = useState(false);
  const [confermando, setConfermando] = useState(false);
  const [urgenteScelto, setUrgenteScelto] = useState(false);
  const [pubblicitaScelto, setPubblicitaScelto] = useState(false);

  // Form collegamento nuova casella
  const [etichetta, setEtichetta] = useState("");
  const [tipo, setTipo] = useState("Email");
  const [host, setHost] = useState("");
  const [porta, setPorta] = useState(993);
  const [utente, setUtente] = useState("");
  const [password, setPassword] = useState("");
  const [ssl, setSsl] = useState(true);
  const [salvandoCasella, setSalvandoCasella] = useState(false);
  const casellaRegistrabile = etichetta.trim().length > 0 && host.trim().length > 0 && utente.trim().length > 0;

  async function carica() {
    setCaricando(true);
    setErrore(null);
    try {
      const [c, m] = await Promise.all([api.caselleEmail(), api.messaggiEmail()]);
      setCaselle(c);
      setMessaggi(m);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore di caricamento");
    } finally {
      setCaricando(false);
    }
  }

  useEffect(() => {
    void carica();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const conteggi = useMemo(() => ({
    nonLetti: messaggi.filter((m) => !m.letto).length,
    urgenti: messaggi.filter((m) => m.urgente === true).length,
    pubblicita: messaggi.filter((m) => m.pubblicita === true).length,
    daConfermare: messaggi.filter(proposteInAttesa).length,
  }), [messaggi]);

  const filtrati = useMemo(() => {
    switch (filtro) {
      case "nonLetti": return messaggi.filter((m) => !m.letto);
      case "urgenti": return messaggi.filter((m) => m.urgente === true);
      case "pubblicita": return messaggi.filter((m) => m.pubblicita === true);
      case "daConfermare": return messaggi.filter(proposteInAttesa);
      default: return messaggi;
    }
  }, [messaggi, filtro]);

  async function sincronizza(id: string): Promise<number> {
    setSincronizzando(id);
    try {
      const esito = await api.sincronizzaCasellaEmail(id);
      if (esito.errore) setErrore((prev) => prev ?? esito.errore);
      return esito.nuoviMessaggi;
    } catch (e) {
      setErrore((prev) => prev ?? (e instanceof Error ? e.message : "Errore di sincronizzazione"));
      return 0;
    }
  }

  async function sincronizzaUna(id: string) {
    setErrore(null);
    setInfo(null);
    const nuovi = await sincronizza(id);
    setSincronizzando(null);
    setInfo(nuovi > 0 ? `${nuovi} nuovi messaggi importati.` : "Nessun nuovo messaggio.");
    await carica();
  }

  async function sincronizzaTutte() {
    setErrore(null);
    setInfo(null);
    let nuovi = 0;
    for (const c of caselle) nuovi += await sincronizza(c.id);
    setSincronizzando(null);
    setInfo(nuovi > 0 ? `${nuovi} nuovi messaggi importati.` : "Nessun nuovo messaggio.");
    await carica();
  }

  async function creaCasella() {
    if (!casellaRegistrabile) {
      setErrore("Compila etichetta, host e utente della casella.");
      return;
    }
    setSalvandoCasella(true);
    setErrore(null);
    try {
      const nuova: NuovaCasellaEmail = {
        etichetta: etichetta.trim(), tipo, host: host.trim(), porta,
        utente: utente.trim(), password: password || undefined, ssl, attiva: true,
      };
      await api.creaCasellaEmail(nuova);
      setEtichetta(""); setHost(""); setUtente(""); setPassword("");
      setInfo("Casella collegata. Sincronizzala per importare i messaggi.");
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setSalvandoCasella(false);
    }
  }

  async function eliminaCasella(id: string) {
    setErrore(null);
    try {
      await api.eliminaCasellaEmail(id);
      if (selezionato?.casellaId === id) setSelezionato(null);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  async function apri(m: MessaggioEmail) {
    setSelezionato(m);
    setCorpo(null);
    setUrgenteScelto(m.urgente ?? m.urgenteProposto ?? false);
    setPubblicitaScelto(m.pubblicita ?? m.pubblicitaProposto ?? false);
    setCorpoInCaricamento(true);
    setErrore(null);
    try {
      const r = await api.corpoMessaggioEmail(m.id);
      setCorpo(r.corpo);
      if (r.errore) setErrore(r.errore);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setCorpoInCaricamento(false);
    }
  }

  async function proponiAi() {
    if (!selezionato) return;
    setProponendo(true);
    setErrore(null);
    try {
      const aggiornato = await api.proponiTriageEmail(selezionato.id);
      setSelezionato(aggiornato);
      setUrgenteScelto(aggiornato.urgente ?? aggiornato.urgenteProposto ?? false);
      setPubblicitaScelto(aggiornato.pubblicita ?? aggiornato.pubblicitaProposto ?? false);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setProponendo(false);
    }
  }

  async function confermaTriage() {
    if (!selezionato) return;
    setConfermando(true);
    setErrore(null);
    try {
      await api.confermaTriageEmail(selezionato.id, urgenteScelto, pubblicitaScelto);
      setInfo("Triage confermato.");
      setSelezionato((prev) => prev && ({
        ...prev, urgente: urgenteScelto, pubblicita: pubblicitaScelto,
        urgenteProposto: null, pubblicitaProposto: null, fonteProposta: null,
      }));
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setConfermando(false);
    }
  }

  async function marcaLetto(m: MessaggioEmail, letto: boolean) {
    setErrore(null);
    try {
      await api.marcaLettoEmail(m.id, letto);
      await carica();
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    }
  }

  const tabs: { key: FiltroPosta; label: string; conteggio: number }[] = [
    { key: "tutti", label: "Tutti", conteggio: messaggi.length },
    { key: "nonLetti", label: "Non letti", conteggio: conteggi.nonLetti },
    { key: "urgenti", label: "Urgenti", conteggio: conteggi.urgenti },
    { key: "pubblicita", label: "Pubblicità", conteggio: conteggi.pubblicita },
    { key: "daConfermare", label: "Da confermare", conteggio: conteggi.daConfermare },
  ];

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Posta</h1>
            <p>Email e PEC collegate · l'AI propone urgente/pubblicità, tu confermi</p>
          </div>
          <button className="btn primary" type="button" onClick={sincronizzaTutte} disabled={caselle.length === 0 || sincronizzando !== null}>
            {sincronizzando !== null ? "Sincronizzo…" : "↻ Sincronizza"}
          </button>
        </div>

        {errore && <div className="alert error">{errore}</div>}
        {info && <div className="alert info">{info}</div>}

        <div className="metric-strip">
          <Metric label="Non letti" value={String(conteggi.nonLetti)} />
          <Metric label="Urgenti" value={String(conteggi.urgenti)} />
          <Metric label="Pubblicità" value={String(conteggi.pubblicita)} />
          <Metric label="Da confermare" value={String(conteggi.daConfermare)} />
        </div>

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>Messaggi</h2>
              <p>Il corpo si legge solo quando apri un messaggio; il triage resta una proposta finché non lo confermi</p>
            </div>
          </div>

          <div className="qw-tabs" role="tablist">
            {tabs.map((t) => (
              <button
                key={t.key}
                className={`qw-tab ${filtro === t.key ? "attiva" : ""}`}
                type="button"
                role="tab"
                aria-selected={filtro === t.key}
                onClick={() => setFiltro(t.key)}
              >
                {t.label}
                {t.conteggio > 0 && <span className="qw-count">{t.conteggio}</span>}
              </button>
            ))}
          </div>

          {caricando ? (
            <p className="muted">Caricamento…</p>
          ) : caselle.length === 0 ? (
            <div className="empty-state" style={{ display: "grid", gap: 10 }}>
              <strong>Nessuna casella email o PEC collegata.</strong>
              <span>Collegala dal pannello a destra per iniziare a importare i messaggi.</span>
            </div>
          ) : filtrati.length === 0 ? (
            <p className="muted">Nessun messaggio in questa vista.</p>
          ) : (
            <div className="table-wrap">
              <table className="data-table spacious">
                <thead>
                  <tr>
                    <th>Messaggio</th>
                    <th>Data</th>
                    <th>Stato</th>
                    <th className="right">Azioni</th>
                  </tr>
                </thead>
                <tbody>
                  {filtrati.map((m) => (
                    <tr key={m.id} onClick={() => void apri(m)} style={{ cursor: "pointer" }}>
                      <td>
                        <strong>{m.oggetto || "(senza oggetto)"}</strong>
                        <div className="muted">{m.mittente}</div>
                        {m.anteprima && <div className="muted">{m.anteprima}</div>}
                      </td>
                      <td className="num">{dataOraIt(m.dataRicezione)}</td>
                      <td>
                        {!m.letto && <span className="status-pill blue">Non letto</span>}
                        {m.urgente === true && <span className="tag red">Urgente</span>}
                        {m.pubblicita === true && <span className="tag slate">Pubblicità</span>}
                        {proposteInAttesa(m) && <span className="tag amber">Da confermare</span>}
                      </td>
                      <td className="right" onClick={(e) => e.stopPropagation()}>
                        <button className="mini-action" type="button" onClick={() => void apri(m)}>Apri</button>
                        <button className="mini-action" type="button" onClick={() => void marcaLetto(m, !m.letto)}>
                          {m.letto ? "Segna non letto" : "Segna letto"}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        {selezionato && (
          <section className="glass-card page-card">
            <div className="card-title-row">
              <div>
                <h2>{selezionato.oggetto || "(senza oggetto)"}</h2>
                <p>Da {selezionato.mittente} · {dataOraIt(selezionato.dataRicezione)}</p>
              </div>
              <div className="actions-row">
                <button className="btn secondary" type="button" onClick={proponiAi} disabled={proponendo}>
                  {proponendo ? "Chiedo all'AI…" : "Proponi con AI"}
                </button>
                <button className="btn secondary" type="button" onClick={() => setSelezionato(null)}>Chiudi</button>
              </div>
            </div>

            {corpoInCaricamento ? (
              <p className="muted">Lettura del messaggio…</p>
            ) : (
              <div className="empty-state" style={{ whiteSpace: "pre-wrap", textAlign: "left" }}>
                {corpo || selezionato.anteprima || "Nessun contenuto leggibile."}
              </div>
            )}

            <div className="actions-row" style={{ marginTop: 12, alignItems: "center" }}>
              <label className="field" style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
                <input type="checkbox" checked={urgenteScelto} onChange={(e) => setUrgenteScelto(e.target.checked)} />
                <span>
                  Urgente
                  {selezionato.urgente === null && selezionato.urgenteProposto !== null
                    && ` (proposta AI: ${selezionato.urgenteProposto ? "sì" : "no"})`}
                </span>
              </label>
              <label className="field" style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
                <input type="checkbox" checked={pubblicitaScelto} onChange={(e) => setPubblicitaScelto(e.target.checked)} />
                <span>
                  Pubblicità
                  {selezionato.pubblicita === null && selezionato.pubblicitaProposto !== null
                    && ` (proposta ${selezionato.fonteProposta === "Euristica" ? "automatica" : "AI"}: ${selezionato.pubblicitaProposto ? "sì" : "no"})`}
                </span>
              </label>
              <button className="btn primary" type="button" onClick={confermaTriage} disabled={confermando}>
                {confermando ? "Confermo…" : "Conferma"}
              </button>
            </div>
          </section>
        )}
      </div>

      <aside className="glass-card module-rail">
        <h2>Caselle collegate</h2>
        {caselle.length === 0 ? (
          <p className="muted">Nessuna casella ancora.</p>
        ) : (
          <div style={{ display: "grid", gap: 8 }}>
            {caselle.map((c) => (
              <div key={c.id} className="empty-state" style={{ display: "grid", gap: 4 }}>
                <strong>{c.etichetta}</strong>
                <span className="muted">{c.tipo} · {c.utente}</span>
                <span className="muted">
                  {c.ultimaSincronizzazioneIl ? `Ultima sync: ${dataOraIt(c.ultimaSincronizzazioneIl)}` : "Mai sincronizzata"}
                </span>
                <div className="actions-row">
                  <button
                    className="btn secondary compact"
                    type="button"
                    disabled={sincronizzando === c.id}
                    onClick={() => void sincronizzaUna(c.id)}
                  >
                    {sincronizzando === c.id ? "Sincronizzo…" : "Sincronizza"}
                  </button>
                  <button className="btn secondary compact" type="button" onClick={() => eliminaCasella(c.id)}>
                    Rimuovi
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        <h2 style={{ marginTop: 24 }}>Collega casella</h2>
        <p className="muted">IMAP: funziona con email normali e con la PEC (host/porta del tuo provider).</p>
        <label className="field">
          <span>Etichetta</span>
          <input value={etichetta} onChange={(e) => setEtichetta(e.target.value)} placeholder="es. PEC studio" />
        </label>
        <label className="field">
          <span>Tipo</span>
          <select value={tipo} onChange={(e) => setTipo(e.target.value)}>
            <option value="Email">Email</option>
            <option value="Pec">PEC</option>
          </select>
        </label>
        <label className="field">
          <span>Host IMAP</span>
          <input value={host} onChange={(e) => setHost(e.target.value)} placeholder="es. imaps.pec.aruba.it" />
        </label>
        <label className="field">
          <span>Porta</span>
          <input type="number" value={porta} onChange={(e) => setPorta(Number(e.target.value) || 993)} />
        </label>
        <label className="field">
          <span>Utente</span>
          <input value={utente} onChange={(e) => setUtente(e.target.value)} placeholder="nome@dominio.it" />
        </label>
        <label className="field">
          <span>Password</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </label>
        <label className="field" style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <input type="checkbox" checked={ssl} onChange={(e) => setSsl(e.target.checked)} />
          <span>Connessione SSL/TLS</span>
        </label>
        <button className="btn primary" type="button" onClick={creaCasella} disabled={!casellaRegistrabile || salvandoCasella}>
          {salvandoCasella ? "Salvataggio…" : "Collega casella"}
        </button>
      </aside>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
