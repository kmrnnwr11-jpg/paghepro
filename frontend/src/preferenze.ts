/**
 * Preferenze del browser: piccole scelte d'uso (non dati di dominio) che restano sul
 * dispositivo. Ogni lettura è protetta: senza storage valgono i default.
 */

export const CATALOGO_STILI_BUSTA = [
  { id: "classico", nome: "Classico Ufficio", gruppo: "Classici da ufficio", accento: "#111111", sfondo: "#F5F5F5", descrizione: "Griglia tradizionale monocromatica, compatta e adatta alla stampa." },
  { id: "classico-blu", nome: "Classico Blu", gruppo: "Classici da ufficio", accento: "#12395E", sfondo: "#EEF4F8", descrizione: "Impostazione da ufficio con griglie blu e gerarchia più marcata." },
  { id: "classico-verde", nome: "Classico Verde", gruppo: "Classici da ufficio", accento: "#1F5B45", sfondo: "#EEF6F1", descrizione: "Griglia tradizionale con accento verde sobrio." },
  { id: "classico-bordeaux", nome: "Classico Bordeaux", gruppo: "Classici da ufficio", accento: "#7C2D3E", sfondo: "#F8F0F2", descrizione: "Griglia tradizionale con accento bordeaux istituzionale." },
  { id: "classico-antracite", nome: "Classico Antracite", gruppo: "Classici da ufficio", accento: "#263238", sfondo: "#F1F3F4", descrizione: "Versione ad alto contrasto per archivi e stampa in scala di grigi." },
  { id: "colori", nome: "Modulare Blu", gruppo: "Modulari a sezioni", accento: "#12395E", sfondo: "#F4F8FB", descrizione: "Sezioni numerate, sintesi iniziale e calendario orizzontale." },
  { id: "colori-verde", nome: "Modulare Verde", gruppo: "Modulari a sezioni", accento: "#1F5B45", sfondo: "#F3F8F5", descrizione: "Sezioni leggibili con palette verde professionale." },
  { id: "colori-bordeaux", nome: "Modulare Bordeaux", gruppo: "Modulari a sezioni", accento: "#7C2D3E", sfondo: "#FAF4F5", descrizione: "Sezioni leggibili con palette bordeaux calda." },
  { id: "colori-viola", nome: "Modulare Viola", gruppo: "Modulari a sezioni", accento: "#4C1D95", sfondo: "#F7F4FC", descrizione: "Sezioni leggibili con palette viola contemporanea." },
  { id: "colori-petrolio", nome: "Modulare Petrolio", gruppo: "Modulari a sezioni", accento: "#0F6660", sfondo: "#F1F8F7", descrizione: "Palette petrolio sobria, leggibile anche in bianco e nero." },
  { id: "editoriale", nome: "Editoriale Rame", gruppo: "Editoriali estesi", accento: "#9A4F2E", sfondo: "#F7F5F0", descrizione: "Prospetto esteso su più pagine con dettagli e guida di lettura." },
  { id: "editoriale-blu", nome: "Editoriale Blu", gruppo: "Editoriali estesi", accento: "#12395E", sfondo: "#F5F8FA", descrizione: "Prospetto esteso con tono blu autorevole." },
  { id: "editoriale-verde", nome: "Editoriale Verde", gruppo: "Editoriali estesi", accento: "#1F5B45", sfondo: "#F5F8F6", descrizione: "Prospetto esteso con tono verde sobrio." },
  { id: "editoriale-bordeaux", nome: "Editoriale Bordeaux", gruppo: "Editoriali estesi", accento: "#7C2D3E", sfondo: "#FAF6F6", descrizione: "Prospetto esteso con tono bordeaux istituzionale." },
  { id: "editoriale-grafite", nome: "Editoriale Grafite", gruppo: "Editoriali estesi", accento: "#334155", sfondo: "#F8FAFC", descrizione: "Prospetto esteso essenziale, pensato per lettura e archiviazione." },
] as const;

export type StileBusta = (typeof CATALOGO_STILI_BUSTA)[number]["id"];
export const STILI_BUSTA: readonly StileBusta[] = CATALOGO_STILI_BUSTA.map((stile) => stile.id);
export const GRUPPI_STILI_BUSTA = ["Classici da ufficio", "Modulari a sezioni", "Editoriali estesi"] as const;
export const STILE_BUSTA_PREDEFINITO: StileBusta = "colori";

export function dettaglioStileBusta(valore: string) {
  return CATALOGO_STILI_BUSTA.find((stile) => stile.id === valore) ?? CATALOGO_STILI_BUSTA[0];
}

function stileBustaValido(valore: string | null | undefined): valore is StileBusta {
  return CATALOGO_STILI_BUSTA.some((stile) => stile.id === valore);
}

const CHIAVE_STILE_BUSTA = "preferenza_stile_busta";
const PREFISSO_CACHE_ISOLATA = "paghepro_preferenza";
const SUFFISSO_CACHE_SPORCA = ":da_sincronizzare";

function storagePredefinito(): Storage | null {
  try {
    return typeof localStorage === "undefined" ? null : localStorage;
  } catch {
    return null;
  }
}

