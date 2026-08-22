import type { CedolinoArchiviato, StatoCedolino } from "./api";

export type StatoCedolinoVisualizzato = StatoCedolino | "Sconosciuto";
export const ANNO_REDDITI_CU_UFFICIALE: number = 2025;

const statiNoti = new Set<StatoCedolino>([
  "PreWorkflow", "Bozza", "Controllata", "Validata", "Chiusa",
]);

/**
 * Semantica unica degli stati cedolino nella SPA. `PreWorkflow` identifica i documenti
 * archiviati prima dell'introduzione del flusso controllo -> validazione -> chiusura:
 * continuano ad alimentare lo storico, ma non diventano retroattivamente "chiusi".
 */
export function statoEffettivo(c: { stato?: unknown }): StatoCedolinoVisualizzato {
  return typeof c.stato === "string" && statiNoti.has(c.stato as StatoCedolino)
    ? c.stato as StatoCedolino
    : "Sconosciuto";
}

export function versioneWorkflow(c: { versione?: unknown }): number | null {
  return typeof c.versione === "number" && Number.isInteger(c.versione) && c.versione >= 1
    ? c.versione
    : null;
}

export function alimentaAdempimenti(stato: StatoCedolinoVisualizzato): boolean {
  return stato === "Chiusa" || stato === "PreWorkflow";
}

export function documentoDefinitivo(stato: StatoCedolinoVisualizzato): boolean {
  return stato === "Chiusa";
}

export function anniContabilizzabili(cedolini: CedolinoArchiviato[]): number[] {
  return [...new Set(
    cedolini
      .filter((c) => alimentaAdempimenti(statoEffettivo(c)))
      .map((c) => c.anno),
  )].sort((a, b) => b - a);
}

export const etichettaStatoCedolino: Record<StatoCedolinoVisualizzato, string> = {
  Sconosciuto: "Stato non disponibile",
  PreWorkflow: "Archivio precedente al workflow",
  Bozza: "Da completare",
  Controllata: "Dati congelati",
  Validata: "Validazione registrata",
  Chiusa: "Chiusa e non modificabile",
};

export const coloreStatoCedolino: Record<StatoCedolinoVisualizzato, string> = {
  Sconosciuto: "amber",
  PreWorkflow: "slate",
  Bozza: "amber",
  Controllata: "blue",
  Validata: "green",
  Chiusa: "slate",
};

export function etichettaDownloadCedolino(stato: StatoCedolinoVisualizzato): string {
  if (stato === "Chiusa") return "Scarica cedolino";
  if (stato === "PreWorkflow") return "Genera ricostruzione storica";
  if (stato === "Sconosciuto") return "Download non disponibile";
  return "Scarica anteprima";
}
