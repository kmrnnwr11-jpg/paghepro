import assert from "node:assert/strict";
import test from "node:test";
import {
  preparaDatiGateRapporto,
  type DatiGateRapportoForm,
} from "../src/rapportoFiscaleForm.ts";

const formCompleto: DatiGateRapportoForm = {
  regione: "Lombardia",
  codiceCatastaleComune: "F205",
  dataDecorrenzaDomicilioFiscale: "2026-01-01",
  riferimentoProgressiviFiscaliPregressi: "CU provvisoria 2026-07 verificata",
  rolResiduiIniziali: "12,50",
  exFestivitaResidueIniziali: "4",
  addizionaleRegionaleConguaglioAnnuo: "120.25",
  addizionaleComunaleSaldoAnnuo: "45,10",
  addizionaleComunaleAccontoAnnuo: "13.53",
};

test("normalizza domicilio, riferimento e saldi separati per il payload rapporto", () => {
  const esito = preparaDatiGateRapporto({
    ...formCompleto,
    regione: "  Lombardia ",
    codiceCatastaleComune: " f205 ",
    riferimentoProgressiviFiscaliPregressi: "  CU provvisoria 2026-07 verificata  ",
  });

  assert.equal(esito.ok, true);
  if (!esito.ok) return;
  assert.deepEqual(esito.valore, {
    regione: "Lombardia",
    codiceCatastaleComune: "F205",
    dataDecorrenzaDomicilioFiscale: "2026-01-01",
    riferimentoProgressiviFiscaliPregressi: "CU provvisoria 2026-07 verificata",
    rolResiduiIniziali: 12.5,
    exFestivitaResidueIniziali: 4,
    addizionaleRegionaleConguaglioAnnuo: 120.25,
    addizionaleComunaleSaldoAnnuo: 45.1,
    addizionaleComunaleAccontoAnnuo: 13.53,
  });
});

test("conserva lo zero esplicito dei tre conguagli e dei due saldi", () => {
  const esito = preparaDatiGateRapporto({
    ...formCompleto,
    rolResiduiIniziali: "0",
    exFestivitaResidueIniziali: "0",
    addizionaleRegionaleConguaglioAnnuo: "0",
    addizionaleComunaleSaldoAnnuo: "0",
    addizionaleComunaleAccontoAnnuo: "0",
  });

  assert.equal(esito.ok, true);
  if (!esito.ok) return;
  assert.equal(esito.valore.rolResiduiIniziali, 0);
  assert.equal(esito.valore.exFestivitaResidueIniziali, 0);
  assert.equal(esito.valore.addizionaleRegionaleConguaglioAnnuo, 0);
  assert.equal(esito.valore.addizionaleComunaleSaldoAnnuo, 0);
  assert.equal(esito.valore.addizionaleComunaleAccontoAnnuo, 0);
});

test("non trasforma campi vuoti in zero e segnala tutti i dati del gate mancanti", () => {
  const esito = preparaDatiGateRapporto({
    regione: "",
    codiceCatastaleComune: "",
    dataDecorrenzaDomicilioFiscale: "",
    riferimentoProgressiviFiscaliPregressi: "",
    rolResiduiIniziali: "",
    exFestivitaResidueIniziali: "",
    addizionaleRegionaleConguaglioAnnuo: "",
    addizionaleComunaleSaldoAnnuo: "",
    addizionaleComunaleAccontoAnnuo: "",
  });

  assert.equal(esito.ok, false);
  if (esito.ok) return;
  assert.equal(esito.errori.length, 9);
  assert.ok(esito.errori.some((errore) => /progressivi fiscali/i.test(errore)));
  assert.ok(esito.errori.some((errore) => /anche 0/i.test(errore)));
});

test("rifiuta codice catastale, data, numeri e conguagli negativi non validi", () => {
  const esito = preparaDatiGateRapporto({
    ...formCompleto,
    codiceCatastaleComune: "MI",
    dataDecorrenzaDomicilioFiscale: "2026-02-30",
    rolResiduiIniziali: "non disponibile",
    addizionaleComunaleSaldoAnnuo: "-1",
  });

  assert.equal(esito.ok, false);
  if (esito.ok) return;
  assert.ok(esito.errori.some((errore) => /codice catastale valido/i.test(errore)));
  assert.ok(esito.errori.some((errore) => /decorrenza valida/i.test(errore)));
  assert.ok(esito.errori.some((errore) => /Saldo iniziale ROL: inserisci un numero valido/i.test(errore)));
  assert.ok(esito.errori.some((errore) => /Saldo addizionale comunale: il valore non può essere negativo/i.test(errore)));
});
