export const MASSIMO_CARATTERI_CONTESTO_AURORA = 4_000;

export interface ContestoAurora {
  testo: string;
  sezione: string;
  troncato: boolean;
}

/** Normalizza il testo scelto senza conservarlo: vive soltanto nello stato React. */
export function creaContestoAurora(testo: string, sezione: string): ContestoAurora | null {
  const pulito = testo.replaceAll("\0", "").trim();
  if (!pulito) return null;
  const troncato = pulito.length > MASSIMO_CARATTERI_CONTESTO_AURORA;
  return {
    testo: troncato ? pulito.slice(0, MASSIMO_CARATTERI_CONTESTO_AURORA) : pulito,
    sezione: sezione.replaceAll("\0", "").trim().slice(0, 80) || "Schermata PaghePro",
    troncato,
  };
}

/**
 * Legge soltanto una selezione esplicita dentro l'area contenuti autorizzata. Campi di input,
 * navigazione, chat e aree marcate private restano fuori dal perimetro.
 */
export function leggiTestoSelezionatoAurora(): string {
  const selezione = window.getSelection();
  // Alcuni browser possono rappresentare una selezione discontinua con più Range. Finché
  // non sono validate tutte separatamente, il confine fail-closed accetta una sola Range.
  if (!selezione || selezione.rangeCount !== 1 || selezione.isCollapsed) return "";

  const elementoDi = (nodo: Node | null): Element | null => {
    if (!nodo) return null;
    return nodo.nodeType === Node.ELEMENT_NODE ? nodo as Element : nodo.parentElement;
  };
  const inAreaConsentita = (elemento: Element | null) =>
    Boolean(elemento?.closest("[data-aurora-selectable]"))
    && !elemento?.closest("[data-aurora-private], input, textarea, select, #aurora-panel");

  if (!inAreaConsentita(elementoDi(selezione.anchorNode))
      || !inAreaConsentita(elementoDi(selezione.focusNode))) return "";

  // Inizio e fine possono essere leciti mentre la Range attraversa un campo o un nodo privato.
  // Controlliamo quindi ogni elemento vietato realmente intersecato dalla selezione.
  const range = selezione.getRangeAt(0);
  const radice = elementoDi(range.commonAncestorContainer);
  const vietati = radice?.querySelectorAll(
    "[data-aurora-private], input, textarea, select, #aurora-panel",
  ) ?? [];
  if ([...vietati].some((elemento) => range.intersectsNode(elemento))) return "";
  return selezione.toString();
}

export function anteprimaContestoAurora(contesto: ContestoAurora, massimo = 180): string {
  const riga = contesto.testo.replace(/\s+/g, " ").trim();
  return riga.length > massimo ? `${riga.slice(0, massimo).trimEnd()}…` : riga;
}
