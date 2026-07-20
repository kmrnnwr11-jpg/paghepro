import assert from "node:assert/strict";
import test from "node:test";
import { validaAppIdStore, validaConfigurazioneMobile } from "../scripts/mobile-config.mjs";

test("rifiuta un'API mobile non HTTPS", () => {
  assert.throws(
    () => validaConfigurazioneMobile("http://api.example.com", "it.azienda.paghepro"),
    /HTTPS reale/,
  );
});

test("rifiuta domini fittizi e identificativi di sviluppo", () => {
  assert.throws(
    () => validaConfigurazioneMobile("https://api.paghepro.invalid", "it.azienda.paghepro"),
    /\.invalid/,
  );
  assert.throws(
    () => validaConfigurazioneMobile("https://api.example.com", "it.paghepro.app.dev"),
    /termina con \.dev/,
  );
});

test("normalizza una configurazione pronta per gli store", () => {
  assert.deepEqual(
    validaConfigurazioneMobile("https://api.example.com/percorso", "IT.Azienda.PaghePro"),
    { apiOrigin: "https://api.example.com", appId: "it.azienda.paghepro" },
  );
});

test("valida l'identificativo store indipendentemente dall'URL API", () => {
  assert.equal(validaAppIdStore("IT.Azienda.PaghePro"), "it.azienda.paghepro");
  assert.throws(() => validaAppIdStore("it.paghepro.app.dev"), /\.dev/);
});
