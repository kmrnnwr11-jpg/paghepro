import test from "node:test";
import assert from "node:assert/strict";
import {
  apriSchedaAurora,
  chiaveSchedeAurora,
  chiudiSchedaAurora,
  eliminaCronologiaAuroraLegacy,
  leggiSchedeAurora,
  salvaSchedeAurora,
} from "../src/auroraWorkspace.ts";

class Memoria {
  dati = new Map<string, string>();
  get length() { return this.dati.size; }
  key(indice: number) { return [...this.dati.keys()][indice] ?? null; }
  getItem(chiave: string) { return this.dati.get(chiave) ?? null; }
  setItem(chiave: string, valore: string) { this.dati.set(chiave, valore); }
  removeItem(chiave: string) { this.dati.delete(chiave); }
}

test("le schede separano la chiave per tenant e utente e salvano soltanto ID", () => {
    const memoria = new Memoria();
    memoria.setItem("tenant_corrente", "tenant-a");
    memoria.setItem("utente_corrente", "utente-a");
    const chiave = chiaveSchedeAurora(memoria)!;
    salvaSchedeAurora({ ids: ["chat-1", "chat-2"], attiva: "chat-2" }, memoria);

    assert.match(chiave, /tenant-a:utente-a/);
    assert.equal(memoria.getItem(chiave), '{"ids":["chat-1","chat-2"],"attiva":"chat-2"}');
    assert.doesNotMatch(memoria.getItem(chiave) ?? "", /testo/);

    memoria.setItem("utente_corrente", "utente-b");
    assert.deepEqual(leggiSchedeAurora(memoria), { ids: [], attiva: null });
});

test("apre, attiva e chiude schede senza eliminare conversazioni", () => {
    const aperte = apriSchedaAurora({ ids: ["a"], attiva: "a" }, "b");
    assert.deepEqual(aperte, { ids: ["a", "b"], attiva: "b" });
    assert.deepEqual(chiudiSchedaAurora(aperte, "b"), { ids: ["a"], attiva: "a" });
});

test("ignora payload locali corrotti", () => {
    const memoria = new Memoria();
    memoria.setItem("tenant_corrente", "t");
    memoria.setItem("utente_corrente", "u");
    memoria.setItem(chiaveSchedeAurora(memoria)!, "{non-json");
    assert.deepEqual(leggiSchedeAurora(memoria), { ids: [], attiva: null });
});

test("bonifica il transcript legacy tenant-only senza importarlo", () => {
  const memoria = new Memoria();
  memoria.setItem("tenant_corrente", "tenant-a");
  memoria.setItem("utente_corrente", "utente-a");
  memoria.setItem("paghepro:aurora-conversazioni:tenant-a", "TESTO_IN_CHIARO");
  memoria.setItem("paghepro:aurora-conversazioni:tenant-vecchio", "ALTRO_TESTO_IN_CHIARO");
  memoria.setItem("preferenza-innocua", "resta");

  eliminaCronologiaAuroraLegacy(memoria);

  assert.equal(memoria.getItem("paghepro:aurora-conversazioni:tenant-a"), null);
  assert.equal(memoria.getItem("paghepro:aurora-conversazioni:tenant-vecchio"), null);
  assert.equal(memoria.getItem("preferenza-innocua"), "resta");
});
