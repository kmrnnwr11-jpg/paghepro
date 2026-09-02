import assert from "node:assert/strict";
import test from "node:test";
import {
  collegamentiImpostazioniPerRuolo,
  CoordinatoreSincronizzazioneImpostazioni,
  famigliaGraficaStile,
  hashSezioneImpostazioni,
  SEZIONE_IMPOSTAZIONI_INIZIALE,
  SEZIONI_IMPOSTAZIONI,
  sezioneImpostazioniDaHash,
  sezioneImpostazioniDaNavigazione,
} from "../src/impostazioni.ts";

test("le impostazioni sono organizzate in cinque categorie uniche", () => {
  assert.equal(SEZIONE_IMPOSTAZIONI_INIZIALE, "documenti");
  assert.deepEqual(
    SEZIONI_IMPOSTAZIONI.map((sezione) => sezione.id),
    ["documenti", "interfaccia", "aurora", "sicurezza", "studio"],
  );
  assert.equal(new Set(SEZIONI_IMPOSTAZIONI.map((sezione) => sezione.id)).size, 5);
});

test("ogni categoria è una pagina indirizzabile via hash", () => {
  assert.equal(sezioneImpostazioniDaHash("#impostazioni/sicurezza"), "sicurezza");
  assert.equal(sezioneImpostazioniDaHash("impostazioni/documenti"), "documenti");
  assert.equal(sezioneImpostazioniDaHash("#Impostazioni/Aurora"), "aurora");
  assert.equal(hashSezioneImpostazioni("interfaccia"), "impostazioni/interfaccia");
});

test("hash sconosciuti o estranei non aprono sezioni inventate", () => {
  assert.equal(sezioneImpostazioniDaHash("#impostazioni/segreta"), null);
  assert.equal(sezioneImpostazioniDaHash("#impostazioni"), null);
  assert.equal(sezioneImpostazioniDaHash("#aziende"), null);
  assert.equal(sezioneImpostazioniDaHash(""), null);
});

test("la navigazione base e una sotto-sezione ignota tornano a Documenti", () => {
  assert.equal(sezioneImpostazioniDaNavigazione("#impostazioni"), "documenti");
  assert.equal(sezioneImpostazioniDaNavigazione("#impostazioni/non-esiste"), "documenti");
  assert.equal(sezioneImpostazioniDaNavigazione("#aziende"), null);
});

test("i collegamenti di studio rispettano la stessa visibilità per ruolo del menu", () => {
  const operatore = collegamentiImpostazioniPerRuolo("operatore").map((voce) => voce.key);
  const admin = collegamentiImpostazioniPerRuolo("admin").map((voce) => voce.key);
  const superadmin = collegamentiImpostazioniPerRuolo("superadmin").map((voce) => voce.key);

  assert.equal(operatore.includes("audit"), false);
  assert.equal(operatore.includes("retention"), false);
  assert.equal(admin.includes("audit"), true);
  assert.equal(admin.includes("retention"), true);
  assert.equal(superadmin.includes("audit"), true);
  assert.equal(superadmin.includes("retention"), false);
  assert.equal(admin.includes("aziende"), true);
  assert.equal(admin.includes("scadenzario"), true);
});

test("l'anteprima riconosce le tre famiglie grafiche", () => {
  assert.equal(famigliaGraficaStile("classico-bordeaux"), "classico");
  assert.equal(famigliaGraficaStile("colori-petrolio"), "modulare");
  assert.equal(famigliaGraficaStile("editoriale-grafite"), "editoriale");
});

test("il badge resta in salvataggio finché tutte le preferenze hanno concluso", () => {
  const coordinatore = new CoordinatoreSincronizzazioneImpostazioni();
  assert.equal(coordinatore.inizia(), "salvataggio");
  assert.equal(coordinatore.inizia(), "salvataggio");
  assert.equal(coordinatore.completa("server"), "salvataggio");
  assert.equal(coordinatore.completa("server"), "server");
});

test("un fallback locale prevale nell'esito aggregato anche se termina per primo", () => {
  const coordinatore = new CoordinatoreSincronizzazioneImpostazioni();
  coordinatore.inizia();
  coordinatore.inizia();
  assert.equal(coordinatore.completa("locale"), "salvataggio");
  assert.equal(coordinatore.completa("server"), "locale");
});
