import test from "node:test";
import assert from "node:assert/strict";
import { creaSequenziatoreRichieste } from "../src/ultimaRichiesta.ts";

function differita<T>() {
  let risolvi!: (valore: T) => void;
  const promessa = new Promise<T>((resolve) => { risolvi = resolve; });
  return { promessa, risolvi };
}

test("una risposta vecchia non può sostituire i dati dello scope più recente", async () => {
  const sequenza = creaSequenziatoreRichieste();
  const prima = sequenza.apri();
  const rispostaA = differita<string>();
  const seconda = sequenza.apri();
  const rispostaB = differita<string>();
  const applicate: string[] = [];

  const esitoA = rispostaA.promessa.then((valore) => {
    if (prima.attiva()) applicate.push(valore);
  });
  const esitoB = rispostaB.promessa.then((valore) => {
    if (seconda.attiva()) applicate.push(valore);
  });

  rispostaB.risolvi("dipendente B");
  await esitoB;
  rispostaA.risolvi("dipendente A");
  await esitoA;

  assert.deepEqual(applicate, ["dipendente B"]);
});

test("la pulizia dello scope invalida anche l'ultima richiesta ancora pendente", () => {
  const richiesta = creaSequenziatoreRichieste().apri();
  assert.equal(richiesta.attiva(), true);
  richiesta.invalida();
  assert.equal(richiesta.attiva(), false);
});
