import { useEffect, useState, type ReactNode } from "react";
import { api, type Azienda, type Fattura, type Trasmissione } from "../api";

// Mappa lo stato di trasmissione SdI a un "tone" del design system (status-pill).
const statoTone: Record<string, string> = {
  PreparataInvioManuale: "amber",
  Inviata: "blue",
  ConsegnataSdi: "green",
  MancataConsegna: "amber",
  ScartataSdi: "red",
  AccettataPa: "green",
  RifiutataPa: "red",
  DecorrenzaTermini: "slate",
  Errore: "red",
};

const statoLabel: Record<string, string> = {
  PreparataInvioManuale: "Pronta (invio manuale)",
  Inviata: "Inviata allo SdI",
  ConsegnataSdi: "Consegnata (RC)",
  MancataConsegna: "Mancata consegna (MC)",
  ScartataSdi: "Scartata (NS)",
  AccettataPa: "Accettata (NE)",
  RifiutataPa: "Rifiutata (NE)",
  DecorrenzaTermini: "Decorrenza termini",
  Errore: "Errore",
};

const oggi = () => new Date().toISOString().slice(0, 10);

export function Fatture() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [aziendaId, setAziendaId] = useState<string>("");
  const [fatture, setFatture] = useState<Fattura[]>([]);
  const [trasm, setTrasm] = useState<Record<string, Trasmissione | null>>({});
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [caricando, setCaricando] = useState(false);

  // Form nuova fattura.
  const [cliente, setCliente] = useState("");
  const [pivaCliente, setPivaCliente] = useState("");
  const [descrizione, setDescrizione] = useState("Consulenza");
  const [importo, setImporto] = useState(1000);
  const [aliquota, setAliquota] = useState(22);

  useEffect(() => {
    api.aziende()
      .then((a) => {
        setAziende(a);
        if (a.length > 0) setAziendaId(a[0].id);
      })
      .catch((e) => setErrore(e.message));
  }, []);

  useEffect(() => {
    if (aziendaId) void caricaFatture(aziendaId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aziendaId]);

  async function caricaFatture(id: string) {
    setCaricando(true);
    setErrore(null);
    try {
      const lista = await api.fatture(id);
      setFatture(lista);
      const stati = await Promise.all(lista.map((f) => api.trasmissione(f.id)));
      const map: Record<string, Trasmissione | null> = {};
      lista.forEach((f, i) => (map[f.id] = stati[i]));
      setTrasm(map);
    } catch (e) {
      setErrore((e as Error).message);
    } finally {
      setCaricando(false);
    }
  }

  async function creaFattura() {
    setErrore(null);
    setInfo(null);
    if (!aziendaId) return setErrore("Seleziona prima un'azienda emittente.");
    try {
      await api.creaFattura(aziendaId, {
        data: oggi(),
        cessionario: {
          denominazione: cliente.trim() || "Cliente demo",
          partitaIva: pivaCliente.trim() || undefined,
          indirizzo: "Via Esempio 1",
          cap: "00100",
          comune: "Roma",
          provincia: "RM",
        },
        righe: [{ descrizione, quantita: 1, prezzoUnitario: Number(importo), aliquotaIva: Number(aliquota) }],
      });
      setInfo("Fattura creata.");
      setCliente("");
      setPivaCliente("");
      await caricaFatture(aziendaId);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function trasmetti(fatturaId: string) {
    setErrore(null);
    setInfo(null);
    try {
      const t = await api.trasmetti(fatturaId);
      setTrasm((m) => ({ ...m, [fatturaId]: t }));
      setInfo(`Trasmissione avviata: ${statoLabel[t.stato] ?? t.stato} · file ${t.nomeFileXml ?? ""}`);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function ricevuta(fatturaId: string, trasmissioneId: string, tipo: string, codiceEsito?: string) {
    setErrore(null);
    try {
      const r = await api.registraRicevuta(trasmissioneId, tipo, codiceEsito);
      setTrasm((m) => ({ ...m, [fatturaId]: { ...(m[fatturaId] as Trasmissione), stato: r.stato } }));
      setInfo(`Ricevuta ${tipo} registrata → ${statoLabel[r.stato] ?? r.stato}`);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function aggiornaEsito(fatturaId: string, trasmissioneId: string) {
    setErrore(null);
    try {
      const r = await api.aggiornaEsito(trasmissioneId);
      setTrasm((m) => ({ ...m, [fatturaId]: { ...(m[fatturaId] as Trasmissione), stato: r.stato } }));
      setInfo(`Esito aggiornato → ${statoLabel[r.stato] ?? r.stato}`);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function scaricaXml(fatturaId: string, numero: string) {
    try {
      const xml = await api.fatturaXml(fatturaId);
      const url = URL.createObjectURL(new Blob([xml], { type: "application/xml" }));
      const a = document.createElement("a");
      a.href = url;
      a.download = `fattura-${numero.replace("/", "-")}.xml`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Fatturazione elettronica</h1>
            <p>Emissione, generazione XML FatturaPA e trasmissione al Sistema di Interscambio (SdI)</p>
          </div>
          <label className="soft-button" style={{ gap: 8 }}>
            <span>🏢</span>
            <select
              value={aziendaId}
              onChange={(e) => setAziendaId(e.target.value)}
              style={{ border: "none", background: "transparent", font: "inherit" }}
            >
              {aziende.length === 0 && <option value="">Nessuna azienda — creane una in “Aziende”</option>}
              {aziende.map((a) => (
                <option key={a.id} value={a.id}>{a.ragioneSociale}</option>
              ))}
            </select>
          </label>
        </div>

        {errore && <div className="status-pill red" style={{ display: "block", marginBottom: 12 }}>⚠️ {errore}</div>}
        {info && <div className="status-pill green" style={{ display: "block", marginBottom: 12 }}>✓ {info}</div>}

        <section className="glass-card page-card" style={{ marginBottom: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>Nuova fattura</h2>
              <p>Compila e salva: i totali IVA sono calcolati dal backend</p>
            </div>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(160px,1fr))", gap: 12 }}>
            <Field label="Cliente">
              <input value={cliente} onChange={(e) => setCliente(e.target.value)} placeholder="Beta S.r.l." />
            </Field>
            <Field label="P.IVA cliente">
              <input value={pivaCliente} onChange={(e) => setPivaCliente(e.target.value)} placeholder="09876543210" />
            </Field>
            <Field label="Descrizione">
              <input value={descrizione} onChange={(e) => setDescrizione(e.target.value)} />
            </Field>
            <Field label="Importo (imponibile €)">
              <input type="number" value={importo} onChange={(e) => setImporto(Number(e.target.value))} />
            </Field>
            <Field label="Aliquota IVA %">
              <input type="number" value={aliquota} onChange={(e) => setAliquota(Number(e.target.value))} />
            </Field>
          </div>
          <div className="actions-row" style={{ marginTop: 12 }}>
            <button className="btn primary" type="button" onClick={() => void creaFattura()}>+ Crea fattura</button>
          </div>
        </section>

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>Fatture emesse</h2>
              <p>{caricando ? "Caricamento…" : `${fatture.length} fatture`}</p>
            </div>
          </div>
          <div className="table-wrap">
            <table className="data-table spacious">
              <thead>
                <tr>
                  <th>Numero</th>
                  <th>Cliente</th>
                  <th className="right">Totale</th>
                  <th>Stato SdI</th>
                  <th className="right">Azioni</th>
                </tr>
              </thead>
              <tbody>
                {fatture.length === 0 && (
                  <tr><td colSpan={5}>Nessuna fattura. Creane una qui sopra.</td></tr>
                )}
                {fatture.map((f) => {
                  const t = trasm[f.id];
                  return (
                    <tr key={f.id}>
                      <td><strong>{f.numero}</strong></td>
                      <td>{f.cessionarioDenominazione ?? "—"}</td>
                      <td className="right num">{f.totaleDocumento?.toLocaleString("it-IT", { minimumFractionDigits: 2 })} €</td>
                      <td>
                        {t
                          ? <span className={`status-pill ${statoTone[t.stato] ?? "slate"}`}>{statoLabel[t.stato] ?? t.stato}</span>
                          : <span className="status-pill slate">Non trasmessa</span>}
                      </td>
                      <td className="right">
                        <div className="actions-row" style={{ justifyContent: "flex-end", flexWrap: "wrap", gap: 6 }}>
                          <button className="mini-action" type="button" onClick={() => void scaricaXml(f.id, f.numero)}>XML</button>
                          {!t && (
                            <button className="mini-action" type="button" onClick={() => void trasmetti(f.id)}>Trasmetti</button>
                          )}
                          {t && t.stato === "Inviata" && (
                            <button className="mini-action" type="button" onClick={() => void aggiornaEsito(f.id, t.id)}>Aggiorna esito</button>
                          )}
                          {t && (t.stato === "Inviata" || t.stato === "PreparataInvioManuale") && (
                            <>
                              <button className="mini-action" type="button" onClick={() => void ricevuta(f.id, t.id, "RC")}>Segna consegnata</button>
                              <button className="mini-action" type="button" onClick={() => void ricevuta(f.id, t.id, "NS", "00400")}>Segna scartata</button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <aside className="glass-card module-rail">
        <h2>Come funziona</h2>
        <p style={{ fontSize: 13, lineHeight: 1.5, color: "var(--muted, #64748b)" }}>
          1. Crea la fattura (il backend calcola IVA e totali).<br />
          2. <strong>Trasmetti</strong>: genera l'XML FatturaPA, esegue i controlli SdI e invia tramite
          il canale configurato.<br />
          3. Lo stato si aggiorna con le ricevute SdI (RC/MC/NS).
        </p>
        <div className="rail-metric"><span>Canale attivo</span><strong>{Object.values(trasm).find(Boolean)?.provider ?? "Manuale"}</strong></div>
        <p style={{ fontSize: 12, color: "var(--muted, #94a3b8)", marginTop: 8 }}>
          Modalità manuale = prepara il file da firmare/caricare. Per l'invio automatico configurare
          un provider accreditato nella sezione <code>Sdi</code> del backend.
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
