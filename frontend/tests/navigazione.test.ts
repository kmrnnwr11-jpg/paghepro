import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import {
  MONDI,
  cercaVoci,
  mondoDiSezione,
  tutteLeVoci,
  vociPerRuolo,
} from "../src/navigazione.ts";
import type { SectionKey } from "../src/homeStudio.ts";

/** Le SectionKey dichiarate nel tipo, lette dal sorgente: nessuna può sparire. */
function chiaviDichiarate(): SectionKey[] {
  const sorgente = readFileSync(new URL("../src/homeStudio.ts", import.meta.url), "utf8");
  const blocco = sorgente.slice(
    sorgente.indexOf("export type SectionKey ="),
    sorgente.indexOf(";", sorgente.indexOf("export type SectionKey =")),
  );
  return [...blocco.matchAll(/"([a-z0-9-]+)"/g)].map((m) => m[1] as SectionKey);
}

// La Home non è una voce di menu (è il pulsante PaghePro) e le tre schermate di
// dettaglio si aprono da dentro un elenco, non dal menu.
const FUORI_MENU = new Set<SectionKey>(["dashboard", "dipendente", "calcolo", "azienda"]);

test("ogni schermata dell'app sta in esattamente un mondo", () => {
  const dichiarate = chiaviDichiarate();
  assert.ok(dichiarate.length >= 30, `attese tutte le sezioni, lette ${dichiarate.length}`);

  const nelMenu = tutteLeVoci().map((v) => v.key);
  const duplicate = nelMenu.filter((k, i) => nelMenu.indexOf(k) !== i);
  assert.deepEqual(duplicate, [], "una schermata compare in due mondi");

  const mancanti = dichiarate.filter((k) => !FUORI_MENU.has(k) && !nelMenu.includes(k));
  assert.deepEqual(mancanti, [], "schermate diventate irraggiungibili dal menu");

  const inventate = nelMenu.filter((k) => !dichiarate.includes(k));
  assert.deepEqual(inventate, [], "voci di menu che puntano a schermate inesistenti");
});

test("nessun mondo è troppo lungo da leggere a colpo d'occhio", () => {
  assert.equal(MONDI.length, 5);
  for (const mondo of MONDI) {
    assert.ok(mondo.voci.length >= 5, `${mondo.titolo} è quasi vuoto`);
    assert.ok(mondo.voci.length <= 10, `${mondo.titolo} ha ${mondo.voci.length} voci: troppe`);
  }
});

test("ogni voce ha un nome e una spiegazione in italiano corrente", () => {
  for (const voce of tutteLeVoci()) {
    assert.ok(voce.label.length > 0 && voce.label.length <= 24, `etichetta fuori misura: ${voce.label}`);
    assert.ok(voce.detail.length > 10, `spiegazione troppo corta: ${voce.label}`);
  }
});

test("le schermate di dettaglio evidenziano il mondo giusto", () => {
  assert.equal(mondoDiSezione("presenze"), "paghe");
  assert.equal(mondoDiSezione("dipendente"), "paghe");
  assert.equal(mondoDiSezione("calcolo"), "paghe");
  assert.equal(mondoDiSezione("azienda"), "clienti");
  assert.equal(mondoDiSezione("f24"), "fisco");
  assert.equal(mondoDiSezione("dashboard"), null);
});

test("le voci riservate non compaiono a chi non ha il ruolo", () => {
  const studio = MONDI.find((m) => m.chiave === "studio")!;
  const chiavi = (ruolo: string | null) => vociPerRuolo(studio, ruolo).map((v) => v.key);

  assert.ok(!chiavi(null).includes("audit"));
  assert.ok(!chiavi(null).includes("retention"));
  assert.ok(!chiavi(null).includes("studi"));

  assert.ok(chiavi("admin").includes("audit"));
  assert.ok(chiavi("admin").includes("retention"));
  assert.ok(!chiavi("admin").includes("studi"));

  assert.ok(chiavi("superadmin").includes("studi"));
  assert.ok(chiavi("superadmin").includes("audit"));
  // Il backend respinge il super-admin sulla conservazione dati: non gliela offriamo.
  assert.ok(!chiavi("superadmin").includes("retention"));
});

test("la ricerca trova per nome, sinonimo e spiegazione", () => {
  assert.equal(cercaVoci("presenze")[0].key, "presenze");
  assert.equal(cercaVoci("busta")[0].key, "cedolini", "chi cerca «busta» vuole le buste paga");
  assert.equal(cercaVoci("cedolin")[0].key, "cedolini");
  assert.equal(cercaVoci("otd")[0].key, "agricoltura");
  assert.equal(cercaVoci("bracciante")[0].key, "agricoltura");
  assert.equal(cercaVoci("cud")[0].key, "cu");
  assert.equal(cercaVoci("730")[0].key, "redditi-pf");
  assert.equal(cercaVoci("durc")[0].key, "scadenzario");
  assert.equal(cercaVoci("visura")[0].key, "aziende");
  assert.equal(cercaVoci("sdi")[0].key, "fatture");
  assert.equal(cercaVoci("uniemens")[0].key, "adempimenti");
  assert.deepEqual(cercaVoci("zzzzz"), []);
});

test("la ricerca a vuoto elenca tutto quello che il ruolo può vedere", () => {
  // Nessun ruolo vede tutte le voci: il super-admin non ha la conservazione dati.
  const visibili = (ruolo: string | null) =>
    MONDI.flatMap((m) => vociPerRuolo(m, ruolo)).length;

  assert.equal(cercaVoci("", null).length, visibili(null));
  assert.equal(cercaVoci("", "admin").length, visibili("admin"));
  assert.equal(cercaVoci("", "superadmin").length, visibili("superadmin"));

  assert.ok(!cercaVoci("", null).some((v) => v.key === "studi"));
  assert.ok(cercaVoci("", "superadmin").some((v) => v.key === "studi"));
  assert.ok(visibili(null) < tutteLeVoci().length, "senza ruolo alcune voci restano nascoste");
});
