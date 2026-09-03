// Riassunto dei valori della schermata corrente che una pagina può pubblicare per Aurora.
// Canale volontario: solo le pagine che chiamano pubblicaContestoPagina lo alimentano e
// vanno ripulite all'uscita (pubblicaContestoPagina(null)). Il server lo tratta comunque
// come dato non fidato: lo taglia, lo filtra dai segreti e non lo esegue mai.
let contesto: string | null = null;

const MASSIMO_CARATTERI = 1500;

export function pubblicaContestoPagina(testo: string | null): void {
  const pulito = testo?.trim() ?? "";
  contesto = pulito ? pulito.slice(0, MASSIMO_CARATTERI) : null;
}

export function contestoPaginaCorrente(): string | null {
  return contesto;
}
