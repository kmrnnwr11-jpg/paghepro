import { useCallback, useEffect, useRef, useState } from "react";
import { Send, ShieldCheck, Sparkles, X } from "lucide-react";
import {
  api,
  type Azienda,
  type CosaVedeAi,
  type EsitoPreflightAi,
  type EsitoPropostaAurora,
  type EsitoQueryAi,
  type GrantAiEmesso,
  type ManifestGrantAi,
  type MessaggioChatAi,
  type PropostaAzioneAurora,
  type RigaCedolinoMeseAi,
  type StatoChatAi,
} from "./api";
import { anteprimaContestoAurora, type ContestoAurora } from "./auroraContext";
import {
  descriviScadenzaProposta,
  esitoPropostaDaErrore,
  esitoRassicurante,
  propostaScaduta,
  puoRipristinare,
} from "./auroraProposta";
import {
  ambitoControlloCedolini,
  contaStatiCedolini,
  DIMENSIONE_LOTTO_CEDOLINI_AURORA,
  richiestaControlloCedolini,
  stessoAmbitoControlloCedolini,
  type AmbitoControlloCedolini,
} from "./auroraCedolini";
import {
  aggiornaManifestMandatoSeCorrente,
  chiaveScopeAurora,
  chiaveScopeConversazioneAurora,
  leasePerScopeAurora,
  mandatoCompatibileConAmbito,
  mandatoPerScope,
  mandatiAttiviUnici,
  mandatiDaRevocare,
} from "./auroraScope";
import { creaSequenziatoreRichieste } from "./ultimaRichiesta";

// Chat e strumenti usano esclusivamente il mandato visibile dello scope azienda-periodo corrente.

interface Bolla {
  id: number;
  chi: "utente" | "aurora";
  testo: string;
  citazione?: string;
}

const SUGGERIMENTI = [
  { etichetta: "Spiega questa pagina", richiesta: "Spiegami in modo semplice a cosa serve questa pagina." },
  { etichetta: "Cosa devo fare", richiesta: "Quali sono i passaggi normali da fare in questa pagina?" },
  { etichetta: "Non ho capito", richiesta: "Non ho capito questa schermata: fammi una domanda per capire cosa mi serve." },
];

const SUGGERIMENTI_CON_TESTO = [
  { etichetta: "Spiegami questo", richiesta: "Spiegami questo testo in parole semplici." },
  { etichetta: "Fammi un esempio", richiesta: "Spiegami questo testo con un esempio pratico." },
  { etichetta: "Cosa significa", richiesta: "Qual è il significato più importante di questo testo?" },
];

const BOZZA_TESTO_SELEZIONATO = "Spiegami questo testo in parole semplici.";

function periodoCorrente() {
  const oggi = new Date();
  return `${oggi.getFullYear()}-${String(oggi.getMonth() + 1).padStart(2, "0")}`;
}

