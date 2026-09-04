import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  ACCENTO_PREDEFINITO,
  accentoInterfacciaPreferito,
  ambitoPreferenzeCorrente,
  aggiornaCachePreferenzeDalServer,
  applicaAspetto,
  CATALOGO_ACCENTI,
  CATALOGO_STILI_BUSTA,
  CATALOGO_TEMI,
  haPreferenzeDaSincronizzare,
  preferenzaLocale,
  pulisciCachePreferenzeCorrenti,
  salvaPreferenzaConEsito,
  salvaPreferenzaOvunque,
  salvaStileBusta,
  risolviTema,
  STILE_BUSTA_PREDEFINITO,
  stileBustaPreferito,
  TEMA_PREDEFINITO,
  temaInterfacciaPreferito,
} from "../src/preferenze.ts";

function memoria(): Storage {
  const dati = new Map<string, string>();
  return {
    getItem: (k: string) => dati.get(k) ?? null,
    setItem: (k: string, v: string) => void dati.set(k, v),
    removeItem: (k: string) => void dati.delete(k),
    clear: () => dati.clear(),
    key: () => null,
    get length() { return dati.size; },
  } as Storage;
}

test("senza preferenza salvata vale Modulare Blu", () => {
  assert.equal(STILE_BUSTA_PREDEFINITO, "colori");
  assert.equal(stileBustaPreferito(memoria()), "colori");
  assert.equal(stileBustaPreferito(null), "colori");
});

test("la preferenza salvata viene riletta e un valore corrotto ricade sul Classico", () => {
  const store = memoria();
  salvaStileBusta("editoriale-blu", store);
  assert.equal(stileBustaPreferito(store), "editoriale-blu");
  salvaStileBusta("stile-inventato", store);   // ignorato
  assert.equal(stileBustaPreferito(store), "editoriale-blu");
  store.setItem("preferenza_stile_busta", "valore-corrotto");
  assert.equal(stileBustaPreferito(store), "classico");
  salvaStileBusta("classico", store);
  assert.equal(stileBustaPreferito(store), "classico");
});

test("il catalogo espone 15 stili unici e ciascuno può essere salvato", () => {
  assert.equal(CATALOGO_STILI_BUSTA.length, 15);
  assert.equal(new Set(CATALOGO_STILI_BUSTA.map((stile) => stile.id)).size, 15);
  const store = memoria();
  for (const stile of CATALOGO_STILI_BUSTA) {
    salvaStileBusta(stile.id, store);
    assert.equal(stileBustaPreferito(store), stile.id);
  }
});

test("le chiavi del catalogo frontend restano allineate alla whitelist backend", () => {
  const sorgenteBackend = readFileSync(new URL(
    "../../backend/src/Paghe.Documenti/CatalogoStiliCedolino.cs",
    import.meta.url,
  ), "utf8");
  const chiaviBackend = [...sorgenteBackend.matchAll(
    /new\(StileCedolino\.[A-Za-z]+,\s*"([^"]+)"/g,
  )].map((corrispondenza) => corrispondenza[1]);
  const defaultBackend = sorgenteBackend.match(/ChiavePredefinita\s*=\s*"([^"]+)"/)?.[1];
  assert.deepEqual(chiaviBackend, CATALOGO_STILI_BUSTA.map((stile) => stile.id));
  assert.equal(defaultBackend, STILE_BUSTA_PREDEFINITO);
});

test("la cache delle preferenze è isolata per tenant e utente e non conserva valori assenti", () => {
  const store = memoria();
  store.setItem("tenant_corrente", "tenant-a");
  store.setItem("utente_corrente", "utente-a");
  aggiornaCachePreferenzeDalServer({
    stile_busta: "editoriale-blu",
    dicitura_studio: "Studio Alfa",
  }, store);
  assert.equal(stileBustaPreferito(store), "editoriale-blu");
  assert.equal(preferenzaLocale("dicitura_studio", "", store), "Studio Alfa");

  store.setItem("utente_corrente", "utente-b");
  assert.equal(stileBustaPreferito(store), "colori");
  store.setItem("tenant_corrente", "tenant-b");
  store.setItem("utente_corrente", "utente-a");
  assert.equal(stileBustaPreferito(store), "colori");

  store.setItem("tenant_corrente", "tenant-b");
  store.setItem("utente_corrente", "utente-b");
  assert.equal(stileBustaPreferito(store), "colori");
  assert.equal(preferenzaLocale("dicitura_studio", "", store), "");

  salvaStileBusta("colori-viola", store);
  salvaPreferenzaOvunque("dicitura_studio", "valore stale", store);
  aggiornaCachePreferenzeDalServer({}, store);
  assert.equal(stileBustaPreferito(store), "colori");
  assert.equal(preferenzaLocale("dicitura_studio", "", store), "");

  aggiornaCachePreferenzeDalServer({ stile_busta: "classico-verde" }, store);
  pulisciCachePreferenzeCorrenti(store);
  assert.equal(stileBustaPreferito(store), "colori");
});