export function ambitoPreferenzeCorrente(store: Storage | null = storagePredefinito()): string | null {
  if (!store) return null;
  try {
    const tenantId = store.getItem("tenant_corrente")?.trim();
    const utenteId = store.getItem("utente_corrente")?.trim();
    return tenantId && utenteId ? `${tenantId}:${utenteId}` : null;
  } catch {
    return null;
  }
}

/**
 * Le preferenze del server appartengono a una coppia tenant+utente. Sul browser condiviso
 * devono avere lo stesso ambito: senza una sessione identificata non si legge né si scrive
 * alcuna cache globale. Uno Storage esplicito senza sessione mantiene le chiavi storiche solo
 * per test puri e pacchetti che lo iniettano deliberatamente.
 */
function chiaveCacheLocale(chiave: string, store: Storage | null): string | null {
  const chiaveStorica = CACHE_LOCALE[chiave];
  if (!store || !chiaveStorica) return null;
  try {
    const ambito = ambitoPreferenzeCorrente(store);
    if (ambito)
      return `${PREFISSO_CACHE_ISOLATA}:${ambito}:${chiave}`;
    return store === storagePredefinito() ? null : chiaveStorica;
  } catch {
    return null;
  }
}

export function stileBustaPreferito(store: Storage | null = storagePredefinito()): StileBusta {
  try {
    const chiave = chiaveCacheLocale("stile_busta", store);
    const valore = chiave ? store?.getItem(chiave) : null;
    if (valore === null || valore === undefined) return STILE_BUSTA_PREDEFINITO;
    // Un valore presente ma sconosciuto può provenire da una vecchia versione: il Classico
    // resta il ripiego di compatibilità, distinto dal default scelto per i nuovi account.
    return stileBustaValido(valore) ? valore : "classico";
  } catch {
    return STILE_BUSTA_PREDEFINITO;
  }
}

export function salvaStileBusta(valore: string, store: Storage | null = storagePredefinito()): void {
  if (!stileBustaValido(valore)) return;
  salvaPreferenzaOvunque("stile_busta", valore, store);
}

/** Stessa chiave usata dal pannello Aurora: il pannello la rilegge alla prossima apertura. */
export const CHIAVE_RISPOSTE_BREVI_AURORA = "aurora_risposte_brevi";

// ---- Preferenze sincronizzate col server -------------------------------------------------
// Il server è la fonte tra dispositivi; la copia locale fa da cache (e da unica fonte nel
// pacchetto offline). Le funzioni di rete sono lazy così i test node non caricano api.ts.

const CACHE_LOCALE: Record<string, string> = {
  stile_busta: CHIAVE_STILE_BUSTA,
  font_documenti: "preferenza_font_documenti",
  dicitura_studio: "preferenza_dicitura_studio",
  risposte_brevi_aurora: CHIAVE_RISPOSTE_BREVI_AURORA,
  scala_testo: "preferenza_scala_testo",
  font_ui: "preferenza_font_ui",
  densita: "preferenza_densita",
};

export type EsitoSincronizzazionePreferenze = "server" | "locale";
type InviaPreferenza = (chiave: string, valore: string) => Promise<void>;

// Modifiche rapide alla stessa preferenza devono arrivare al server nello stesso ordine
// in cui la persona le ha fatte: l'ultima scelta resta sempre l'ultima scritta.
const codeSalvataggio = new Map<string, Promise<void>>();

function chiaveCacheSporca(cache: string): string {
  return `${cache}${SUFFISSO_CACHE_SPORCA}`;
}

function cacheAncoraDellaStessaSessione(
  chiave: string,
  cacheAttesa: string,
  store: Storage | null,
): boolean {
  return chiaveCacheLocale(chiave, store) === cacheAttesa;
}

export function preferenzaLocale(
  chiave: string,
  predefinito = "",
  store: Storage | null = storagePredefinito(),
): string {
  try {
    const k = chiaveCacheLocale(chiave, store);
    const v = k ? store?.getItem(k) : null;
    if (chiave === "risposte_brevi_aurora") return v === "1" ? "1" : "0";
    return v ?? predefinito;
  } catch {
    return predefinito;
  }
}

/** Indica se questa sessione conserva almeno una scelta locale non ancora confermata dal server. */
export function haPreferenzeDaSincronizzare(
  store: Storage | null = storagePredefinito(),
): boolean {
  if (!store) return false;
  return Object.keys(CACHE_LOCALE).some((chiave) => {
    const cache = chiaveCacheLocale(chiave, store);
    try {
      return !!cache && store.getItem(chiaveCacheSporca(cache)) === "1";
    } catch {
      return false;
    }
  });
}

async function salvaSulServer(chiave: string, valore: string): Promise<void> {
  const { api } = await import("./api");
  await api.salvaPreferenza(chiave, valore);
}