export function AuroraPanel({
  azienda,
  sezione,
  contesto,
  onRimuoviContesto,
  onChiudi,
}: {
  azienda: Azienda | null;
  sezione: string;
  contesto: ContestoAurora | null;
  onRimuoviContesto: () => void;
  onChiudi: () => void;
}) {
  const [bolle, setBolle] = useState<Bolla[]>([]);
  const [storia, setStoria] = useState<MessaggioChatAi[]>([]);
  const [bozza, setBozza] = useState("");
  const [inCorso, setInCorso] = useState(false);
  const [stato, setStato] = useState<StatoChatAi | null>(null);
  const [periodo, setPeriodo] = useState(periodoCorrente);
  const [preflight, setPreflight] = useState<EsitoPreflightAi | null>(null);
  const [ambitoPreflight, setAmbitoPreflight] = useState<AmbitoControlloCedolini | null>(null);
  const [mandato, setMandato] = useState<GrantAiEmesso | null>(null);
  const [mandatiResidui, setMandatiResidui] = useState<ManifestGrantAi[]>([]);
  const [inventarioCaricato, setInventarioCaricato] = useState(false);
  const [inventarioInCorso, setInventarioInCorso] = useState(true);
  const [esitoCedolini, setEsitoCedolini] = useState<EsitoQueryAi<RigaCedolinoMeseAi> | null>(null);
  const [trasparenza, setTrasparenza] = useState<CosaVedeAi | null>(null);
  const [erroreControllo, setErroreControllo] = useState<string | null>(null);
  const [controlloInCorso, setControlloInCorso] = useState(false);
  const [scopeChat, setScopeChat] = useState<string | null>(null);
  const [proposta, setProposta] = useState<PropostaAzioneAurora | null>(null);
  const [esitoProposta, setEsitoProposta] = useState<EsitoPropostaAurora | null>(null);
  const [scopeProposta, setScopeProposta] = useState<string | null>(null);
  const [propostaInCorso, setPropostaInCorso] = useState(false);
  const [scopeEsito, setScopeEsito] = useState<string | null>(null);
  const [scopeTrasparenza, setScopeTrasparenza] = useState<string | null>(null);
  const [quotaConfermata, setQuotaConfermata] = useState<{
    grantId: string;
    scopeMandato: string;
  } | null>(null);
  const prossimoId = useRef(1);
  const fondo = useRef<HTMLDivElement>(null);
  const storiaRef = useRef<MessaggioChatAi[]>([]);
  const campoMessaggio = useRef<HTMLTextAreaElement>(null);
  const chiusuraInCorso = useRef(false);
  const montato = useRef(true);
  const avvioInCorsoRef = useRef(false);
  const mandatoRef = useRef<GrantAiEmesso | null>(mandato);
  mandatoRef.current = mandato;
  const richiesteChat = useRef(creaSequenziatoreRichieste());
  const richiestePreflight = useRef(creaSequenziatoreRichieste());
  const richiesteQuery = useRef(creaSequenziatoreRichieste());
  const richiesteTrasparenza = useRef(creaSequenziatoreRichieste());
  const richiesteRevoca = useRef(creaSequenziatoreRichieste());
  const richiesteMandati = useRef(creaSequenziatoreRichieste());
  const richiesteStato = useRef(creaSequenziatoreRichieste());
  const richiesteAvvio = useRef(creaSequenziatoreRichieste());
  const richiesteQuota = useRef(creaSequenziatoreRichieste());
  const richiesteProposta = useRef(creaSequenziatoreRichieste());
  const ambitoCorrente = azienda ? ambitoControlloCedolini(azienda.id, periodo) : null;
  const ambitoCorrenteRef = useRef<AmbitoControlloCedolini | null>(ambitoCorrente);
  ambitoCorrenteRef.current = ambitoCorrente;
  const scopeMandato = chiaveScopeAurora(azienda?.id, periodo);
  const scopeMandatoRef = useRef(scopeMandato);
  scopeMandatoRef.current = scopeMandato;
  const scopeConversazione = chiaveScopeConversazioneAurora(azienda?.id, periodo, sezione);
  const scopeConversazioneRef = useRef(scopeConversazione);
  scopeConversazioneRef.current = scopeConversazione;
  const scopeConversazionePrecedente = useRef(scopeConversazione);
  const ultimoContesto = useRef<ContestoAurora | null>(contesto);
  const scopeContesto = useRef<string | null>(contesto ? scopeConversazione : null);
  if (ultimoContesto.current !== contesto) {
    ultimoContesto.current = contesto;
    scopeContesto.current = contesto ? scopeConversazione : null;
  }
  const contestoCorrente = scopeContesto.current === scopeConversazione ? contesto : null;
  const mandatoCorrente = mandatoPerScope(mandato, azienda?.id, periodo);
  const leaseCorrente = leasePerScopeAurora(mandato, azienda?.id, periodo);
  const preflightCorrente = stessoAmbitoControlloCedolini(ambitoPreflight, ambitoCorrente)
    ? preflight
    : null;
  const esitoCorrente = scopeEsito === scopeMandato ? esitoCedolini : null;
  const trasparenzaCorrente = scopeTrasparenza === scopeMandato ? trasparenza : null;
  const bolleCorrenti = scopeChat === scopeConversazione ? bolle : [];
  const propostaCorrente = scopeProposta === scopeConversazione ? proposta : null;
  const esitoPropostaCorrente = scopeProposta === scopeConversazione ? esitoProposta : null;
  const quotaCorrenteConfermata = Boolean(mandatoCorrente
    && quotaConfermata?.grantId === mandatoCorrente.manifest.grantId
    && quotaConfermata.scopeMandato === scopeMandato);

  const riconciliaMandati = useCallback(async (): Promise<ManifestGrantAi[]> => {
    const richiesta = richiesteMandati.current.apri();
    if (montato.current) {
      setInventarioCaricato(false);
      setInventarioInCorso(true);
    }
    try {
      const grants = await api.aiGrants(richiesta.signal);
      if (!richiesta.attiva()) throw new DOMException("Inventario superato", "AbortError");
      const attivi = mandatiAttiviUnici(grants);
      if (montato.current) {
        const grantCorrenteId = mandatoRef.current?.manifest.grantId;
        setMandatiResidui(attivi.filter((grant) => grant.grantId !== grantCorrenteId));
        setInventarioCaricato(true);
      }
      return attivi;
    } finally {
      if (richiesta.attiva() && montato.current) setInventarioInCorso(false);
    }
  }, []);

  const riconciliaQuotaChat = async (grantId: string, scopeAtteso: string) => {
    if (!montato.current
      || scopeMandatoRef.current !== scopeAtteso
      || mandatoRef.current?.manifest.grantId !== grantId) return;
    const richiesta = richiesteQuota.current.apri();
    try {
      const risultato = await api.aiCosaVede(grantId, richiesta.signal);
      if (!richiesta.attiva()
        || !montato.current
        || scopeMandatoRef.current !== scopeAtteso
        || mandatoRef.current?.manifest.grantId !== grantId) return;
      setMandato((corrente) =>
        aggiornaManifestMandatoSeCorrente(corrente, risultato.manifest, grantId));
      setQuotaConfermata({ grantId, scopeMandato: scopeAtteso });
    } catch {
      // La quota resta esplicitamente non confermata finché il server non è leggibile.
    }
  };

  useEffect(() => { storiaRef.current = storia; }, [storia]);

  useEffect(() => {
    if (!contestoCorrente) return;
    setBozza((corrente) => corrente.trim() || BOZZA_TESTO_SELEZIONATO);
  }, [contestoCorrente]);

  useEffect(() => {
    const id = window.setTimeout(() => campoMessaggio.current?.focus(), 0);
    return () => window.clearTimeout(id);
  }, []);

  useEffect(() => {
    montato.current = true;
    const richiestaStato = richiesteStato.current.apri();
    api.aiChatStato(richiestaStato.signal)
      .then((risultato) => { if (richiestaStato.attiva()) setStato(risultato); })
      .catch(() => { if (richiestaStato.attiva()) setStato(null); });
    void riconciliaMandati().catch((e) => {
      if (e instanceof DOMException && e.name === "AbortError") return;
      if (montato.current)
        setErroreControllo("Impossibile verificare i mandati attivi: Aurora resta bloccata.");
    });
    return () => {
      richiestaStato.invalida();
    };
  }, [riconciliaMandati]);

  useEffect(() => {
    richiestePreflight.current.invalida();
    richiesteQuery.current.invalida();
    richiesteTrasparenza.current.invalida();
    richiesteAvvio.current.invalida();
    richiesteQuota.current.invalida();
    setPreflight(null);
    setAmbitoPreflight(null);
    setEsitoCedolini(null);
    setScopeEsito(null);
    setTrasparenza(null);
    setScopeTrasparenza(null);
    setQuotaConfermata(null);
    // Se è stato annullato soltanto un preflight, nessuna revoca successiva deve restare
    // bloccata. Con un mandato presente sarà invece la revoca a possedere lo stato busy.
    if (!mandatoRef.current && !avvioInCorsoRef.current) setControlloInCorso(false);
  }, [scopeMandato]);

  useEffect(() => {
    if (scopeConversazionePrecedente.current === scopeConversazione) return;
    scopeConversazionePrecedente.current = scopeConversazione;
    richiesteChat.current.invalida();
    setBolle([]);
    setStoria([]);
    setScopeChat(null);
    setInCorso(false);
    setBozza("");
    scopeContesto.current = null;
    onRimuoviContesto();
  }, [onRimuoviContesto, scopeConversazione]);

  useEffect(() => {
    if (!mandato || mandatoPerScope(mandato, azienda?.id, periodo)) return;
    const grantId = mandato.manifest.grantId;
    const manifestPrecedente = mandato.manifest;
    const scopeRevoca = scopeMandato;
    const richiestaRevoca = richiesteRevoca.current.apri();
    // Non mostrare mai righe del cliente precedente sotto la nuova intestazione. Conserviamo
    // soltanto il manifest minimo necessario a rendere revocabile un eventuale errore di rete.
    setMandato(null);
    setQuotaConfermata(null);
    setMandatiResidui((correnti) => [
      manifestPrecedente,
      ...correnti.filter((grant) => grant.grantId !== grantId),
    ]);
    setPreflight(null);
    setAmbitoPreflight(null);
    setEsitoCedolini(null);
    setScopeEsito(null);
    setTrasparenza(null);
    setScopeTrasparenza(null);
    setControlloInCorso(true);
    const messaggioRevoca = "Cambio azienda o mese: sto revocando il mandato precedente.";
    setErroreControllo(messaggioRevoca);
    void api.aiRevocaGrant(grantId)
      .then(() => {
        setMandatiResidui((correnti) => correnti.filter((grant) => grant.grantId !== grantId));
        if (richiestaRevoca.attiva() && scopeMandatoRef.current === scopeRevoca)
          setErroreControllo((corrente) => corrente === messaggioRevoca ? null : corrente);
      })
      .catch(() => {
        if (richiestaRevoca.attiva() && scopeMandatoRef.current === scopeRevoca)
          setErroreControllo("Il vecchio mandato non è stato revocato: usa Revoca mandati residui prima di continuare.");
      })
      .finally(() => {
        if (richiestaRevoca.attiva()) setControlloInCorso(false);
      });
  }, [azienda?.id, periodo, mandato, scopeMandato]);

  useEffect(() => () => {
    montato.current = false;
    richiesteChat.current.invalida();
    richiestePreflight.current.invalida();
    richiesteQuery.current.invalida();
    richiesteTrasparenza.current.invalida();
    richiesteRevoca.current.invalida();
    richiesteMandati.current.invalida();
    richiesteStato.current.invalida();
    richiesteAvvio.current.invalida();
    richiesteQuota.current.invalida();
    richiesteProposta.current.invalida();
  }, []);

  useEffect(() => {
    fondo.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [bolle, inCorso]);

  const invia = async (testo: string) => {
    const messaggio = testo.trim();
    if (!messaggio || inCorso) return;
    const scopeRichiesto = scopeConversazione;
    const scopeQuotaRichiesto = scopeMandato;
    const lease = leaseCorrente;
    const grantIdQuota = mandatoCorrente?.manifest.grantId ?? null;
    const contestoDaInviare = contestoCorrente;
    const storiaDaInviare = scopeChat === scopeRichiesto ? storiaRef.current : [];
    const bollaUtente: Bolla = {
      id: prossimoId.current++,
      chi: "utente",
      testo: messaggio,
      citazione: contestoDaInviare ? anteprimaContestoAurora(contestoDaInviare, 240) : undefined,
    };
    setBolle((prima) => scopeChat === scopeRichiesto ? [...prima, bollaUtente] : [bollaUtente]);
    setScopeChat(scopeRichiesto);
    setBozza("");
    if (!lease || !grantIdQuota) {
      setBolle((prima) => [...prima, {
        id: prossimoId.current++,
        chi: "aurora",
        testo: "Prima mostra e avvia il controllo in sola lettura: Aurora può rispondere soltanto con un mandato valido per questa azienda e questo mese.",
      }]);
      setBozza(messaggio);
      return;
    }
    const richiesta = richiesteChat.current.apri();
    richiesteQuota.current.invalida();
    setQuotaConfermata(null);
    setInCorso(true);
    try {
      const r = await api.aiChat(
        messaggio,
        storiaDaInviare,
        lease,
        azienda?.id ?? null,
        contestoDaInviare
          ? { testo: contestoDaInviare.testo, sezione: contestoDaInviare.sezione }
          : null,
        sezione,
        richiesta.signal,
      );
      if (!richiesta.attiva() || scopeConversazioneRef.current !== scopeRichiesto) return;
      setBolle((prima) => [...prima, { id: prossimoId.current++, chi: "aurora", testo: r.testo }]);
      if (r.azione) {
        setProposta(r.azione);
        setEsitoProposta(null);
        setScopeProposta(scopeRichiesto);
      }
      if (r.ok) {
        setStoria(r.storia);
        setScopeChat(scopeRichiesto);
        if (contestoDaInviare) onRimuoviContesto();
      } else {
        // Il provider spento risponde in modo controllato: conserva testo e domanda per il retry.
        setBozza(messaggio);
      }
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") return;
      if (!richiesta.attiva() || scopeConversazioneRef.current !== scopeRichiesto) return;
      setBozza(messaggio);
      setBolle((prima) => [...prima, {
        id: prossimoId.current++,
        chi: "aurora",
        testo: `Non riesco a parlare con l'assistente: ${e instanceof Error ? e.message : "errore"}.`,
      }]);
    } finally {
      await riconciliaQuotaChat(grantIdQuota, scopeQuotaRichiesto);
      if (richiesta.attiva()) setInCorso(false);
    }
  };

  const rimuoviContesto = () => {
    setBozza((corrente) => corrente === BOZZA_TESTO_SELEZIONATO ? "" : corrente);
    onRimuoviContesto();
  };

  // Conferma, annullo e ripristino rimandano al server soltanto il token firmato. L'esito
  // arriva anche dentro 409/410: viene mostrato com'è, senza ritentare nulla in automatico.
  const operaSuProposta = async (
    chiama: (token: string, signal?: AbortSignal) => Promise<EsitoPropostaAurora>,
    token: string,
  ) => {
    if (propostaInCorso) return;
    const scopeRichiesto = scopeConversazione;
    const scopeQuotaRichiesto = scopeMandato;
    const grantIdQuota = mandatoCorrente?.manifest.grantId ?? null;
    const richiesta = richiesteProposta.current.apri();
    setPropostaInCorso(true);
    try {
      let esito: EsitoPropostaAurora;
      try {
        esito = await chiama(token, richiesta.signal);
      } catch (e) {
        if (e instanceof DOMException && e.name === "AbortError") return;
        esito = esitoPropostaDaErrore(e);
      }
      if (!richiesta.attiva() || scopeConversazioneRef.current !== scopeRichiesto) return;
      setEsitoProposta(esito);
      setScopeProposta(scopeRichiesto);
    } finally {
      // Un effetto applicato consuma quota del mandato: il contatore si rilegge dal server.
      if (grantIdQuota) await riconciliaQuotaChat(grantIdQuota, scopeQuotaRichiesto);
      if (richiesta.attiva()) setPropostaInCorso(false);
    }
  };

  const suggerimenti = contestoCorrente ? SUGGERIMENTI_CON_TESTO : SUGGERIMENTI;
  const conteggi = esitoCorrente ? contaStatiCedolini(esitoCorrente.righe) : null;

  const mostraPreflight = async () => {
    if (!azienda || controlloInCorso) return;
    if (mandato) {
      setErroreControllo("Attendi la revoca del mandato precedente prima di iniziare un nuovo controllo.");
      return;
    }
    setControlloInCorso(true);
    setErroreControllo(null);
    setPreflight(null);
    setAmbitoPreflight(null);
    setEsitoCedolini(null);
    setScopeEsito(null);
    setTrasparenza(null);
    setScopeTrasparenza(null);
    const ambitoRichiesto = ambitoControlloCedolini(azienda.id, periodo);
    const scopeRichiesto = scopeMandato;
    const richiesta = richiestePreflight.current.apri();
    try {
      const attivi = await riconciliaMandati();
      if (!richiesta.attiva() || scopeMandatoRef.current !== scopeRichiesto) return;
      if (attivi.length > 0) {
        setErroreControllo("Revoca tutti i mandati attivi prima di iniziare un nuovo controllo.");
        return;
      }
      const esito = await api.aiPreflight(
        richiestaControlloCedolini(ambitoRichiesto.aziendaId, ambitoRichiesto.periodo),
        richiesta.signal,
      );
      if (!richiesta.attiva() || scopeMandatoRef.current !== scopeRichiesto) return;
      setPreflight(esito);
      setAmbitoPreflight(ambitoRichiesto);
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") {
        richiesta.invalida();
        return;
      }
      if (!richiesta.attiva() || scopeMandatoRef.current !== scopeRichiesto) return;
      setErroreControllo(e instanceof Error ? e.message : "Preflight non disponibile.");
    } finally {
      if (richiesta.attiva() && scopeMandatoRef.current === scopeRichiesto)
        setControlloInCorso(false);
    }
  };

  const avviaControllo = async () => {
    if (!azienda || !preflightCorrente?.consentito || controlloInCorso) return;
    const ambitoAvvio = ambitoControlloCedolini(azienda.id, periodo);
    const scopeAvvio = scopeMandato;
    const richiestaAvvio = richiesteAvvio.current.apri();
    let nuovoMandato: GrantAiEmesso | null = null;
    let completato = false;
    let emissioneTentata = false;
    avvioInCorsoRef.current = true;
    setControlloInCorso(true);
    setErroreControllo(null);
    try {
      const attiviPrima = await riconciliaMandati();
      if (!richiestaAvvio.attiva() || scopeMandatoRef.current !== scopeAvvio) return;
      if (attiviPrima.length > 0) {
        setErroreControllo("Esistono già mandati attivi: revocali prima di emetterne uno nuovo.");
        return;
      }

      emissioneTentata = true;
      nuovoMandato = await api.aiEmettiGrant(
        richiestaControlloCedolini(ambitoAvvio.aziendaId, ambitoAvvio.periodo),
      );
      if (!richiestaAvvio.attiva()
        || !stessoAmbitoControlloCedolini(ambitoAvvio, ambitoCorrenteRef.current)
        || scopeMandatoRef.current !== scopeAvvio) {
        throw new Error("Azienda o mese cambiati durante l’emissione: il mandato sarà revocato senza leggere dati.");
      }
      const richiestaQuery = richiesteQuery.current.apri();
      const esito = await api.aiQuery<RigaCedolinoMeseAi>(
        nuovoMandato.lease,
        "Cedolini",
        richiestaQuery.signal,
      );
      if (!richiestaQuery.attiva() || scopeMandatoRef.current !== scopeAvvio)
        throw new Error("Azienda o mese cambiati durante la lettura: mandato revocato. Ripeti il preflight.");
      if (!esito.consentito) throw new Error(esito.motivo ?? "Accesso negato dal Governor.");
      setMandato(nuovoMandato);
      setMandatiResidui((correnti) => correnti.filter(
        (grant) => grant.grantId !== nuovoMandato?.manifest.grantId));
      setEsitoCedolini(esito);
      setScopeEsito(scopeAvvio);
      setMandato({
        ...nuovoMandato,
        manifest: {
          ...nuovoMandato.manifest,
          lettureUsate: nuovoMandato.manifest.maxLetture - esito.lettureRimanenti,
        },
      });
      setQuotaConfermata({
        grantId: nuovoMandato.manifest.grantId,
        scopeMandato: scopeAvvio,
      });
      completato = true;
    } catch (e) {
      if (!emissioneTentata && !nuovoMandato) return;
      let inventarioDopoErrore: ManifestGrantAi[] = [];
      try {
        inventarioDopoErrore = await riconciliaMandati();
      } catch {
        // Se la risposta di emissione è andata persa, il prossimo tentativo resta bloccato
        // finché l'inventario non torna leggibile.
      }
      const candidati = mandatiDaRevocare(
        nuovoMandato,
        [],
        inventarioDopoErrore.filter((grant) =>
          mandatoCompatibileConAmbito(grant, ambitoAvvio.aziendaId, ambitoAvvio.periodo)),
      );
      const falliti = new Map<string, ManifestGrantAi>();
      const revocaCandidati = async (manifesti: ManifestGrantAi[]) => {
        if (completato) return;
        for (const manifest of mandatiAttiviUnici(manifesti)) {
          try {
            await api.aiRevocaGrant(manifest.grantId);
            falliti.delete(manifest.grantId);
          } catch {
            falliti.set(manifest.grantId, manifest);
          }
        }
      };
      await revocaCandidati(candidati);

      // Seconda riconciliazione: copre il caso in cui il POST abbia creato il grant ma la
      // risposta sia andata persa e il primo inventario fosse temporaneamente indisponibile.
      let inventarioFinale: ManifestGrantAi[] = [];
      try {
        const ancoraAttivi = await riconciliaMandati();
        await revocaCandidati(ancoraAttivi.filter((grant) =>
          mandatoCompatibileConAmbito(grant, ambitoAvvio.aziendaId, ambitoAvvio.periodo)));
        inventarioFinale = await riconciliaMandati();
      } catch {
        // Il gate dell'inventario resta chiuso; gli ID noti falliti restano visibili.
      }

      if (montato.current) {
        const nonRevocati = mandatiAttiviUnici([
          ...falliti.values(),
          ...inventarioFinale.filter((grant) =>
            mandatoCompatibileConAmbito(grant, ambitoAvvio.aziendaId, ambitoAvvio.periodo)),
        ]);
        if (nonRevocati.length > 0) {
          setMandatiResidui((correnti) => mandatiAttiviUnici([...correnti, ...nonRevocati]));
          setErroreControllo(
            "Il controllo è stato interrotto e alcuni mandati non sono stati revocati: usa Revoca mandati residui.",
          );
        } else if (richiestaAvvio.attiva() && scopeMandatoRef.current === scopeAvvio) {
          setErroreControllo(e instanceof Error ? e.message : "Controllo non riuscito.");
        }
      }
    } finally {
      avvioInCorsoRef.current = false;
      if (montato.current) setControlloInCorso(false);
    }
  };

  const mostraTrasparenza = async () => {
    if (!mandatoCorrente || controlloInCorso) return;
    const scopeRichiesto = scopeMandato;
    const richiesta = richiesteTrasparenza.current.apri();
    setControlloInCorso(true);
    setErroreControllo(null);
    try {
      const risultato = await api.aiCosaVede(mandatoCorrente.manifest.grantId, richiesta.signal);
      if (!richiesta.attiva() || scopeMandatoRef.current !== scopeRichiesto) return;
      setTrasparenza(risultato);
      setScopeTrasparenza(scopeRichiesto);
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") return;
      if (!richiesta.attiva() || scopeMandatoRef.current !== scopeRichiesto) return;
      setErroreControllo(e instanceof Error ? e.message : "Registro non disponibile.");
    } finally {
      if (richiesta.attiva() && scopeMandatoRef.current === scopeRichiesto)
        setControlloInCorso(false);
    }
  };

  const arresta = async () => {
    if (controlloInCorso) return;
    richiesteRevoca.current.invalida();
    richiesteMandati.current.invalida();
    richiesteChat.current.invalida();
    richiestePreflight.current.invalida();
    richiesteQuery.current.invalida();
    richiesteTrasparenza.current.invalida();
    richiesteQuota.current.invalida();
    setInCorso(false);
    setControlloInCorso(true);
    setErroreControllo(null);
    try {
      await api.aiArresta();
      setMandato(null);
      setQuotaConfermata(null);
      setMandatiResidui([]);
      setBolle([]);
      setStoria([]);
      setScopeChat(null);
      setPreflight(null);
      setAmbitoPreflight(null);
      setEsitoCedolini(null);
      setScopeEsito(null);
      setTrasparenza(null);
      setScopeTrasparenza(null);
    } catch (e) {
      setErroreControllo(e instanceof Error ? e.message : "Arresto non riuscito.");
    } finally {
      setControlloInCorso(false);
    }
  };

  const chiudiSicuro = async () => {
    if (chiusuraInCorso.current) return;
    if (controlloInCorso) {
      setErroreControllo("Attendi la conclusione del controllo prima di chiudere Aurora.");
      return;
    }
    chiusuraInCorso.current = true;
    setControlloInCorso(true);
    setErroreControllo("Sto verificando e revocando tutti i mandati prima di chiudere Aurora.");
    try {
      const inventario = await riconciliaMandati();
      const daRevocare = mandatiDaRevocare(mandatoRef.current, mandatiResidui, inventario);
      const falliti: ManifestGrantAi[] = [];
      for (const manifest of daRevocare) {
        try {
          await api.aiRevocaGrant(manifest.grantId);
        } catch {
          falliti.push(manifest);
        }
      }
      if (falliti.length > 0) {
        setMandato(null);
        setQuotaConfermata(null);
        setMandatiResidui(mandatiAttiviUnici(falliti));
        setErroreControllo(
          `Non chiudo Aurora: ${falliti.length} mandat${falliti.length === 1 ? "o non è stato revocato" : "i non sono stati revocati"}. Riprova Revoca mandati residui.`,
        );
        return;
      }

      const rimasti = await riconciliaMandati();
      if (rimasti.length > 0) {
        setMandato(null);
        setQuotaConfermata(null);
        setMandatiResidui(rimasti);
        setErroreControllo("Non chiudo Aurora: il server segnala ancora mandati attivi.");
        return;
      }
      setMandato(null);
      setQuotaConfermata(null);
      setMandatiResidui([]);
      onChiudi();
    } catch (e) {
      setErroreControllo(
        `Non chiudo Aurora perché non posso verificare o revocare tutti i mandati: ${e instanceof Error ? e.message : "errore"}. Riprova.`,
      );
    } finally {
      chiusuraInCorso.current = false;
      setControlloInCorso(false);
    }
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") void chiudiSicuro(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  return (
    <aside id="aurora-panel" className="aurora-panel glass-panel" role="complementary" aria-label="Aurora PaghePro">
      <header className="cp-mandato">
        <div className="cp-mandato-riga">
          <span className="cp-titolo"><Sparkles size={16} aria-hidden="true" /> Aurora</span>
          <span className={`cp-badge${mandatoCorrente ? " attivo" : ""}`}>
            {mandatoCorrente ? "mandato attivo" : stato?.attiva
              ? `${stato.provider} · ${stato.modello}`
              : "controllo deterministico"}
          </span>
          <button className="cp-chiudi" type="button" aria-label="Chiudi Aurora" disabled={controlloInCorso} onClick={() => void chiudiSicuro()}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
        <p className="cp-scope">
          Pagina: <b>{sezione}</b>{azienda ? <> · Azienda: <b>{azienda.ragioneSociale}</b></> : null}<br />
          Aurora risponde e usa strumenti in sola lettura soltanto con il mandato mostrato per
          questa azienda e questo mese. Il controllo cedolini legge al massimo 10 righe e non
          modifica nulla.<br />
          {stato?.attiva ? (
            <>Le domande scritte qui vengono inviate al provider <b>{stato.provider}</b> per
            generare la risposta: non incollare dati che non vuoi condividere.</>
          ) : (
            <>Nessun testo lascia il server: i provider AI sono spenti e il controllo
            cedolini resta interamente dentro PaghePro.</>
          )}
        </p>
      </header>

      {contestoCorrente && (
        <section className="cp-contesto" aria-label="Testo selezionato per Aurora">
          <div className="cp-contesto-testa">
            <strong>Testo selezionato · {contestoCorrente.sezione}</strong>
            <button type="button" onClick={rimuoviContesto} aria-label="Rimuovi testo selezionato">
              <X size={15} aria-hidden="true" />
            </button>
          </div>
          <blockquote>{anteprimaContestoAurora(contestoCorrente, 320)}</blockquote>
          <small>
            {contestoCorrente.troncato ? "Testo ridotto a 4.000 caratteri. " : ""}
            Sarà allegato una volta sola alla prossima domanda, anche se usi un pulsante rapido.
          </small>
        </section>
      )}

      <div className="cp-thread" role="log" aria-live="polite" aria-relevant="additions text">
        <section className="cp-preflight" aria-label="Controllo governato dei cedolini">
          <span className="cp-preflight-tag">Controllo cedolini · lotto 10</span>
          {!azienda ? (
            <div className="cp-esito avviso">
              <strong>Scegli prima un’azienda</strong>
              <span>Aurora non seleziona mai automaticamente il primo cliente.</span>
            </div>
          ) : (
            <>
              <label className="cp-mese">
                <span>Mese da controllare</span>
                <input
                  type="month"
                  value={periodo}
                  disabled={Boolean(mandato) || controlloInCorso}
                  onChange={(e) => {
                    setPeriodo(e.target.value);
                    setPreflight(null);
                    setEsitoCedolini(null);
                    setScopeEsito(null);
                    setTrasparenza(null);
                    setScopeTrasparenza(null);
                  }}
                />
              </label>
              {!preflightCorrente && !mandatoCorrente && (
                <button className="btn secondary" type="button" disabled={!inventarioCaricato || inventarioInCorso || controlloInCorso || Boolean(mandato) || mandatiResidui.length > 0} onClick={mostraPreflight}>
                  {inventarioInCorso ? "Verifica mandati…" : "Mostra cosa controllerà"}
                </button>
              )}
            </>
          )}

          {preflightCorrente && !mandatoCorrente && (
            <div className={`cp-esito ${preflightCorrente.consentito ? "ok" : "urgente"}`}>
              <strong>{preflightCorrente.consentito ? "Pronto per la tua conferma" : "Controllo non consentito"}</strong>
              <span>{preflightCorrente.descrizione ?? preflightCorrente.motivo}</span>
              {preflightCorrente.consentito && (
                <>
                  <small>
                    Ogni lettura mostra al massimo <b>{DIMENSIONE_LOTTO_CEDOLINI_AURORA} righe</b>. Budget complessivo del mandato: <b>{preflightCorrente.maxLetture ?? "definito dal server"} letture</b> · durata: <b>{preflightCorrente.durataMinuti ?? "definita dal server"} minuti</b>.
                  </small>
                  <button className="btn primary" type="button" disabled={!inventarioCaricato || inventarioInCorso || controlloInCorso || mandatiResidui.length > 0} onClick={avviaControllo}>
                    Avvia controllo in sola lettura
                  </button>
                </>
              )}
            </div>
          )}

          {mandatoCorrente && (
            <div className="cp-esito ok">
              <strong>{mandatoCorrente.manifest.periodo} · sola lettura</strong>
              {quotaCorrenteConfermata ? (
                <span>
                  Quota: {mandatoCorrente.manifest.lettureUsate}/{mandatoCorrente.manifest.maxLetture} righe ·
                  scade {new Date(mandatoCorrente.manifest.scadeUtc).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit" })}
                </span>
              ) : (
                <span>
                  Quota da verificare con il server · scade {new Date(mandatoCorrente.manifest.scadeUtc).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit" })}
                </span>
              )}
              {quotaCorrenteConfermata
                && mandatoCorrente.manifest.maxLetture - mandatoCorrente.manifest.lettureUsate < DIMENSIONE_LOTTO_CEDOLINI_AURORA && (
                <small>
                  Restano meno di {DIMENSIONE_LOTTO_CEDOLINI_AURORA} letture: non c’è budget per un altro lotto completo.
                </small>
              )}
              <div className="cp-mandato-azioni">
                <button className="cp-chip" type="button" disabled={controlloInCorso} onClick={mostraTrasparenza}>
                  Dati letti dal controllo
                </button>
                <button className="cp-chip stop" type="button" disabled={controlloInCorso} onClick={arresta}>
                  Revoca mandato
                </button>
              </div>
            </div>
          )}

          {mandatiResidui.length > 0 && (
            <div className="cp-esito avviso">
              <strong>Mandato precedente ancora attivo</strong>
              <span>
                La lease non è salvata nello storage, ma il mandato resta attivo sul server. Revoca
                i {mandatiResidui.length} mandati residui prima di iniziare un nuovo controllo.
              </span>
              <button className="btn secondary" type="button" disabled={controlloInCorso} onClick={arresta}>
                Revoca mandati residui
              </button>
            </div>
          )}

          {!inventarioCaricato && !inventarioInCorso && mandatiResidui.length === 0 && (
            <div className="cp-esito urgente">
              <strong>Inventario mandati non verificato</strong>
              <span>Aurora non può emettere nuovi mandati finché il server non risponde.</span>
              <button
                className="btn secondary"
                type="button"
                disabled={controlloInCorso}
                onClick={() => {
                  setErroreControllo(null);
                  void riconciliaMandati().catch(() => {
                    if (montato.current)
                      setErroreControllo("Impossibile verificare i mandati attivi: Aurora resta bloccata.");
                  });
                }}
              >
                Riprova verifica mandati
              </button>
            </div>
          )}

          {conteggi && esitoCorrente && (
            <div className="cp-lotto">
              <strong>
                {esitoCorrente.righe.length} di {esitoCorrente.totale} rapporti controllati
              </strong>
              <span className="cp-lotto-riepilogo">
                {conteggi.cartellino_presente} cartellini presenti · {conteggi.da_compilare} da compilare · {conteggi.gia_elaborato} già elaborati · {conteggi.bloccato} bloccati · {conteggi.protetto} protetti
              </span>
              <ul>
                {esitoCorrente.righe.map((riga) => (
                  <li key={riga.rapportoId}>
                    <div><b>{riga.nominativo}</b><span className={`cp-stato ${riga.stato}`}>{riga.stato.replaceAll("_", " ")}</span></div>
                    <small>{riga.motivo}</small>
                  </li>
                ))}
              </ul>
              {esitoCorrente.totale > esitoCorrente.righe.length && (
                <small>
                  Questo incremento mostra i primi 10. I successivi richiedono il prossimo
                  checkpoint con cursore verificabile e non vengono letti ora.
                </small>
              )}
            </div>
          )}

          {trasparenzaCorrente && (
            <div className="cp-trasparenza">
              <div className="cp-trasparenza-testa">
                <strong>Cosa ha letto il controllo</strong>
                <span>{trasparenzaCorrente.manifest.risorse.join(", ")}</span>
              </div>
              <ul>
                {trasparenzaCorrente.eventi.map((evento) => (
                  <li key={evento.sequenza}>
                    <span className={`cp-evento${evento.motivo ? " nega" : ""}`}>{evento.evento}</span>
                    <span className="cp-evento-dettaglio">{evento.dettaglio}{evento.motivo ? ` · ${evento.motivo}` : ""}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {erroreControllo && (
            <div className="cp-esito urgente"><strong>Controllo interrotto</strong><span>{erroreControllo}</span></div>
          )}
        </section>

        <div className="cp-msg">
          <p>
            Ciao! Seleziona una frase, un errore o una voce della pagina e aprimi: te la spiego
            in modo semplice. Se non capisco che cosa intendi, ti faccio una domanda.
          </p>
        </div>

        {stato && !stato.attiva && (
          <div className="cp-esito avviso">
            <strong>Assistente non ancora configurato</strong>
            <span>{stato.messaggio}</span>
          </div>
        )}

        {bolleCorrenti.map((b) => (
          <div className={`cp-msg${b.chi === "utente" ? " mio" : ""}`} key={b.id}>
            <p>{b.testo}</p>
            {b.citazione && <blockquote className="cp-citazione">“{b.citazione}”</blockquote>}

          </div>
        ))}

        {propostaCorrente && (
          <div className="cp-azione" aria-label="Proposta di modifica preparata da Aurora">
            {!propostaCorrente.confermabile && (
              <span className="cp-azione-tag">Anteprima di collaudo — non può modificare dati</span>
            )}
            <strong>{propostaCorrente.titolo}</strong>
            {propostaCorrente.diff.length > 0 && (
              <dl className="cp-azione-campi">
                {propostaCorrente.diff.map((riga, i) => (
                  <div key={i}>
                    <dt>{riga.etichetta}</dt>
                    <dd>
                      {riga.da != null && riga.da !== "" && <>{riga.da} → </>}
                      {riga.a ?? "—"}
                    </dd>
                  </div>
                ))}
              </dl>
            )}

            {esitoPropostaCorrente ? (
              <>
                <div className={`cp-esito${esitoRassicurante(esitoPropostaCorrente) ? "" : " avviso"}`}>
                  <span>{esitoPropostaCorrente.messaggio}</span>
                </div>
                {puoRipristinare(esitoPropostaCorrente) && (
                  <div className="cp-azione-bottoni">
                    <button
                      type="button"
                      className="btn"
                      disabled={propostaInCorso}
                      onClick={() => void operaSuProposta(
                        api.aiPropostaUndo, esitoPropostaCorrente.tokenUndo!)}
                    >
                      Ripristina com’era prima
                    </button>
                  </div>
                )}
              </>
            ) : propostaCorrente.confermabile && propostaCorrente.token ? (
              propostaScaduta(propostaCorrente, new Date()) ? (
                <div className="cp-esito avviso">
                  <span>Proposta scaduta: chiedi ad Aurora di prepararla di nuovo.</span>
                </div>
              ) : (
                <>
                  <p className="cp-azione-scadenza">
                    {descriviScadenzaProposta(propostaCorrente, new Date())} Nessun dato viene
                    modificato finché non confermi.
                  </p>
                  <div className="cp-azione-bottoni">
                    <button
                      type="button"
                      className="btn primary"
                      disabled={propostaInCorso}
                      onClick={() => void operaSuProposta(
                        api.aiPropostaConferma, propostaCorrente.token!)}
                    >
                      Conferma e applica
                    </button>
                    <button
                      type="button"
                      className="btn"
                      disabled={propostaInCorso}
                      onClick={() => void operaSuProposta(
                        api.aiPropostaAnnulla, propostaCorrente.token!)}
                    >
                      Annulla proposta
                    </button>
                  </div>
                </>
              )
            ) : null}
          </div>
        )}

        {inCorso && <div className="cp-msg cp-attesa"><p>Sto pensando…</p></div>}
        <div ref={fondo} />
      </div>

      <div className="cp-rapidi" aria-label="Domande rapide">
        {suggerimenti.map((s) => (
          <button key={s.etichetta} type="button" disabled={inCorso} onClick={() => invia(s.richiesta)}>
            {s.etichetta}
          </button>
        ))}
      </div>

      <form
        className="cp-comporre"
        onSubmit={(e) => { e.preventDefault(); void invia(bozza); }}
      >
        <textarea
          ref={campoMessaggio}
          value={bozza}
          onChange={(e) => setBozza(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              void invia(bozza);
            }
          }}
          placeholder="Scrivi ad Aurora… (Invio per inviare)"
          rows={2}
          aria-label="Messaggio per Aurora"
        />
        <button className="btn primary" type="submit" disabled={inCorso || !bozza.trim()} aria-label="Manda">
          <Send size={15} aria-hidden="true" />
        </button>
      </form>

      <footer className="cp-nota">
        <ShieldCheck size={14} aria-hidden="true" />
        <span>
          La chat invia al server mandato, messaggio, sezione, ID dell’azienda scelta e fino a 12 tue
          domande precedenti; il testo selezionato è monouso. Ogni lettura passa dal Governor e resta
          vincolata ad azienda, mese, risorse e quota del mandato visibile. Aurora non esegue mai
          modifiche da sola: può solo prepararle, si applicano con la tua conferma e restano
          annullabili.
        </span>
      </footer>
    </aside>
  );
}
