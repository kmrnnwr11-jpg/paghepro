import { useState, type ReactNode } from "react";
import { api } from "../api";

type Metodo = "pfx" | "cns";

export function Firma() {
  const [metodo, setMetodo] = useState<Metodo>("pfx");
  const [file, setFile] = useState<File | null>(null);
  const [pfx, setPfx] = useState<File | null>(null);
  const [password, setPassword] = useState("");
  const [pin, setPin] = useState("");
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [firmando, setFirmando] = useState(false);

  async function firma() {
    setErrore(null);
    setInfo(null);
    if (!file) return setErrore("Seleziona prima il file da firmare (es. l'XML della fattura).");
    if (metodo === "pfx" && !pfx) return setErrore("Carica il certificato .pfx/.p12 con cui firmare.");
    if (metodo === "cns" && !pin.trim()) return setErrore("Inserisci il PIN della smart card / CNS.");

    setFirmando(true);
    try {
      const { blob, filename } = await api.firmaCades(file, {
        pfx: metodo === "pfx" ? pfx ?? undefined : undefined,
        password: metodo === "pfx" ? password : undefined,
        pin: metodo === "cns" ? pin : undefined,
      });
      // Scarica la busta firmata .p7m.
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      a.click();
      URL.revokeObjectURL(url);
      setInfo(`Firma completata: scaricato ${filename}`);
    } catch (e) {
      setErrore((e as Error).message);
    } finally {
      setFirmando(false);
    }
  }

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Firma digitale</h1>
            <p>Firma CAdES-BES (busta .p7m) di fatture e documenti, con certificato o smart card/CNS</p>
          </div>
        </div>

        {errore && <div className="status-pill red" style={{ display: "block", marginBottom: 12 }}>⚠️ {errore}</div>}
        {info && <div className="status-pill green" style={{ display: "block", marginBottom: 12 }}>✓ {info}</div>}

        <section className="glass-card page-card" style={{ marginBottom: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>1 · Documento da firmare</h2>
              <p>Tipicamente l'XML FatturaPA, ma va bene qualsiasi file</p>
            </div>
          </div>
          <Field label="File">
            <input
              type="file"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </Field>
          {file && (
            <p style={{ fontSize: 13, color: "var(--muted, #64748b)", marginTop: 8 }}>
              Selezionato: <strong>{file.name}</strong> → verrà prodotto <strong>{file.name}.p7m</strong>
            </p>
          )}
        </section>

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>2 · Come firmi</h2>
              <p>Scegli il dispositivo di firma</p>
            </div>
          </div>

          <div className="tabs-line" style={{ marginBottom: 12 }}>
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
                <input
                  type="file"
                  accept=".pfx,.p12"
                  onChange={(e) => setPfx(e.target.files?.[0] ?? null)}
                />
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
            <button className="btn primary" type="button" disabled={firmando} onClick={() => void firma()}>
              {firmando ? "Firma in corso…" : "🔏 Firma e scarica .p7m"}
            </button>
          </div>
        </section>
      </div>

      <aside className="glass-card module-rail">
        <h2>Come funziona</h2>
        <p style={{ fontSize: 13, lineHeight: 1.6, color: "var(--muted, #64748b)" }}>
          La firma produce una busta <strong>CAdES-BES</strong> (<code>.xml.p7m</code>) con digest
          SHA-256, il formato richiesto dal Sistema di Interscambio per la fattura elettronica.
        </p>
        <div className="rail-metric"><span>Formato</span><strong>CAdES (.p7m)</strong></div>
        <div className="rail-metric"><span>Digest</span><strong>SHA-256</strong></div>
        <h2 style={{ marginTop: 16 }}>Smart card / CNS</h2>
        <p style={{ fontSize: 12, lineHeight: 1.6, color: "var(--muted, #94a3b8)" }}>
          La firma con CNS richiede il <strong>middleware PKCS#11</strong> della carta, il lettore e
          la carta sulla macchina che esegue la firma, e la sezione <code>Firma:Cns</code> configurata
          nel backend. Funziona in contesto desktop/agente locale, non su un backend cloud dove la
          carta non è presente.
        </p>
      </aside>
    </div>
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