test("una risposta GET della sessione precedente non entra nella cache della nuova", () => {
  const store = memoria();
  store.setItem("tenant_corrente", "tenant-a");
  store.setItem("utente_corrente", "utente-a");
  const ambitoA = ambitoPreferenzeCorrente(store);

  store.setItem("tenant_corrente", "tenant-b");
  store.setItem("utente_corrente", "utente-b");
  assert.equal(aggiornaCachePreferenzeDalServer(
    { dicitura_studio: "Dato di A" }, store, ambitoA,
  ), false);
  assert.equal(preferenzaLocale("dicitura_studio", "", store), "");
});

test("applicaAspetto non esplode fuori dal browser (test e script)", () => {
  assert.doesNotThrow(() => applicaAspetto());
});

test("i salvataggi rapidi della stessa preferenza arrivano al server in ordine", async () => {
  const store = memoria();
  const ordine: string[] = [];
  let richiesteAttive = 0;
  let massimoRichiesteAttive = 0;
  const invia = async (_chiave: string, valore: string) => {
    richiesteAttive++;
    massimoRichiesteAttive = Math.max(massimoRichiesteAttive, richiesteAttive);
    await new Promise((risolvi) => setTimeout(risolvi, 4));
    ordine.push(valore);
    richiesteAttive--;
  };

  const esiti = await Promise.all([
    salvaPreferenzaConEsito("scala_testo", "normale", store, invia),
    salvaPreferenzaConEsito("scala_testo", "grande", store, invia),
    salvaPreferenzaConEsito("scala_testo", "molto-grande", store, invia),
  ]);

  assert.deepEqual(ordine, ["normale", "grande", "molto-grande"]);
  assert.equal(massimoRichiesteAttive, 1);
  assert.deepEqual(esiti, ["server", "server", "server"]);
  assert.equal(preferenzaLocale("scala_testo", "", store), "molto-grande");
});

test("un errore server conserva la preferenza locale senza dichiararla sincronizzata", async () => {
  const store = memoria();
  const esito = await salvaPreferenzaConEsito(
    "densita",
    "compatta",
    store,
    async () => { throw new Error("offline"); },
  );

  assert.equal(esito, "locale");
  assert.equal(preferenzaLocale("densita", "", store), "compatta");
});

test("una scrittura accodata non viene inviata dopo il cambio di sessione", async () => {
  const store = memoria();
  store.setItem("tenant_corrente", "tenant-a");
  store.setItem("utente_corrente", "utente-a");
  const inviati: string[] = [];
  let sbloccaPrima: (() => void) | undefined;
  const attesaPrima = new Promise<void>((risolvi) => { sbloccaPrima = risolvi; });
  const invia = async (_chiave: string, valore: string) => {
    inviati.push(valore);
    if (valore === "grande") await attesaPrima;
  };

  const prima = salvaPreferenzaConEsito("scala_testo", "grande", store, invia);
  const seconda = salvaPreferenzaConEsito("scala_testo", "molto-grande", store, invia);
  await new Promise((risolvi) => setTimeout(risolvi, 0));
  store.setItem("tenant_corrente", "tenant-b");
  store.setItem("utente_corrente", "utente-b");
  sbloccaPrima?.();

  assert.equal(await prima, "server");
  assert.equal(await seconda, "locale");
  assert.deepEqual(inviati, ["grande"]);
});