export async function salvaPreferenzaConEsito(
  chiave: string,
  valore: string,
  store: Storage | null = storagePredefinito(),
  inviaPreferenza?: InviaPreferenza,
): Promise<EsitoSincronizzazionePreferenze> {
  const cache = chiaveCacheLocale(chiave, store);
  if (!cache) return "locale";

  try {
    store?.setItem(cache, valore);
  } catch { /* la sincronizzazione server può riuscire anche senza cache browser */ }

  const invia = inviaPreferenza
    ?? (typeof window !== "undefined" && store === storagePredefinito() ? salvaSulServer : null);
  if (!invia) return "locale";
  try {
    store?.setItem(chiaveCacheSporca(cache), "1");
  } catch { /* il marcatore abilita il retry, ma la rete può funzionare anche senza storage */ }

  const precedente = codeSalvataggio.get(cache) ?? Promise.resolve();
  const corrente = precedente
    .catch(() => undefined)
    .then(() => {
      // Una scrittura accodata non deve mai usare la sessione aperta successivamente
      // nello stesso browser. Il valore resta nella cache isolata della sessione originaria.
      if (!cacheAncoraDellaStessaSessione(chiave, cache, store))
        throw new Error("Sessione cambiata durante il salvataggio della preferenza.");
      return invia(chiave, valore);
    });
  codeSalvataggio.set(cache, corrente);

  try {
    await corrente;
    // Una risposta precedente non rende pulita una scelta più recente ancora in coda.
    if (codeSalvataggio.get(cache) === corrente && store?.getItem(cache) === valore)
      store.removeItem(chiaveCacheSporca(cache));
    return "server";
  } catch {
    return "locale";
  } finally {
    if (codeSalvataggio.get(cache) === corrente) codeSalvataggio.delete(cache);
  }
}

export function salvaPreferenzaOvunque(
  chiave: string,
  valore: string,
  store: Storage | null = storagePredefinito(),
): void {
  void salvaPreferenzaConEsito(chiave, valore, store);
}

/** Applica una risposta completa del server: una chiave assente elimina l'eventuale valore stale. */
export function aggiornaCachePreferenzeDalServer(
  dalServer: Record<string, string | undefined>,
  store: Storage | null = storagePredefinito(),
  ambitoAtteso?: string | null,
): boolean {
  if (!store || (ambitoAtteso !== undefined && ambitoPreferenzeCorrente(store) !== ambitoAtteso))
    return false;
  for (const [chiave, chiaveStorica] of Object.entries(CACHE_LOCALE)) {
    const cache = chiaveCacheLocale(chiave, store);
    if (!cache) continue;
    const valore = dalServer[chiave];
    try {
      // Se una modifica offline attende ancora il server, la risposta remota non deve
      // cancellarla. Verrà ritentata subito dopo il caricamento delle altre preferenze.
      if (store.getItem(chiaveCacheSporca(cache)) !== "1") {
        if (valore === undefined) store.removeItem(cache);
        else store.setItem(cache, valore);
      }
      // Non lasciare attiva la vecchia cache globale dopo l'aggiornamento dell'app.
      if (cache !== chiaveStorica) store.removeItem(chiaveStorica);
    } catch { /* cache facoltativa */ }
  }
  return true;
}

/** Da chiamare prima di rimuovere tenant e utente della sessione corrente. */
export function pulisciCachePreferenzeCorrenti(
  store: Storage | null = storagePredefinito(),
): void {
  if (!store) return;
  for (const [chiave, chiaveStorica] of Object.entries(CACHE_LOCALE)) {
    const cache = chiaveCacheLocale(chiave, store);
    try {
      if (cache) store.removeItem(cache);
      if (cache) store.removeItem(chiaveCacheSporca(cache));
      store.removeItem(chiaveStorica);
      store.removeItem(chiaveCacheSporca(chiaveStorica));
    } catch { /* pulizia best effort */ }
  }
}

export async function sincronizzaPreferenzeDalServer(): Promise<EsitoSincronizzazionePreferenze> {
  try {
    const store = storagePredefinito();
    const ambitoAtteso = ambitoPreferenzeCorrente(store);
    const { api } = await import("./api");
    const dal_server = await api.preferenze();
    // Una risposta appartenente alla sessione precedente non deve mai entrare nella
    // cache della persona che ha effettuato l'accesso nel frattempo.
    if (!aggiornaCachePreferenzeDalServer(dal_server, store, ambitoAtteso)) return "locale";
    const daRitentare = Object.keys(CACHE_LOCALE).flatMap((chiave) => {
      const cache = chiaveCacheLocale(chiave, store);
      if (!cache || store?.getItem(chiaveCacheSporca(cache)) !== "1") return [];
      const valore = store.getItem(cache);
      return valore === null ? [] : [{ chiave, valore }];
    });
    const esiti = await Promise.all(daRitentare.map(({ chiave, valore }) =>
      salvaPreferenzaConEsito(chiave, valore, store, async (c, v) => {
        await api.salvaPreferenza(c, v);
      })));
    applicaAspetto();
    return esiti.every((esito) => esito === "server") ? "server" : "locale";
  } catch {
    // Offline o non autenticato: restano valide le copie locali isolate per tenant e utente.
    return "locale";
  }
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
