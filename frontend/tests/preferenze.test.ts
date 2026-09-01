import assert from "node:assert/strict";
import test from "node:test";
import { applicaAspetto, salvaStileBusta, stileBustaPreferito } from "../src/preferenze.ts";

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

test("senza preferenza salvata vale lo stile classico", () => {
  assert.equal(stileBustaPreferito(memoria()), "classico");
  assert.equal(stileBustaPreferito(null), "classico");
});

test("la preferenza salvata viene riletta, i valori sconosciuti vengono ignorati", () => {
  const store = memoria();
  salvaStileBusta("colori", store);
  assert.equal(stileBustaPreferito(store), "colori");
  salvaStileBusta("stile-inventato", store);   // ignorato
  assert.equal(stileBustaPreferito(store), "colori");
  store.setItem("preferenza_stile_busta", "valore-corrotto");
  assert.equal(stileBustaPreferito(store), "classico");
});

test("applicaAspetto non esplode fuori dal browser (test e script)", () => {
  assert.doesNotThrow(() => applicaAspetto());
});
