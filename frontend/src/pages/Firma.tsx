import { useState, type ReactNode } from "react";
import { api, type EsitoVerifica, type FormatoFirma } from "../api";

type Metodo = "pfx" | "cns";

interface FirmaFatta {
  blob: Blob;
  filename: string;
  esito: EsitoVerifica;
}

export function Firma() {
  const [formato, setFormato] = useState<FormatoFirma>("cades");
  const [metodo, setMetodo] = useState<Metodo>("pfx");
  const [file, setFile] = useState<File | null>(null);
  const [anteprima, setAnteprima] = useState<string | null>(null);
  const [pfx, setPfx] = useState<File | null>(null);
  const [password, setPassword] = useState("");
  const [pin, setPin] = useState("");
  const [errore, setErrore] = useState<string | null>(null);
  const [firmando, setFirmando] = useState(false);
  const [fatta, setFatta] = useState<FirmaFatta | null>(null);

  async function scegliFile(f: File | null) {
    setFile(f);
    setFatta(null);
    setErrore(null);
    setAnteprima(null);
    if (!f) return;
    // Anteprima testuale (XML/testo): mostra l'inizio del documento.
    if (f.size <= 1_000_000) {
      try {
        const testo = await f.text();
        const stampabile = /[^\x09\x0A\x0D\x20-\x7E -￿]/.test(testo.slice(0, 500));
        setAnteprima(stampabile ? null : testo.slice(0, 4000));
      } catch {
        setAnteprima(null);
      }
    }
  }

  async function firma() {
    setErrore(null);
    setFatta(null);
    if (!file) return setErrore("Seleziona prima il file da firmare (es. l'XML della fattura).");
    if (metodo === "pfx" && !pfx) return setErrore("Carica il certificato .pfx/.p12 con cui firmare.");
    if (metodo === "cns" && !pin.trim()) return setErrore("Inserisci il PIN della smart card / CNS.");

    setFirmando(true);
    try {
      const { blob, filename } = await api.firmaCades(file, {
        formato,
        pfx: metodo === "pfx" ? pfx ?? undefined : undefined,
        password: metodo === "pfx" ? password : undefined,
        pin: metodo === "cns" ? pin : undefined,
      });
      // Mostra il risultato verificando subito la firma prodotta.
      const esito = await api.verificaFirma(blob, filename);
      setFatta({ blob, filename, esito });
    } catch (e) {
      setErrore((e as Error).message);
    } finally {
      setFirmando(false);
    }
  }

  function scarica() {
    if (!fatta) return;
    const url = URL.createObjectURL(fatta.blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fatta.filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Firma digitale</h1>
            <p>Firma fatture e documenti in CAdES (.p7m) o XAdES (XML), con certificato o smart card/CNS</p>
          </div>
        </div>

        {errore && <div className="status-pill red" style={{ display: "block", marginBottom: 12 }}>⚠️ {errore}</div>}

        {/* 1 · Documento + anteprima */}
        <section className="glass-card page-card" style={{ marginBottom: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>1 · Documento da firmare</h2>
              <p>Tipicamente l'XML FatturaPA, ma va bene qualsiasi file</p>
            </div>
          </div>
          <Field label="File">
            <input type="file" onChange={(e) => void scegliFile(e.target.files?.[0] ?? null)} />
          </Field>
          {file && (
            <p style={{ fontSize: 13, color: "var(--muted, #64748b)", marginTop: 8 }}>
              Selezionato: <strong>{file.name}</strong> · {(file.size / 1024).toFixed(1)} KB
            </p>
          )}
          {anteprima !== null && (
            <div style={{ marginTop: 10 }}>
              <span style={{ fontSize: 12, color: "var(--muted, #64748b)" }}>Anteprima del contenuto</span>
              <pre style={{
                marginTop: 4, maxHeight: 220, overflow: "auto", background: "rgba(92,71,42,0.06)",
                borderRadius: 10, padding: 12, fontSize: 12, lineHeight: 1.5, whiteSpace: "pre-wrap",
                wordBreak: "break-word",
              }}>{anteprima}</pre>
            </div>
          )}
        </section>

        {/* 2 · Formato + metodo */}
        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>2 · Tipo di firma</h2>
              <p>Scegli il formato e il dispositivo di firma</p>
            </div>
          </div>

          <span style={{ fontSize: 12, color: "var(--muted, #64748b)" }}>Formato</span>
          <div className="tabs-line" style={{ margin: "4px 0 14px" }}>
            <button type="button" className={formato === "cades" ? "active" : ""} onClick={() => setFormato("cades")}>
              CAdES · .p7m
            </button>
            <button type="button" className={formato === "xades" ? "active" : ""} onClick={() => setFormato("xades")}>
              XAdES · XML
            </button>
          </div>

          <span style={{ fontSize: 12, color: "var(--muted, #64748b)" }}>Dispositivo</span>
          <div className="tabs-line" style={{ margin: "4px 0 14px" }}>
            <button type="button" className={metodo === "pfx" ? "active" : ""} onClick={() => setMetodo("pfx")}>
              📄 Certificato (.pfx/.p12)
            </button>
            <button type="button" className={metodo === "cns" ? "active" : ""} onClick={() => setMetodo("cns")}>
              💳 Smart card / CNS
            </button>
          </div>

          {metodo === "pfx" ? (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(200px,1fr))", gap: 12 }}>
              <Field label="Certificato .pfx / .p12">
                <input type="file" accept=".pfx,.p12" onChange={(e) => setPfx(e.target.files?.[0] ?? null)} />
              </Field>
              <Field label="Password del certificato">
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" />
              </Field>
            </div>
          ) : (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(200px,1fr))", gap: 12 }}>
              <Field label="PIN di firma della carta">
                <input type="password" value={pin} onChange={(e) => setPin(e.target.value)} placeholder="PIN CNS" />
              </Field>
            </div>
          )}

          <div className="actions-row" style={{ marginTop: 16 }}>
            <button className="btn primary" type="button" disabled={firmando || !file} onClick={() => void firma()}>
              {firmando ? "Firma in corso…" : "🔏 Firma documento"}
            </button>
          </div>
        </section>

        {/* 3 · Risultato firmato */}
        {fatta && (
          <section className="glass-card page-card" style={{ marginTop: 16, borderColor: "rgba(16,185,129,0.4)" }}>
            <div className="card-title-row">
              <div>
                <h2>3 · Documento firmato ✓</h2>
                <p>Firma prodotta e verificata</p>
              </div>
              <span className={`status-pill ${fatta.esito.valido ? "green" : "red"}`}>
                {fatta.esito.valido ? "Firma valida" : "Firma non valida"}
              </span>
            </div>
            <div className="ledger-grid">
              <Dato label="File firmato" valore={fatta.filename} />
              <Dato label="Formato" valore={fatta.esito.formato} />
              <Dato label="Firmatario" valore={fatta.esito.firmatario ?? "—"} />
              <Dato label="Data e ora" valore={formattaData(fatta.esito.dataFirmaUtc)} />
              <Dato label="Algoritmo" valore={fatta.esito.algoritmoDigest ?? "—"} />
            </div>
            <div className="actions-row" style={{ marginTop: 14 }}>
              <button className="btn primary" type="button" onClick={scarica}>⬇️ Scarica {fatta.filename}</button>
            </div>
          </section>
        )}
      </div>

      <aside className="glass-card module-rail">
        <h2>Come funziona</h2>
        <p style={{ fontSize: 13, lineHeight: 1.6, color: "var(--muted, #64748b)" }}>
          Scegli il documento, controlla l'<strong>anteprima</strong>, firma e ottieni il file
          firmato già <strong>verificato</strong> (chi ha firmato, quando, validità), pronto da scaricare.
        </p>
        <div className="rail-metric"><span>Formati</span><strong>CAdES · XAdES</strong></div>
        <div className="rail-metric"><span>Digest</span><strong>SHA-256</strong></div>
        <h2 style={{ marginTop: 16 }}>Quale formato?</h2>
        <p style={{ fontSize: 12, lineHeight: 1.6, color: "var(--muted, #94a3b8)" }}>
          <strong>CAdES</strong> (<code>.p7m</code>) e <strong>XAdES</strong> (XML firmato) sono entrambi
          accettati dallo SdI per la fattura elettronica. CAdES è il più diffuso.
        </p>
        <h2 style={{ marginTop: 16 }}>Smart card / CNS</h2>
        <p style={{ fontSize: 12, lineHeight: 1.6, color: "var(--muted, #94a3b8)" }}>
          Richiede il middleware PKCS#11 della carta + lettore sul computer che firma, e la sezione
          <code>Firma:Cns</code> configurata nel backend.
        </p>
      </aside>
    </div>
  );
}

function formattaData(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString("it-IT");
}

function Dato({ label, valore }: { label: string; valore: string }) {
  return (
    <article className="ledger-card">
      <span>{label}</span>
      <strong style={{ wordBreak: "break-word" }}>{valore}</strong>
    </article>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: "grid", gap: 4, fontSize: 13 }}>
      <span style={{ color: "var(--muted, #64748b)" }}>{label}</span>
      {children}
    </label>
  );
}
