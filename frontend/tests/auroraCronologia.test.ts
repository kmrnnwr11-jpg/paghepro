import test from "node:test";
import assert from "node:assert/strict";
import {
  aggiornaElenco,
  MASSIMO_CONVERSAZIONI,
  titoloConversazione,
  type ConversazioneSalvata,
} from "../src/auroraCronologia.ts";

function conversazione(
  id: string,
  scope = "azienda-a:2026-08",
  testo = `Domanda ${id}`,
): ConversazioneSalvata {
  return {
    id,
    scope,
    titolo: "",
    aggiornataIl: "2026-08-27T10:00:00.000Z",
    bolle: [
      { chi: "utente", testo },
      { chi: "aurora", testo: "Risposta." },
    ],
    storia: [{ ruolo: "user", contenuto: testo }],
  };
}

test("il titolo è la prima domanda dell'utente, accorciata oltre i 60 caratteri", () => {
  assert.equal(titoloConversazione(conversazione("a").bolle), "Domanda a");
  const lunga = conversazione("b", "s", "x".repeat(80));
  assert.equal(titoloConversazione(lunga.bolle), `${"x".repeat(60)}…`);
  assert.equal(titoloConversazione([{ chi: "aurora", testo: "Solo io" }]), "Conversazione");
});

test("aggiornare una conversazione la porta in testa senza duplicarla", () => {
  const elenco = aggiornaElenco(
    aggiornaElenco([], conversazione("prima")),
    conversazione("seconda"),
  );
  assert.deepEqual(elenco.map((c) => c.id), ["seconda", "prima"]);

  const aggiornata = aggiornaElenco(elenco, conversazione("prima"));
  assert.deepEqual(aggiornata.map((c) => c.id), ["prima", "seconda"]);
  assert.equal(aggiornata[0].titolo, "Domanda prima");
});

test("oltre il limite viene scartata la conversazione più vecchia", () => {
  let elenco: ConversazioneSalvata[] = [];
  for (let i = 0; i < MASSIMO_CONVERSAZIONI + 3; i++)
    elenco = aggiornaElenco(elenco, conversazione(`c${i}`));
  assert.equal(elenco.length, MASSIMO_CONVERSAZIONI);
  assert.equal(elenco[0].id, `c${MASSIMO_CONVERSAZIONI + 2}`);
  assert.ok(!elenco.some((c) => c.id === "c0"));
});

test("bolle e storia vengono limitate senza perdere le più recenti", () => {
  const gigante: ConversazioneSalvata = {
    ...conversazione("g"),
    bolle: Array.from({ length: 200 }, (_, i) => ({
      chi: i % 2 === 0 ? "utente" as const : "aurora" as const,
      testo: `bolla ${i}`,
    })),
    storia: Array.from({ length: 60 }, (_, i) => ({
      ruolo: "user",
      contenuto: `messaggio ${i}`,
    })),
  };
  const [salvata] = aggiornaElenco([], gigante);
  assert.equal(salvata.bolle.length, 80);
  assert.equal(salvata.bolle.at(-1)?.testo, "bolla 199");
  assert.equal(salvata.storia.length, 24);
  assert.equal(salvata.storia.at(-1)?.contenuto, "messaggio 59");
});
