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
  if (typeof window !== "undefined" && store === storagePredefinito())
    void salvaSulServer("stile_busta", valore);
}

/** Stessa chiave usata dal pannello Aurora: il pannello la rilegge alla prossima apertura. */
export const CHIAVE_RISPOSTE_BREVI_AURORA = "aurora_risposte_brevi";

// ---- Preferenze sincronizzate col server -------------------------------------------------
// Il server è la fonte tra dispositivi; la copia locale fa da cache (e da unica fonte nel
// pacchetto offline). Le funzioni di rete sono lazy così i test node non caricano api.ts.

const CACHE_LOCALE: Record<string, string> = {
  stile_busta: "preferenza_stile_busta",
  font_documenti: "preferenza_font_documenti",
  dicitura_studio: "preferenza_dicitura_studio",
  risposte_brevi_aurora: CHIAVE_RISPOSTE_BREVI_AURORA,
  scala_testo: "preferenza_scala_testo",
  font_ui: "preferenza_font_ui",
  densita: "preferenza_densita",
};

export function preferenzaLocale(chiave: string, predefinito = ""): string {
  try {
    const k = CACHE_LOCALE[chiave];
    const v = k ? localStorage.getItem(k) : null;
    if (chiave === "risposte_brevi_aurora") return v === "1" ? "1" : "0";
    return v ?? predefinito;
  } catch {
    return predefinito;
  }
}

async function salvaSulServer(chiave: string, valore: string): Promise<void> {
  try {
    const { api } = await import("./api");
    await api.salvaPreferenza(chiave, valore);
  } catch { /* offline o non autenticato: resta la copia locale */ }
}

export function salvaPreferenzaOvunque(chiave: string, valore: string): void {
  try {
    const k = CACHE_LOCALE[chiave];
    if (k) localStorage.setItem(k, valore);
  } catch { /* cache facoltativa */ }
  void salvaSulServer(chiave, valore);
}

export async function sincronizzaPreferenzeDalServer(): Promise<void> {
  try {
    const { api } = await import("./api");
    const dal_server = await api.preferenze();
    for (const [chiave, cache] of Object.entries(CACHE_LOCALE)) {
      const valore = dal_server[chiave];
      try {
        if (valore !== undefined) localStorage.setItem(cache, valore);
      } catch { /* cache facoltativa */ }
    }
    applicaAspetto();
  } catch { /* offline: valgono le copie locali */ }
}

/** Applica le preferenze d'aspetto (taglia, font, densità) alle classi del body. */
export function applicaAspetto(): void {
  if (typeof document === "undefined") return;
  const body = document.body;
  const scala = preferenzaLocale("scala_testo", "normale");
  body.classList.toggle("scala-grande", scala === "grande");
  body.classList.toggle("scala-molto-grande", scala === "molto-grande");
  body.classList.toggle("font-lato", preferenzaLocale("font_ui", "sistema") === "lato");
  body.classList.toggle("densita-compatta", preferenzaLocale("densita", "comoda") === "compatta");
}
