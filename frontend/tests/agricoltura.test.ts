import assert from "node:assert/strict";
import test from "node:test";
import {
  eRapportoAgricolo,
  eTipoContrattoOti,
  validaDatiCedolinoAgricolo,
  giorniDetrazioneNelMese,
  oreDaGiornate,
  oreGiornaliereDaContratto,
  riassuntoPaginaAgricola,
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
  giorniTrasferta: 0,
  indennitaTrasfertaGiornaliera: 0,
  rimborsiSpeseDocumentate: 0,
};

test("la trasferta oltre i giorni del mese o negativa viene fermata dal client", () => {
  assert.deepEqual(
    validaDatiCedolinoAgricolo({ ...validi, giorniTrasferta: 35 }),
    ["I giorni di trasferta devono essere compresi tra 0 e 31."]);
  assert.deepEqual(
    validaDatiCedolinoAgricolo({ ...validi, indennitaTrasfertaGiornaliera: -1 }),
    ["L'indennita di trasferta giornaliera non puo essere negativa."]);
  assert.deepEqual(
    validaDatiCedolinoAgricolo({ ...validi, giorniTrasferta: 8, indennitaTrasfertaGiornaliera: 15 }),
    []);
});

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

test("le ore al giorno vengono dal contratto: 30 h/sett. su 6 giorni = 5", () => {
  assert.equal(oreGiornaliereDaContratto(30), 5);
  assert.equal(oreGiornaliereDaContratto(39), 6.5);
  assert.equal(oreGiornaliereDaContratto(0), null);
  assert.equal(oreGiornaliereDaContratto(undefined), null);
});

test("le ore del mese proposte sono giornate per ore al giorno, e restano un numero pulito", () => {
  assert.equal(oreDaGiornate(15, 5), 75);
  assert.equal(oreDaGiornate(13, 6.5), 84.5);
  assert.equal(oreDaGiornate(Number.NaN, 5), null);
  assert.equal(oreDaGiornate(10, 0), null);
});

test("i giorni di detrazione seguono assunzione e cessazione dentro il mese", () => {
  assert.equal(giorniDetrazioneNelMese(2026, 7, "30/06/2026", "30/09/2026"), 31);
  assert.equal(giorniDetrazioneNelMese(2026, 7, "09/07/2026", "31/08/2026"), 23);
  assert.equal(giorniDetrazioneNelMese(2026, 8, "09/07/2026", "30/08/2026"), 30);
  assert.equal(giorniDetrazioneNelMese(2026, 6, "09/07/2026", null), 0);
  assert.equal(giorniDetrazioneNelMese(2026, 7, "2026-07-09", null), 23);
});

test("i gruppi di trasferta passano se validi e vengono fermati se sforano il mese", () => {
  assert.deepEqual(
    validaDatiCedolinoAgricolo({ ...validi, trasferte: [{ giorni: 10, importoGiorno: 15 }, { giorni: 3, importoGiorno: 20 }] }),
    []);
  assert.deepEqual(
    validaDatiCedolinoAgricolo({ ...validi, trasferte: [{ giorni: -1, importoGiorno: 15 }] }),
    ["I gruppi di trasferta non possono avere giorni o importi negativi."]);
  assert.deepEqual(
    validaDatiCedolinoAgricolo({ ...validi, trasferte: [{ giorni: 30, importoGiorno: 15 }, { giorni: 5, importoGiorno: 20 }] }),
    ["I giorni di trasferta dei gruppi superano i 31 giorni del mese."]);
});

test("riassuntoPaginaAgricola descrive solo i campi compilati", () => {
  const testo = riassuntoPaginaAgricola({
    lavoratore: "MARIO ROSSI",
    anno: "2026",
    mese: "7",
    giornate: "22",
    oreMese: "110",
    oreGiorno: "5",
    redditoPresuntoAnnuo: "9000",
    trasferte: [{ giorni: "10", importo: "15" }, { giorni: "5", importo: "20" }],
    rimborsiDocumentati: "0",
    netto: 812.5,
  });
  assert.ok(testo.includes("lavoratore MARIO ROSSI"));
  assert.ok(testo.includes("periodo 7/2026"));
  assert.ok(testo.includes("giornate lavorate 22"));
  assert.ok(testo.includes("trasferte 10 giorni a 15 EUR + 5 giorni a 20 EUR"));
  assert.ok(testo.includes("netto calcolato in anteprima 812.50 EUR"));
  // I default a zero e i campi vuoti non entrano nel riassunto.
  assert.ok(!testo.includes("rimborsi"));

  const minimo = riassuntoPaginaAgricola({
    anno: "2026",
    mese: "8",
    giornate: "",
    oreMese: "",
    oreGiorno: "",
    redditoPresuntoAnnuo: "",
    trasferte: [],
    giorniTrasferta: "0",
    indennitaTrasferta: "0",
  });
  assert.equal(minimo, "periodo 8/2026; reddito annuo presunto non ancora indicato.");
});
