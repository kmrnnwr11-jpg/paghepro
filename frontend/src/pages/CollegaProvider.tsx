import { useEffect, useState } from "react";
import { getToken } from "../api";

const BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? "http://localhost:5171";

interface ConfigSdi {
  modalita: string;
  provider: string;
  baseUrl: string;
  apiKeyImpostata: boolean;
  endpointInvio: string;
  endpointEsito: string;
  campoId: string;
  campoStato: string;
  firmaLocale: boolean;
  attivo: boolean;
}

async function chiama<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers as Record<string, string>),
    },
  });
  if (!res.ok) throw new Error(`Errore ${res.status}`);
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

const providerPreset: Record<string, { provider: string; baseUrl: string; nota: string }> = {
  aruba: { provider: "Aruba", baseUrl: "https://api.fatturazioneelettronica.aruba.it/", nota: "Diffuso, economico, assistenza in italiano." },
  fattureincloud: { provider: "Fatture in Cloud", baseUrl: "https://api-v2.fattureincloud.it/", nota: "Molto usato da micro-imprese e studi." },
  acube: { provider: "Acube", baseUrl: "https://api.acubeapi.com/", nota: "Pensato per integrazioni software." },
};

export function CollegaProvider() {
  const [cfg, setCfg] = useState<ConfigSdi | null>(null);
  const [apiKey, setApiKey] = useState("");
  const [avanzate, setAvanzate] = useState(false);
  const [salvato, setSalvato] = useState(false);
  const [verifica, setVerifica] = useState<{ ok: boolean; messaggio: string } | null>(null);
  const [errore, setErrore] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    chiama<ConfigSdi>("/api/configurazione-sdi")
      .then(setCfg)
      .catch((e) => setErrore(e instanceof Error ? e.message : "Errore"));
  }, []);

  function set<K extends keyof ConfigSdi>(k: K, v: ConfigSdi[K]) {
    setCfg((c) => (c ? { ...c, [k]: v } : c));
    setSalvato(false);
    setVerifica(null);
  }

  function applicaPreset(key: string) {
    const p = providerPreset[key];
    if (!p || !cfg) return;
    setCfg({ ...cfg, modalita: "Rest", provider: p.provider, baseUrl: p.baseUrl });
    setSalvato(false);
    setVerifica(null);
  }

  async function salva() {
    if (!cfg) return;
    setBusy(true);
    setErrore(null);
    try {
      const body = {
        modalita: cfg.modalita,
        provider: cfg.provider,
        baseUrl: cfg.baseUrl,
        apiKey: apiKey === "" ? null : apiKey, // vuoto = non cambiare la chiave già salvata
        endpointInvio: cfg.endpointInvio,
        endpointEsito: cfg.endpointEsito,
        campoId: cfg.campoId,
        campoStato: cfg.campoStato,
        firmaLocale: cfg.firmaLocale,
        attivo: cfg.attivo,
      };
      const aggiornata = await chiama<ConfigSdi>("/api/configurazione-sdi", {
        method: "PUT",
        body: JSON.stringify(body),
      });
      setCfg(aggiornata);
      setApiKey("");
      setSalvato(true);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setBusy(false);
    }
  }

  async function verificaConnessione() {
    setBusy(true);
    setErrore(null);
    try {
      const r = await chiama<{ ok: boolean; messaggio: string }>("/api/configurazione-sdi/verifica", { method: "POST" });
      setVerifica(r);
    } catch (e) {
      setErrore(e instanceof Error ? e.message : "Errore");
    } finally {
      setBusy(false);
    }
  }

  if (!cfg) {
    return (
      <section className="glass-card page-card">
        {errore ? <p className="errore">{errore}</p> : <p className="muto">Caricamento…</p>}
      </section>
    );
  }

  const isRest = cfg.modalita === "Rest";

  return (
    <div className="module-layout">
      <section className="glass-card form-card">
        <div className="card-title-row">
          <div>
            <h1>Collega Fatturazione Elettronica</h1>
            <p>Collega un provider per inviare davvero le fatture allo SdI — da solo, in 3 minuti.</p>
          </div>
          <span className={`status-pill ${cfg.attivo && isRest ? "green" : "slate"}`}>
            {isRest ? (cfg.attivo ? "Provider attivo" : "Provider non attivo") : cfg.modalita}
          </span>
        </div>

        <label>
          <span style={{ fontWeight: 800, fontSize: 12 }}>Come vuoi inviare le fatture?</span>
          <select value={cfg.modalita} onChange={(e) => set("modalita", e.target.value)}>
            <option value="Manuale">Manuale — l'app prepara il file, lo invii tu dal portale</option>
            <option value="Simulato">Simulato — per fare delle prove senza inviare davvero</option>
            <option value="Rest">Provider accreditato — invio automatico (consigliato)</option>
          </select>
        </label>

        {isRest && (
          <>
            <div>
              <span style={{ fontWeight: 800, fontSize: 12, color: "var(--muted)" }}>Scelta rapida provider</span>
              <div className="actions-row" style={{ marginTop: 8 }}>
                {Object.entries(providerPreset).map(([key, p]) => (
                  <button
                    key={key}
                    type="button"
                    className={`btn secondary ${cfg.provider === p.provider ? "" : ""}`}
                    onClick={() => applicaPreset(key)}
                  >
                    {p.provider}
                  </button>
                ))}
              </div>
            </div>

            <div className="form-grid">
              <label>
                <span>Provider</span>
                <input value={cfg.provider} onChange={(e) => set("provider", e.target.value)} placeholder="Es. Aruba" />
              </label>
              <label>
                <span>Indirizzo API (URL base)</span>
                <input value={cfg.baseUrl} onChange={(e) => set("baseUrl", e.target.value)} placeholder="https://api.provider.it/" />
              </label>
              <label className="wide-field">
                <span>API Key {cfg.apiKeyImpostata && <small>· già impostata, lascia vuoto per non cambiarla</small>}</span>
                <input
                  type="password"
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder={cfg.apiKeyImpostata ? "•••••••••• (salvata)" : "Incolla qui la chiave del provider"}
                  autoComplete="off"
                />
              </label>
            </div>

            <button type="button" className="link-button" style={{ justifySelf: "start" }} onClick={() => setAvanzate((v) => !v)}>
              {avanzate ? "▾" : "▸"} Impostazioni avanzate (di solito non servono)
            </button>
            {avanzate && (
              <div className="form-grid">
                <label><span>Endpoint invio</span><input value={cfg.endpointInvio} onChange={(e) => set("endpointInvio", e.target.value)} /></label>
                <label><span>Endpoint esito</span><input value={cfg.endpointEsito} onChange={(e) => set("endpointEsito", e.target.value)} /></label>
                <label><span>Campo "id" nella risposta</span><input value={cfg.campoId} onChange={(e) => set("campoId", e.target.value)} /></label>
                <label><span>Campo "stato" nella risposta</span><input value={cfg.campoStato} onChange={(e) => set("campoStato", e.target.value)} /></label>
                <label className="wide-field" style={{ flexDirection: "row", alignItems: "center", gap: 8, display: "flex" }}>
                  <input type="checkbox" style={{ width: 18, height: 18, minHeight: 0 }} checked={cfg.firmaLocale} onChange={(e) => set("firmaLocale", e.target.checked)} />
                  <span>Il provider richiede la fattura già firmata da me (.p7m)</span>
                </label>
              </div>
            )}

            <label style={{ flexDirection: "row", alignItems: "center", gap: 8, display: "flex" }}>
              <input type="checkbox" style={{ width: 18, height: 18, minHeight: 0 }} checked={cfg.attivo} onChange={(e) => set("attivo", e.target.checked)} />
              <span style={{ fontWeight: 700, fontSize: 13 }}>Usa questo provider per le trasmissioni</span>
            </label>
          </>
        )}

        <div className="actions-row">
          <button className="btn primary" type="button" onClick={salva} disabled={busy}>
            {busy ? "Salvataggio…" : "Salva"}
          </button>
          {isRest && (
            <button className="btn secondary" type="button" onClick={verificaConnessione} disabled={busy}>
              Verifica connessione
            </button>
          )}
        </div>

        {salvato && <p style={{ color: "#15803d", fontWeight: 700, fontSize: 13 }}>✓ Configurazione salvata.</p>}
        {verifica && (
          <p style={{ color: verifica.ok ? "#15803d" : "var(--red)", fontWeight: 700, fontSize: 13 }}>
            {verifica.ok ? "✓ " : "✕ "}{verifica.messaggio}
          </p>
        )}
        {errore && <p className="errore">{errore}</p>}
      </section>

      <aside className="glass-card module-rail">
        <h2>Guida rapida</h2>
        <ol style={{ margin: 0, paddingLeft: 18, display: "grid", gap: 10, fontSize: 12.5, color: "var(--muted)", fontWeight: 600, lineHeight: 1.45 }}>
          <li>Scegli un <b>provider</b> (Aruba / Fatture in Cloud / Acube) e crea l'account dal loro sito.</li>
          <li>Attiva <b>"Fatturazione Elettronica + API"</b> e copia l'<b>API Key</b>.</li>
          <li>Qui: scegli "Provider accreditato", premi il nome, incolla l'<b>API Key</b> e <b>Salva</b>.</li>
          <li>Premi <b>Verifica connessione</b>: deve dire ✓.</li>
          <li>Vai in <b>Fatturazione</b>, crea una fattura di prova e premi <b>Trasmetti</b>.</li>
        </ol>
        <div className="empty-state" style={{ marginTop: 14 }}>
          Non hai ancora un provider? Usa <b>Simulato</b> per provare tutto il flusso senza inviare davvero.
        </div>
      </aside>
    </div>
  );
}
