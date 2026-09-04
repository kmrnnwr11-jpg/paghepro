import type { SectionKey } from "./homeStudio";
import { MONDI, vociPerRuolo, type VoceMondo } from "./navigazione.ts";

export const SEZIONI_IMPOSTAZIONI = [
  {
    id: "documenti",
    gruppo: "Preferenze",
    titolo: "Documenti",
    descrizione: "Stile, font e piè di pagina",
  },
  {
    id: "interfaccia",
    gruppo: "Preferenze",
    titolo: "Interfaccia",
    descrizione: "Tema, colore, testo e densità",
  },
  {
    id: "aurora",
    gruppo: "Preferenze",
    titolo: "Aurora",
    descrizione: "Risposte e utilizzo assistente",
  },
  {
    id: "sicurezza",
    gruppo: "Account",
    titolo: "Sicurezza",
    descrizione: "Password e verifica in due passaggi",
  },
  {
    id: "studio",
    gruppo: "Studio",
    titolo: "Gestione studio",
    descrizione: "Dati, utenti e servizi collegati",
  },
] as const;

export type SezioneImpostazioni = (typeof SEZIONI_IMPOSTAZIONI)[number]["id"];
export const SEZIONE_IMPOSTAZIONI_INIZIALE: SezioneImpostazioni = "documenti";

/**
 * Ogni categoria delle impostazioni è una pagina indirizzabile: `#impostazioni/sicurezza`
 * apre direttamente quella sezione (link, cronologia del browser, tessere, Aurora).
 * Il solo `#impostazioni` resta la pagina iniziale; una sotto-sezione sconosciuta
 * non inventa nulla e ricade sull'iniziale.
 */
export function sezioneImpostazioniDaHash(hash: string): SezioneImpostazioni | null {
  const pulito = decodeURIComponent(hash.replace(/^#/, "")).trim().toLowerCase();
  if (!pulito.startsWith("impostazioni/")) return null;
  const sotto = pulito.slice("impostazioni/".length);
  return SEZIONI_IMPOSTAZIONI.some((sezione) => sezione.id === sotto)
    ? (sotto as SezioneImpostazioni)
    : null;
}

/** Per la pagina Impostazioni, base e sotto-sezioni sconosciute ricadono su Documenti. */
export function sezioneImpostazioniDaNavigazione(hash: string): SezioneImpostazioni | null {
  const trovata = sezioneImpostazioniDaHash(hash);
  if (trovata) return trovata;
  const pulito = hash.replace(/^#/, "").trim().toLowerCase();
  return pulito === "impostazioni" || pulito.startsWith("impostazioni/")
    ? SEZIONE_IMPOSTAZIONI_INIZIALE
    : null;
}

export function hashSezioneImpostazioni(sezione: SezioneImpostazioni): string {
  return `impostazioni/${sezione}`;
}

export type StatoSincronizzazioneImpostazioni = "salvataggio" | "server" | "locale";

/** Mantiene veritiero il badge globale quando preferenze diverse salvano in parallelo. */
export class CoordinatoreSincronizzazioneImpostazioni {
  private pendenti = 0;
  private fallbackLocale = false;

  inizia(): StatoSincronizzazioneImpostazioni {
    this.pendenti++;
    return "salvataggio";
  }

  completa(esito: "server" | "locale"): StatoSincronizzazioneImpostazioni {
    if (this.pendenti === 0) return esito;
    this.fallbackLocale ||= esito === "locale";
    this.pendenti--;
    if (this.pendenti > 0) return "salvataggio";
    const finale = this.fallbackLocale ? "locale" : "server";
    this.fallbackLocale = false;
    return finale;
  }
}

const CHIAVI_COLLEGAMENTI_STUDIO: readonly SectionKey[] = [
  "aziende",
  "collega-sdi",
  "utenti",
  "audit",
  "retention",
  "scadenzario",
];

/**
 * I collegamenti della pagina riusano nomi, descrizioni e soprattutto la stessa policy
 * per ruolo della navigazione principale: una scorciatoia non deve suggerire accessi negati.
 */
export function collegamentiImpostazioniPerRuolo(ruolo: string | null): VoceMondo[] {
  const visibili = new Map(
    MONDI.flatMap((mondo) => vociPerRuolo(mondo, ruolo))
      .map((voce) => [voce.key, voce] as const),
  );
  return CHIAVI_COLLEGAMENTI_STUDIO
    .map((chiave) => visibili.get(chiave))
    .filter((voce): voce is VoceMondo => voce !== undefined);
}

export function famigliaGraficaStile(stile: string): "classico" | "modulare" | "editoriale" {
  if (stile.startsWith("editoriale")) return "editoriale";
  if (stile.startsWith("colori")) return "modulare";
  return "classico";
}
