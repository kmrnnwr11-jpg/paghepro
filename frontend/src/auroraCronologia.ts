import type { MessaggioChatAi } from "./api";

// Cronologia delle chat di Aurora: vive solo nel browser dell'utente, isolata per
// studio (tenant). Le conversazioni si salvano da sole a ogni turno e si cancellano
// soltanto a mano; oltre il limite viene scartata la più vecchia.

const PREFISSO_CONVERSAZIONI = "paghepro:aurora-conversazioni";
export const MASSIMO_CONVERSAZIONI = 20;
const MASSIMO_BOLLE = 80;
const MASSIMO_CARATTERI_BOLLA = 8_000;
const MASSIMO_MESSAGGI_STORIA = 24;

export interface BollaSalvata {
  chi: "utente" | "aurora";
  testo: string;
  citazione?: string;
}

export interface ConversazioneSalvata {
  id: string;
  /** Chiave cliente-mese a cui appartiene la conversazione. */
  scope: string;
  titolo: string;
  aggiornataIl: string;
  bolle: BollaSalvata[];
  storia: MessaggioChatAi[];
}

/** Il titolo è la prima domanda dell'utente, accorciata per l'elenco. */
export function titoloConversazione(bolle: BollaSalvata[]): string {
  const prima = bolle.find((b) => b.chi === "utente")?.testo.trim() ?? "";
  if (!prima) return "Conversazione";
  return prima.length > 60 ? `${prima.slice(0, 60)}…` : prima;
}

/**
 * Inserisce o aggiorna una conversazione nell'elenco (pura, testabile): la più
 * recente in testa, dimensioni limitate, mai due voci con lo stesso id.
 */
export function aggiornaElenco(
  elenco: ConversazioneSalvata[],
  conversazione: ConversazioneSalvata,
): ConversazioneSalvata[] {
  const ridotta: ConversazioneSalvata = {
    ...conversazione,
    titolo: titoloConversazione(conversazione.bolle),
    bolle: conversazione.bolle.slice(-MASSIMO_BOLLE).map((b) => ({
      chi: b.chi,
      testo: b.testo.slice(0, MASSIMO_CARATTERI_BOLLA),
      ...(b.citazione ? { citazione: b.citazione.slice(0, 240) } : {}),
    })),
    storia: conversazione.storia.slice(-MASSIMO_MESSAGGI_STORIA),
  };
  return [ridotta, ...elenco.filter((c) => c.id !== conversazione.id)]
    .slice(0, MASSIMO_CONVERSAZIONI);
}

// Stessa convenzione di aziendaCorrente.ts: la chiave è legata allo studio (tenant).
function chiave(): string {
  return `${PREFISSO_CONVERSAZIONI}:${localStorage.getItem("tenant_corrente") || "senza-tenant"}`;
}

export function elencoConversazioni(): ConversazioneSalvata[] {
  try {
    const grezzo = localStorage.getItem(chiave());
    if (!grezzo) return [];
    const lette = JSON.parse(grezzo) as unknown;
    if (!Array.isArray(lette)) return [];
    return (lette as ConversazioneSalvata[]).filter((c) =>
      Boolean(c) && typeof c.id === "string" && typeof c.scope === "string"
      && Array.isArray(c.bolle) && Array.isArray(c.storia));
  } catch {
    return [];
  }
}

export function salvaConversazione(conversazione: ConversazioneSalvata): void {
  // Una chat senza domande non merita una voce in cronologia.
  if (!conversazione.bolle.some((b) => b.chi === "utente")) return;
  try {
    localStorage.setItem(
      chiave(), JSON.stringify(aggiornaElenco(elencoConversazioni(), conversazione)));
  } catch {
    /* storage pieno o non disponibile: la chat resta comunque utilizzabile */
  }
}

export function eliminaConversazione(id: string): void {
  try {
    localStorage.setItem(
      chiave(), JSON.stringify(elencoConversazioni().filter((c) => c.id !== id)));
  } catch {
    /* storage non disponibile: nessuna voce da cancellare */
  }
}

/** L'ultima conversazione salvata per il cliente-mese indicato, se esiste. */
export function conversazionePerScope(scope: string): ConversazioneSalvata | null {
  return elencoConversazioni().find((c) => c.scope === scope) ?? null;
}
