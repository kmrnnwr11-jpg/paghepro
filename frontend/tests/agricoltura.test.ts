import assert from "node:assert/strict";
import test from "node:test";
import {
  eRapportoAgricolo,
  eTipoContrattoOti,
  validaDatiCedolinoAgricolo,
} from "../src/agricoltura.ts";
import type { DatiMensiliCedolinoAgricolo } from "../src/api.ts";

const validi: DatiMensiliCedolinoAgricolo = {
  anno: 2025,
  mese: 10,
  oreOrdinarie: 64,
  giornateLavorate: 8,
  giorniDetrazione: 10,
  unaTantum: 114,
  detrazioniGiaGoduteAnno: 0,
  arrotondamentoRiportato: 0,
  ultimoCedolino: false,
  redditoPresuntoAnnuo: 6000,
};

test("riconosce soltanto il CCNL agricolo dedicato", () => {
  assert.equal(eRapportoAgricolo("AGRICOLTURA_OPERAI"), true);
  assert.equal(eRapportoAgricolo(" agricoltura_operai "), true);
  assert.equal(eRapportoAgricolo("COMMERCIO"), false);
});

test("riconosce OTI senza confonderlo con il tempo determinato", () => {
  assert.equal(eTipoContrattoOti("OTI"), true);
  assert.equal(eTipoContrattoOti("Tempo indeterminato"), true);
  assert.equal(eTipoContrattoOti("Tempo determinato"), false);
  assert.equal(eTipoContrattoOti("OTD"), false);
});

test("i dati della busta reale di ottobre superano i controlli client", () => {
  assert.deepEqual(validaDatiCedolinoAgricolo(validi), []);
});

test("giornate e giorni detrazione non possono superare il mese", () => {
  const errori = validaDatiCedolinoAgricolo({
    ...validi,
    mese: 2,
    giornateLavorate: 30,
    giorniDetrazione: 30,
  });
  assert.equal(errori.length, 2);
  assert.ok(errori.every((errore) => errore.includes("28")));
});

test("il reddito presunto e obbligatorio per non annualizzare un mese stagionale", () => {
  const errori = validaDatiCedolinoAgricolo({ ...validi, redditoPresuntoAnnuo: null });
  assert.deepEqual(errori, ["Indica il reddito annuo presunto reale del rapporto."]);
});
