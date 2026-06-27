import { useEffect, useMemo, useState, type ReactNode } from "react";
import { api, type AvvisoPagamento, type Azienda } from "../api";

const euro = new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" });

const statoMeta: Record<string, { label: string; tone: string }> = {
  DaPagare: { label: "Da pagare", tone: "amber" },
  Sollecitato: { label: "Sollecitato", tone: "red" },
  Pagato: { label: "Pagato", tone: "green" },
  Annullato: { label: "Annullato", tone: "slate" },
};

function inputDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function meseCorrente(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}`;
}

function scadenzaDefault(): string {
  const now = new Date();
  return inputDate(new Date(now.getFullYear(), now.getMonth() + 1, 10));
}

function oggi(): string {
  return inputDate(new Date());
}

function periodoDaInput(value: string): { anno: number; mese: number } {
  const [anno, mese] = value.split("-").map(Number);
  return { anno, mese };
}

function dataIt(value?: string | null): string {
  if (!value) return "—";
  return new Date(`${value}T00:00:00`).toLocaleDateString("it-IT");
}

function scadenzaLabel(a: AvvisoPagamento): string {
  if (a.stato === "Pagato") return `Pagato il ${dataIt(a.pagatoIl)}`;
  if (a.stato === "Annullato") return "Chiuso";
  if (a.giorniAllaScadenza < 0) return `${Math.abs(a.giorniAllaScadenza)} gg in ritardo`;
  if (a.giorniAllaScadenza === 0) return "Scade oggi";
  return `tra ${a.giorniAllaScadenza} gg`;
}

export function Pagamenti() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [avvisi, setAvvisi] = useState<AvvisoPagamento[]>([]);
  const [promemoria, setPromemoria] = useState<AvvisoPagamento[]>([]);
  const [errore, setErrore] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [documento, setDocumento] = useState<{ titolo: string; testo: string } | null>(null);
  const [caricando, setCaricando] = useState(false);
  const [filtro, setFiltro] = useState<"tutti" | "scaduti" | "daPagare" | "pagati">("tutti");

  const [aziendaTarget, setAziendaTarget] = useState("tutte");
  const [periodo, setPeriodo] = useState(meseCorrente());
  const [scadenza, setScadenza] = useState(scadenzaDefault());
  const [importo, setImporto] = useState(180);
  const [descrizione, setDescrizione] = useState("Compenso commercialista mensile");
  const [iban, setIban] = useState("");
  const [note, setNote] = useState("");

  useEffect(() => {
    void carica();
  }, []);

  async function carica() {
    setCaricando(true);
    setErrore(null);
    try {
      const [listaAziende, listaAvvisi, listaPromemoria] = await Promise.all([
        api.aziende(),
        api.avvisiPagamento(),
        api.promemoriaPagamento(7),
      ]);
      setAziende(listaAziende);
      setAvvisi(listaAvvisi);
      setPromemoria(listaPromemoria);
    } catch (e) {
      setErrore((e as Error).message);
    } finally {
      setCaricando(false);
    }
  }

  const statistiche = useMemo(() => {
    const aperti = avvisi.filter((a) => a.stato !== "Pagato" && a.stato !== "Annullato");
    return {
      daIncassare: aperti.reduce((sum, a) => sum + Number(a.importo), 0),
      scaduti: aperti.filter((a) => a.scaduto).length,
      promemoria: promemoria.length,
      pagati: avvisi.filter((a) => a.stato === "Pagato").length,
    };
  }, [avvisi, promemoria]);

  const avvisiFiltrati = useMemo(() => {
    if (filtro === "scaduti") return avvisi.filter((a) => a.scaduto);
    if (filtro === "daPagare") return avvisi.filter((a) => a.stato === "DaPagare" || a.stato === "Sollecitato");
    if (filtro === "pagati") return avvisi.filter((a) => a.stato === "Pagato");
    return avvisi;
  }, [avvisi, filtro]);

  async function creaAvviso() {
    setErrore(null);
    setInfo(null);
    setDocumento(null);
    if (aziende.length === 0) return setErrore("Crea almeno un'azienda cliente prima di emettere avvisi.");

    const { anno, mese } = periodoDaInput(periodo);
    const payload = {
      anno,
      mese,
      importo: Number(importo),
      dataScadenza: scadenza,
      descrizione: descrizione.trim() || undefined,
      metodoPagamento: "Bonifico",
      iban: iban.trim() || undefined,
      note: note.trim() || undefined,
    };

    try {
      if (aziendaTarget === "tutte") {
        const result = await api.creaAvvisiPagamentoMese(payload);
        setInfo(`Creati ${result.creati} avvisi mensili. I clienti gia presenti per il mese sono stati saltati.`);
      } else {
        await api.creaAvvisoPagamento(aziendaTarget, payload);
        setInfo("Avviso di pagamento creato.");
      }
      await carica();
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function preparaAvviso(avviso: AvvisoPagamento) {
    setErrore(null);
    setInfo(null);
    try {
      const result = await api.testoAvvisoPagamento(avviso.id);
      setDocumento({ titolo: "Avviso di pagamento", testo: result.testo });
      setInfo(`Avviso pronto per ${avviso.cliente ?? "cliente"}.`);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function registraPromemoria(avviso: AvvisoPagamento) {
    setErrore(null);
    setInfo(null);
    try {
      const result = await api.registraPromemoriaPagamento(avviso.id);
      setDocumento({ titolo: "Testo promemoria", testo: result.testo });
      setInfo(`Promemoria registrato per ${avviso.cliente ?? "cliente"}.`);
      await carica();
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function cambiaStato(avviso: AvvisoPagamento, stato: "Pagato" | "Annullato" | "DaPagare") {
    setErrore(null);
    setInfo(null);
    try {
      await api.cambiaStatoAvvisoPagamento(avviso.id, {
        stato,
        pagatoIl: stato === "Pagato" ? oggi() : undefined,
        riferimentoPagamento: stato === "Pagato" ? "Registrato manualmente" : undefined,
      });
      setInfo(stato === "Pagato" ? "Pagamento registrato." : "Stato aggiornato.");
      await carica();
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function copiaDocumento() {
    if (!documento?.testo) return;
    await navigator.clipboard.writeText(documento.testo);
    setInfo("Testo copiato.");
  }

  return (
    <div className="module-layout">
      <div>
        <div className="page-heading">
          <div>
            <h1>Pagamenti clienti</h1>
            <p>Avvisi mensili, debiti aperti e promemoria per il commercialista</p>
          </div>
          <button className="btn secondary" type="button" onClick={() => void carica()}>
            Aggiorna
          </button>
        </div>

        {errore && <div className="status-pill red" style={{ display: "block", marginBottom: 12 }}>! {errore}</div>}
        {info && <div className="status-pill green" style={{ display: "block", marginBottom: 12 }}>{info}</div>}

        <section className="glass-card page-card" style={{ marginBottom: 16 }}>
          <div className="card-title-row">
            <div>
              <h2>Nuovo avviso mensile</h2>
              <p>Genera il debito del mese per un cliente o per tutte le aziende clienti</p>
            </div>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(170px,1fr))", gap: 12 }}>
            <Field label="Cliente">
              <select value={aziendaTarget} onChange={(e) => setAziendaTarget(e.target.value)}>
                <option value="tutte">Tutte le aziende clienti</option>
                {aziende.map((a) => (
                  <option key={a.id} value={a.id}>{a.ragioneSociale}</option>
                ))}
              </select>
            </Field>
            <Field label="Mese">
              <input type="month" value={periodo} onChange={(e) => setPeriodo(e.target.value)} />
            </Field>
            <Field label="Scadenza">
              <input type="date" value={scadenza} onChange={(e) => setScadenza(e.target.value)} />
            </Field>
            <Field label="Importo">
              <input type="number" min="0" step="0.01" value={importo} onChange={(e) => setImporto(Number(e.target.value))} />
            </Field>
            <Field label="Descrizione">
              <input value={descrizione} onChange={(e) => setDescrizione(e.target.value)} />
            </Field>
            <Field label="IBAN studio">
              <input value={iban} onChange={(e) => setIban(e.target.value)} placeholder="IT00..." />
            </Field>
          </div>
          <Field label="Note interne">
            <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Es. inviare dopo consegna documenti" />
          </Field>
          <div className="actions-row" style={{ marginTop: 12 }}>
            <button className="btn primary" type="button" onClick={() => void creaAvviso()}>
              Crea avviso
            </button>
          </div>
        </section>

        <div className="metric-strip">
          <Metric label="Da incassare" value={euro.format(statistiche.daIncassare)} />
          <Metric label="Scaduti" value={String(statistiche.scaduti)} />
          <Metric label="Promemoria 7 gg" value={String(statistiche.promemoria)} />
          <Metric label="Pagati" value={String(statistiche.pagati)} />
        </div>

        <section className="glass-card page-card">
          <div className="card-title-row">
            <div>
              <h2>Debiti clienti</h2>
              <p>{caricando ? "Caricamento..." : `${avvisiFiltrati.length} avvisi visualizzati`}</p>
            </div>
          </div>
          <div className="tabs-line">
            <button className={filtro === "tutti" ? "active" : ""} type="button" onClick={() => setFiltro("tutti")}>Tutti</button>
            <button className={filtro === "daPagare" ? "active" : ""} type="button" onClick={() => setFiltro("daPagare")}>Da pagare</button>
            <button className={filtro === "scaduti" ? "active" : ""} type="button" onClick={() => setFiltro("scaduti")}>Scaduti</button>
            <button className={filtro === "pagati" ? "active" : ""} type="button" onClick={() => setFiltro("pagati")}>Pagati</button>
          </div>
          <div className="table-wrap">
            <table className="data-table spacious">
              <thead>
                <tr>
                  <th>Cliente</th>
                  <th>Mese</th>
                  <th>Scadenza</th>
                  <th className="right">Importo</th>
                  <th>Stato</th>
                  <th className="right">Azioni</th>
                </tr>
              </thead>
              <tbody>
                {avvisiFiltrati.length === 0 && (
                  <tr><td colSpan={6}>Nessun avviso. Crea il primo avviso mensile qui sopra.</td></tr>
                )}
                {avvisiFiltrati.map((a) => {
                  const meta = statoMeta[a.stato] ?? { label: a.stato, tone: "slate" };
                  const chiuso = a.stato === "Pagato" || a.stato === "Annullato";
                  return (
                    <tr key={a.id}>
                      <td>
                        <strong>{a.cliente ?? "Cliente"}</strong>
                        <small style={{ display: "block", color: "var(--soft)" }}>{a.descrizione}</small>
                      </td>
                      <td className="num">{String(a.mese).padStart(2, "0")}/{a.anno}</td>
                      <td>
                        <strong>{dataIt(a.dataScadenza)}</strong>
                        <small style={{ display: "block", color: a.scaduto ? "var(--red)" : "var(--soft)" }}>{scadenzaLabel(a)}</small>
                      </td>
                      <td className="right num"><strong>{euro.format(a.importo)}</strong></td>
                      <td>
                        <span className={`status-pill ${meta.tone}`}>{meta.label}</span>
                        {a.numeroPromemoria > 0 && (
                          <small style={{ display: "block", color: "var(--soft)", marginTop: 4 }}>
                            {a.numeroPromemoria} promemoria
                          </small>
                        )}
                      </td>
                      <td className="right">
                        <div className="actions-row" style={{ justifyContent: "flex-end", gap: 6, flexWrap: "wrap" }}>
                          <button className="mini-action" type="button" onClick={() => void preparaAvviso(a)}>Avviso</button>
                          {!chiuso && (
                            <>
                              <button className="mini-action" type="button" onClick={() => void registraPromemoria(a)}>Promemoria</button>
                              <button className="mini-action" type="button" onClick={() => void cambiaStato(a, "Pagato")}>Pagato</button>
                              <button className="mini-action" type="button" onClick={() => void cambiaStato(a, "Annullato")}>Annulla</button>
                            </>
                          )}
                          {a.stato === "Annullato" && (
                            <button className="mini-action" type="button" onClick={() => void cambiaStato(a, "DaPagare")}>Riapri</button>
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
        <h2>Procedura pagamento</h2>
        <PaymentStep n="1" title="Crea avviso" text="Genera importo, scadenza e causale per il mese." />
        <PaymentStep n="2" title="Invia avviso" text="Usa il testo formale e conserva il debito aperto." />
        <PaymentStep n="3" title="Verifica incasso" text="Controlla bonifico o ricevuta comunicata dal cliente." />
        <PaymentStep n="4" title="Sollecita o chiudi" text="Invia promemoria se scade, oppure segna pagato." />

        {documento && (
          <div style={{ marginTop: 16 }}>
            <div className="card-title-row" style={{ marginBottom: 8 }}>
              <h2>{documento.titolo}</h2>
              <button className="link-button" type="button" onClick={() => void copiaDocumento()}>Copia</button>
            </div>
            <textarea readOnly value={documento.testo} rows={11} />
          </div>
        )}
      </aside>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: "grid", gap: 4, marginTop: 10, fontSize: 13 }}>
      <span style={{ color: "var(--muted)", fontWeight: 700 }}>{label}</span>
      {children}
    </label>
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

function PaymentStep({ n, title, text }: { n: string; title: string; text: string }) {
  return (
    <div className="rail-metric" style={{ alignItems: "flex-start", gap: 10 }}>
      <span className="tag blue">{n}</span>
      <strong style={{ display: "grid", gap: 3 }}>
        {title}
        <small style={{ color: "var(--muted)", fontWeight: 600, lineHeight: 1.35 }}>{text}</small>
      </strong>
    </div>
  );
}
