import type { EsitoPropostaAurora, PropostaAzioneAurora } from "./api";

// Logica pura della card proposta A3: interpretazione degli esiti del protocollo
// (anche quando arrivano come corpo di un errore HTTP), scadenza e ripristinabilità.

/** Stati in cui l'operazione risulta applicata e quindi eventualmente ripristinabile. */
const STATI_APPLICATA = new Set(["Applicata", "GiaApplicata"]);

/**
 * Il protocollo risponde con l'esito completo anche su 409 (Negata/Conflitto/InCorso) e
 * 410 (Scaduta): qui lo si recupera dal corpo dell'ApiError, riconosciuto per forma per
 * restare testabile fuori dal browser. Per 400/404 e per errori di rete si costruisce un
 * esito minimo con il messaggio disponibile, senza inventare stati applicati o token.
 */
export function esitoPropostaDaErrore(errore: unknown): EsitoPropostaAurora {
  const corpo = errore !== null && typeof errore === "object"
    ? (errore as { corpo?: unknown }).corpo
    : undefined;
  if (corpo !== null && typeof corpo === "object"
    && typeof (corpo as { stato?: unknown }).stato === "string") {
    const esito = corpo as Partial<EsitoPropostaAurora> & { stato: string };
    return {
      propostaId: typeof esito.propostaId === "string" ? esito.propostaId : null,
      stato: esito.stato,
      messaggio: typeof esito.messaggio === "string"
        ? esito.messaggio
        : errore instanceof Error ? errore.message : "Operazione non riuscita.",
      esitoJson: typeof esito.esitoJson === "string" ? esito.esitoJson : null,
      tokenUndo: typeof esito.tokenUndo === "string" ? esito.tokenUndo : null,
    };
  }
  return {
    propostaId: null,
    stato: "Errore",
    messaggio: errore instanceof Error ? errore.message : "Errore di comunicazione col server.",
    esitoJson: null,
    tokenUndo: null,
  };
}

export function propostaScaduta(proposta: PropostaAzioneAurora, adesso: Date): boolean {
  const scade = Date.parse(proposta.scadeUtc);
  return Number.isFinite(scade) && adesso.getTime() >= scade;
}

/** Testo semplice per l'utente: quanto resta per decidere, senza contatori ansiogeni. */
export function descriviScadenzaProposta(proposta: PropostaAzioneAurora, adesso: Date): string {
  const scade = Date.parse(proposta.scadeUtc);
  if (!Number.isFinite(scade)) return "";
  const minuti = Math.floor((scade - adesso.getTime()) / 60_000);
  if (minuti < 0) return "Proposta scaduta: chiedi ad Aurora di prepararla di nuovo.";
  if (minuti < 1) return "La proposta scade tra meno di un minuto.";
  return `Puoi decidere con calma: la proposta resta valida ${minuti} minut${minuti === 1 ? "o" : "i"}.`;
}

/** Il ripristino è offerto solo con operazione applicata e token di undo del server. */
export function puoRipristinare(esito: EsitoPropostaAurora | null): boolean {
  return esito !== null && STATI_APPLICATA.has(esito.stato) && Boolean(esito.tokenUndo);
}

/** Quando l'esito non consente più il ripristino, la card resta senza pulsanti: partita chiusa. */
export function esitoTerminale(esito: EsitoPropostaAurora | null): boolean {
  return esito !== null && !puoRipristinare(esito);
}

/** Stati che chiudono la partita come voleva l'utente: niente tono di avviso. */
const STATI_SERENI = new Set([
  "Applicata", "GiaApplicata", "Ripristinata", "GiaRipristinata", "Annullata",
]);

export function esitoRassicurante(esito: EsitoPropostaAurora): boolean {
  return STATI_SERENI.has(esito.stato);
}
