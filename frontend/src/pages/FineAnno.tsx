import { useEffect, useState } from "react";
import {
  api,
  scaricaBlob,
  type Azienda,
  type DipendenteRapporto,
  type ConguaglioAnnuale,
  type TfrRivalutazione,
  type InailAutoliquidazione,
  type PraticaInail,
} from "../api";

const eur = (n: number) =>
  n.toLocaleString("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";

/**
 * Operazioni di fine anno: conguaglio fiscale IRPEF, rivalutazione del fondo TFR e
 * autoliquidazione INAIL. Il conguaglio va in busta da solo archiviando dicembre;
 * qui lo si consulta e si preparano TFR e INAIL.
 */
export function FineAnno() {
  const [aziende, setAziende] = useState<Azienda[]>([]);
  const [aziendaId, setAziendaId] = useState("");
  const [dipendenti, setDipendenti] = useState<DipendenteRapporto[]>([]);
  const [rapportoId, setRapportoId] = useState("");
  const [anno, setAnno] = useState(new Date().getFullYear());
  const [errore, setErrore] = useState<string | null>(null);

  const [conguaglio, setConguaglio] = useState<ConguaglioAnnuale | null>(null);
  const [istatPct, setIstatPct] = useState("");
  const [tfr, setTfr] = useState<TfrRivalutazione | null>(null);
  const [pat, setPat] = useState("");
  const [voceTariffa, setVoceTariffa] = useState("");
  const [tasso, setTasso] = useState("");
  const [rataVersata, setRataVersata] = useState("");
  const [inail, setInail] = useState<InailAutoliquidazione | null>(null);
  const [praticaInail, setPraticaInail] = useState<PraticaInail | null>(null);
  const [salvandoPratica, setSalvandoPratica] = useState(false);

  useEffect(() => {
    api.aziende()
      .then((a) => {
        setAziende(a);
        if (a.length > 0) setAziendaId(a[0].id);
      })
      .catch((e) => setErrore((e as Error).message));
  }, []);

  useEffect(() => {
    if (!aziendaId) return;
    setConguaglio(null);
    setTfr(null);
    setInail(null);
    api.dipendenti(aziendaId)
      .then((d) => {
        setDipendenti(d);
        setRapportoId(d.length > 0 ? d[0].rapportoId : "");
      })
      .catch((e) => setErrore((e as Error).message));
    api.inailPosizione(aziendaId, anno).then((p) => {
      setPat(p?.pat ?? "");
      setVoceTariffa(p?.voceTariffa ?? "");
      setTasso(p ? String(p.tassoPerMille) : "");
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aziendaId, anno]);

  async function calcolaConguaglio() {
    if (!rapportoId) return setErrore("Seleziona un dipendente.");
    setErrore(null);
    setConguaglio(null);
    try {
      setConguaglio(await api.conguaglioAnnuale(rapportoId, anno));
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function calcolaTfr() {
    if (!rapportoId) return setErrore("Seleziona un dipendente.");
    setErrore(null);
    setTfr(null);
    try {
      // Input in percento (es. 1,2) → decimale (0,012). Vuoto = usa il parametro dell'anno.
      const pct = istatPct.trim().replace(",", ".");
      const variazione = pct === "" ? undefined : Number(pct) / 100;
      if (variazione !== undefined && Number.isNaN(variazione))
        return setErrore("Variazione ISTAT non valida: usa un numero, es. 1,2");
      setTfr(await api.tfrRivalutazione(rapportoId, anno, variazione));
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function salvaPosizione() {
    setErrore(null);
    try {
      const t = Number(tasso.trim().replace(",", "."));
      if (Number.isNaN(t) || t <= 0)
        return setErrore("Tasso INAIL non valido: usa il valore per mille della lettera basi di calcolo, es. 12,5");
      await api.salvaPosizioneInail(aziendaId, { anno, pat, voceTariffa, tassoPerMille: t });
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function calcolaInail() {
    setErrore(null);
    setInail(null);
    setPraticaInail(null);
    try {
      await salvaPosizione();
      const rata = Number(rataVersata.trim().replace(",", ".") || "0");
      setInail(await api.inailAutoliquidazione(aziendaId, anno, Number.isNaN(rata) ? 0 : rata));
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  async function salvaPraticaInail() {
    setErrore(null);
    setSalvandoPratica(true);
    try {
      const rata = Number(rataVersata.trim().replace(",", ".") || "0");
      setPraticaInail(await api.salvaAutoliquidazioneInail(aziendaId, anno, Number.isNaN(rata) ? 0 : rata));
    } catch (e) {
      setErrore((e as Error).message);
    } finally {
      setSalvandoPratica(false);
    }
  }

  async function scaricaBozzaInail() {
    if (!praticaInail) return;
    setErrore(null);
    try {
      const { blob, filename } = await api.scaricaBozzaInail(praticaInail.id);
      scaricaBlob(blob, filename);
    } catch (e) {
      setErrore((e as Error).message);
    }
  }

  return (
    <div>
      <div className="page-heading">
        <div>
          <h1>Fine anno</h1>
          <p>Conguaglio IRPEF, rivalutazione TFR e autoliquidazione INAIL sui cedolini archiviati</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <label className="soft-button" style={{ gap: 8 }}>
            <span>🏢</span>
            <select
              value={aziendaId}
              onChange={(e) => setAziendaId(e.target.value)}
              style={{ border: "none", background: "transparent", font: "inherit" }}
            >
              {aziende.length === 0 && <option value="">Nessuna azienda</option>}
              {aziende.map((a) => (
                <option key={a.id} value={a.id}>{a.ragioneSociale}</option>
              ))}
            </select>
          </label>
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 13 }}>
            Anno
            <input
              type="number"
              value={anno}
              onChange={(e) => setAnno(Number(e.target.value))}
              style={{ width: 90 }}
            />
          </label>
        </div>
      </div>

      {errore && <div className="status-pill red" style={{ display: "block", marginBottom: 12 }}>⚠️ {errore}</div>}

      <section className="glass-card page-card" style={{ marginBottom: 16 }}>
        <div className="card-title-row">
          <div>
            <h2>Conguaglio fiscale IRPEF</h2>
            <p>IRPEF dovuta sull'anno reale vs trattenuta mese per mese — archiviando dicembre va in busta da solo</p>
          </div>
        </div>
        <div className="actions-row" style={{ marginTop: 12, flexWrap: "wrap", gap: 8, alignItems: "center" }}>
          <select value={rapportoId} onChange={(e) => setRapportoId(e.target.value)}>
            {dipendenti.length === 0 && <option value="">Nessun dipendente</option>}
            {dipendenti.map((d) => (
              <option key={d.rapportoId} value={d.rapportoId}>{d.nominativo}</option>
            ))}
          </select>
          <button className="btn primary" type="button" onClick={() => void calcolaConguaglio()}>
            Calcola conguaglio {anno}
          </button>
        </div>
        {conguaglio && (
          <div style={{ marginTop: 12 }}>
            <span className={`status-pill ${conguaglio.conguaglio === 0 ? "green" : conguaglio.aDebito ? "amber" : "blue"}`}>
              {conguaglio.conguaglio === 0
                ? "Nessun conguaglio: anno già allineato"
                : conguaglio.aDebito
                  ? `A debito: ${eur(conguaglio.conguaglio)} da trattenere`
                  : `A credito: ${eur(-conguaglio.conguaglio)} da rimborsare`}
            </span>
            <table className="data-table" style={{ marginTop: 10 }}>
              <tbody>
                <tr><td>IRPEF lorda annua</td><td className="right num">{eur(conguaglio.irpefLordaAnnua)}</td></tr>
                <tr><td>Detrazioni spettanti</td><td className="right num">{eur(conguaglio.detrazioniSpettanti)}</td></tr>
                <tr><td>IRPEF netta dovuta</td><td className="right num">{eur(conguaglio.irpefNettaAnnua)}</td></tr>
                <tr><td>IRPEF già trattenuta</td><td className="right num">{eur(conguaglio.irpefGiaTrattenuta)}</td></tr>
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="glass-card page-card" style={{ marginBottom: 16 }}>
        <div className="card-title-row">
          <div>
            <h2>Rivalutazione TFR</h2>
            <p>Fondo al 31/12 precedente rivalutato: 1,5% + 75% della variazione ISTAT, imposta sostitutiva 17%</p>
          </div>
        </div>
        <div className="actions-row" style={{ marginTop: 12, flexWrap: "wrap", gap: 8, alignItems: "center" }}>
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 13 }}>
            Variazione ISTAT %
            <input
              placeholder="es. 1,2"
              value={istatPct}
              onChange={(e) => setIstatPct(e.target.value)}
              style={{ width: 90 }}
            />
          </label>
          <button className="btn primary" type="button" onClick={() => void calcolaTfr()}>
            Calcola rivalutazione {anno}
          </button>
        </div>
        {tfr && (
          <table className="data-table" style={{ marginTop: 12 }}>
            <tbody>
              <tr><td>Fondo TFR al 31/12/{anno - 1}</td><td className="right num">{eur(tfr.fondoAl31DicembrePrecedente)}</td></tr>
              <tr><td>Coefficiente ({(tfr.variazioneIstat * 100).toLocaleString("it-IT")}% ISTAT)</td>
                <td className="right num">{(tfr.coefficiente * 100).toLocaleString("it-IT", { maximumFractionDigits: 4 })}%</td></tr>
              <tr><td>Rivalutazione lorda</td><td className="right num">{eur(tfr.rivalutazioneLorda)}</td></tr>
              <tr><td>Imposta sostitutiva 17%</td><td className="right num">{eur(tfr.impostaSostitutiva)}</td></tr>
              <tr><td>Rivalutazione netta</td><td className="right num">{eur(tfr.rivalutazioneNetta)}</td></tr>
              <tr><td>Quote maturate nel {anno}</td><td className="right num">{eur(tfr.quoteMaturateAnno)}</td></tr>
              <tr><td><strong>Fondo TFR a fine {anno}</strong></td>
                <td className="right num"><strong>{eur(tfr.fondoFinale)}</strong></td></tr>
            </tbody>
          </table>
        )}
      </section>

      <section className="glass-card page-card">
        <div className="card-title-row">
          <div>
            <h2>Autoliquidazione INAIL</h2>
            <p>PAT e tasso dalla lettera "basi di calcolo" INAIL; le retribuzioni arrivano dai cedolini archiviati</p>
          </div>
        </div>
        <div className="actions-row" style={{ marginTop: 12, flexWrap: "wrap", gap: 8, alignItems: "center" }}>
          <input placeholder="PAT (es. 12345678/90)" value={pat} onChange={(e) => setPat(e.target.value)} style={{ width: 160 }} />
          <input placeholder="Voce tariffa" value={voceTariffa} onChange={(e) => setVoceTariffa(e.target.value)} style={{ width: 110 }} />
          <input placeholder="Tasso ‰ (es. 12,5)" value={tasso} onChange={(e) => setTasso(e.target.value)} style={{ width: 120 }} />
          <input placeholder="Rata già versata €" value={rataVersata} onChange={(e) => setRataVersata(e.target.value)} style={{ width: 140 }} />
          <button className="btn primary" type="button" onClick={() => void calcolaInail()}>
            Calcola autoliquidazione {anno}
          </button>
        </div>
        {inail && (
          <div style={{ marginTop: 12 }}>
            <table className="data-table">
              <tbody>
                <tr><td>Retribuzioni {anno} ({inail.numeroCedolini} cedolini)</td>
                  <td className="right num">{eur(inail.retribuzioniAnno)}</td></tr>
                <tr><td>Regolazione ({inail.tassoPerMille.toLocaleString("it-IT")}‰)</td>
                  <td className="right num">{eur(inail.regolazione)}</td></tr>
                <tr><td>Rata anticipata {anno + 1}</td><td className="right num">{eur(inail.rataAnticipata)}</td></tr>
                <tr><td>Rata già versata</td><td className="right num">−{eur(inail.rataGiaVersata)}</td></tr>
                <tr><td><strong>Totale da versare</strong></td>
                  <td className="right num"><strong>{eur(inail.totaleDaVersare)}</strong></td></tr>
              </tbody>
            </table>
            <p style={{ fontSize: 12, color: "var(--muted, #94a3b8)", marginTop: 8 }}>{inail.nota}</p>
            <div className="actions-row" style={{ marginTop: 8, alignItems: "center" }}>
              <button className="btn secondary" type="button" onClick={() => void salvaPraticaInail()} disabled={salvandoPratica}>
                {salvandoPratica ? "Salvo…" : "Salva pratica"}
              </button>
              {praticaInail && (
                <>
                  <span className="status-pill blue">{praticaInail.stato}</span>
                  <button className="btn secondary" type="button" onClick={() => void scaricaBozzaInail()}>
                    Scarica bozza
                  </button>
                </>
              )}
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
