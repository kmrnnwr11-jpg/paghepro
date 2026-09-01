/**
 * Preferenze del browser: piccole scelte d'uso (non dati di dominio) che restano sul
 * dispositivo. Ogni lettura è protetta: senza storage valgono i default.
 */

export const STILI_BUSTA = ["classico", "colori", "editoriale"] as const;
export type StileBusta = (typeof STILI_BUSTA)[number];

const CHIAVE_STILE_BUSTA = "preferenza_stile_busta";

function storagePredefinito(): Storage | null {
  try {
    return typeof localStorage === "undefined" ? null : localStorage;
  } catch {
    return null;
  }
}

export function stileBustaPreferito(store: Storage | null = storagePredefinito()): StileBusta {
  try {
    const valore = store?.getItem(CHIAVE_STILE_BUSTA);
    return (STILI_BUSTA as readonly string[]).includes(valore ?? "")
      ? (valore as StileBusta)
      : "classico";
  } catch {
    return "classico";
  }
}

export function salvaStileBusta(valore: string, store: Storage | null = storagePredefinito()): void {
  if (!(STILI_BUSTA as readonly string[]).includes(valore)) return;
  try {
    store?.setItem(CHIAVE_STILE_BUSTA, valore);
  } catch { /* preferenza facoltativa */ }
}

/** Stessa chiave usata dal pannello Aurora: il pannello la rilegge alla prossima apertura. */
export const CHIAVE_RISPOSTE_BREVI_AURORA = "aurora_risposte_brevi";