test("una modifica offline non viene sovrascritta e diventa pulita dopo il retry", async () => {
  const store = memoria();
  store.setItem("tenant_corrente", "tenant-a");
  store.setItem("utente_corrente", "utente-a");
  assert.equal(await salvaPreferenzaConEsito(
    "densita", "compatta", store, async () => { throw new Error("offline"); },
  ), "locale");
  assert.equal(haPreferenzeDaSincronizzare(store), true);

  // Il successo successivo di un'altra chiave non può nascondere il fallback rimasto.
  assert.equal(await salvaPreferenzaConEsito("font_ui", "lato", store, async () => undefined), "server");
  assert.equal(haPreferenzeDaSincronizzare(store), true);

  aggiornaCachePreferenzeDalServer({ densita: "comoda" }, store);
  assert.equal(preferenzaLocale("densita", "", store), "compatta");

  assert.equal(await salvaPreferenzaConEsito("densita", "compatta", store, async () => undefined), "server");
  assert.equal(haPreferenzeDaSincronizzare(store), false);
  aggiornaCachePreferenzeDalServer({ densita: "comoda" }, store);
  assert.equal(preferenzaLocale("densita", "", store), "comoda");
});

// ---- Tema e accento dell'interfaccia --------------------------------------------------

test("il catalogo dei temi e degli accenti resta allineato alla whitelist backend", () => {
  const sorgenteBackend = readFileSync(new URL(
    "../../backend/src/Paghe.Persistence/PreferenzeUtenteService.cs",
    import.meta.url,
  ), "utf8");
  const lista = (nome: string) => [...(sorgenteBackend.match(
    new RegExp(`${nome} =\\s*\\[([^\\]]+)\\]`),
  )?.[1] ?? "").matchAll(/"([^"]+)"/g)].map((m) => m[1]);
  assert.deepEqual(lista("TemiInterfaccia"), CATALOGO_TEMI.map((tema) => tema.id));
  assert.deepEqual(lista("AccentiInterfaccia"), CATALOGO_ACCENTI.map((accento) => accento.id));
  assert.equal(TEMA_PREDEFINITO, "chiaro");
  assert.equal(ACCENTO_PREDEFINITO, "salvia");
});

test("automatico segue il dispositivo, chiaro e scuro restano fissi", () => {
  assert.equal(risolviTema("automatico", true), "scuro");
  assert.equal(risolviTema("automatico", false), "chiaro");
  assert.equal(risolviTema("scuro", false), "scuro");
  assert.equal(risolviTema("chiaro", true), "chiaro");
});

test("tema e accento si rileggono dalla cache della sessione e un valore corrotto torna al default", () => {
  const store = memoria();
  store.setItem("tenant_corrente", "tenant-a");
  store.setItem("utente_corrente", "utente-a");
  assert.equal(temaInterfacciaPreferito(store), "chiaro");
  assert.equal(accentoInterfacciaPreferito(store), "salvia");
  aggiornaCachePreferenzeDalServer({ tema_interfaccia: "scuro", accento_interfaccia: "bordeaux" }, store);
  assert.equal(temaInterfacciaPreferito(store), "scuro");
  assert.equal(accentoInterfacciaPreferito(store), "bordeaux");
  store.setItem("paghepro_preferenza:tenant-a:utente-a:tema_interfaccia", "notte");
  store.setItem("paghepro_preferenza:tenant-a:utente-a:accento_interfaccia", "#ff0000");
  assert.equal(temaInterfacciaPreferito(store), "chiaro");
  assert.equal(accentoInterfacciaPreferito(store), "salvia");
});

test("senza sessione vale l'ultimo aspetto ricordato sul dispositivo, mai la cache di un altro utente", () => {
  const store = memoria();
  assert.equal(temaInterfacciaPreferito(store), "chiaro");
  store.setItem("paghepro_aspetto_dispositivo", JSON.stringify({ tema: "scuro", accento: "oceano" }));
  assert.equal(temaInterfacciaPreferito(store), "scuro");
  assert.equal(accentoInterfacciaPreferito(store), "oceano");
  store.setItem("paghepro_aspetto_dispositivo", "{non-json");
  assert.equal(temaInterfacciaPreferito(store), "chiaro");
  // Con una sessione attiva la cache isolata prevale sul promemoria del dispositivo.
  store.setItem("paghepro_aspetto_dispositivo", JSON.stringify({ tema: "scuro", accento: "oceano" }));
  store.setItem("tenant_corrente", "tenant-b");
  store.setItem("utente_corrente", "utente-b");
  assert.equal(temaInterfacciaPreferito(store), "chiaro");
  assert.equal(accentoInterfacciaPreferito(store), "salvia");
});
