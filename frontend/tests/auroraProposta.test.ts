import test from "node:test";
import assert from "node:assert/strict";
import {
  descriviScadenzaProposta,
  esitoPropostaDaErrore,
  esitoRassicurante,
  esitoTerminale,
  propostaScaduta,
  puoRipristinare,
} from "../src/auroraProposta.ts";
import type { EsitoPropostaAurora, PropostaAzioneAurora } from "../src/api.ts";

/** Stessa forma dell'ApiError del client: Error con status e corpo della risposta. */
function erroreApi(messaggio: string, status: number, corpo: Record<string, unknown>) {
  return Object.assign(new Error(messaggio), { status, corpo });
}

const proposta: PropostaAzioneAurora = {
  token: "v2c.token.firmato",
  strumento: "compila_presenze",
  titolo: "Compila le presenze di agosto",
  diff: [{ etichetta: "Straordinari", da: "0", a: "4" }],
  resaUi: "diff",
  scadeUtc: "2026-08-23T12:10:00Z",
  propostaId: "p1",
  confermabile: true,
};

const esito = (parziale: Partial<EsitoPropostaAurora>): EsitoPropostaAurora => ({
  propostaId: "p1",
  stato: "Applicata",
  messaggio: "Operazione applicata.",
  esitoJson: null,
  tokenUndo: null,
  ...parziale,
});

test("l'esito del protocollo viene recuperato anche dal corpo di un 409/410", () => {
  const errore = erroreApi("Errore 409", 409, {
    propostaId: "p1",
    stato: "Negata",
    messaggio: "Mandato non più spendibile.",
    esitoJson: null,
    tokenUndo: null,
  });
  const letto = esitoPropostaDaErrore(errore);
  assert.equal(letto.stato, "Negata");
  assert.equal(letto.messaggio, "Mandato non più spendibile.");
  assert.equal(letto.tokenUndo, null);
});

test("un errore senza esito strutturato diventa uno stato Errore col messaggio disponibile", () => {
  const daApi = esitoPropostaDaErrore(erroreApi("Token della conferma assente o alterato.", 400, {
    errore: "Token della conferma assente o alterato.",
  }));
  assert.equal(daApi.stato, "Errore");
  assert.equal(daApi.messaggio, "Token della conferma assente o alterato.");
  assert.equal(daApi.tokenUndo, null);

  const daRete = esitoPropostaDaErrore(new TypeError("Failed to fetch"));
  assert.equal(daRete.stato, "Errore");

  // Un corpo ostile non può inventare un token di undo né uno stato applicato.
  const ostile = esitoPropostaDaErrore(erroreApi("x", 409, { stato: "Applicata", tokenUndo: 42 }));
  assert.equal(ostile.tokenUndo, null);
});

test("la scadenza della proposta è valutata sull'orologio passato, non su quello di sistema", () => {
  assert.equal(propostaScaduta(proposta, new Date("2026-08-23T12:09:59Z")), false);
  assert.equal(propostaScaduta(proposta, new Date("2026-08-23T12:10:00Z")), true);
  assert.match(
    descriviScadenzaProposta(proposta, new Date("2026-08-23T12:02:00Z")),
    /8 minuti/,
  );
  assert.match(
    descriviScadenzaProposta(proposta, new Date("2026-08-23T12:09:30Z")),
    /meno di un minuto/,
  );
  assert.match(
    descriviScadenzaProposta(proposta, new Date("2026-08-23T12:11:00Z")),
    /scaduta/,
  );
});

test("il ripristino è offerto soltanto con operazione applicata e token di undo", () => {
  assert.equal(puoRipristinare(esito({ stato: "Applicata", tokenUndo: "v2u.t" })), true);
  assert.equal(puoRipristinare(esito({ stato: "GiaApplicata", tokenUndo: "v2u.t" })), true);
  assert.equal(puoRipristinare(esito({ stato: "Applicata", tokenUndo: null })), false);
  assert.equal(puoRipristinare(esito({ stato: "Annullata", tokenUndo: "v2u.t" })), false);
  assert.equal(puoRipristinare(esito({ stato: "Ripristinata", tokenUndo: "v2u.t" })), false);
  assert.equal(puoRipristinare(null), false);
});

test("gli stati terminali chiudono la card; quelli voluti dall'utente non allarmano", () => {
  assert.equal(esitoTerminale(esito({ stato: "Applicata", tokenUndo: "v2u.t" })), false);
  assert.equal(esitoTerminale(esito({ stato: "Annullata" })), true);
  assert.equal(esitoTerminale(esito({ stato: "Ripristinata" })), true);
  assert.equal(esitoTerminale(null), false);

  assert.equal(esitoRassicurante(esito({ stato: "Applicata" })), true);
  assert.equal(esitoRassicurante(esito({ stato: "Annullata" })), true);
  assert.equal(esitoRassicurante(esito({ stato: "Negata" })), false);
  assert.equal(esitoRassicurante(esito({ stato: "Conflitto" })), false);
  assert.equal(esitoRassicurante(esito({ stato: "Errore" })), false);
});
