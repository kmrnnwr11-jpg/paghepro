import test from "node:test";
import assert from "node:assert/strict";
import {
  alimentaAdempimenti,
  anniContabilizzabili,
  documentoDefinitivo,
  etichettaDownloadCedolino,
  statoEffettivo,
  versioneWorkflow,
} from "../src/cedolinoStati.ts";
import type { CedolinoArchiviato, StatoCedolino } from "../src/api.ts";

function cedolino(anno: number, stato: StatoCedolino): CedolinoArchiviato {
  return {
    id: `${anno}-${stato}`,
    anno,
    mese: 1,
    nettoInBusta: 1,
    costoAzienda: 1,
    stato,
    versione: 1,
  };
}

test("PreWorkflow alimenta lo storico senza diventare un documento definitivo", () => {
  assert.equal(alimentaAdempimenti("PreWorkflow"), true);
  assert.equal(documentoDefinitivo("PreWorkflow"), false);
  assert.equal(etichettaDownloadCedolino("PreWorkflow"), "Genera ricostruzione storica");
  assert.equal(documentoDefinitivo("Chiusa"), true);
});

test("gli adempimenti includono Chiusa e PreWorkflow ma escludono gli stati in lavorazione", () => {
  const lista = [
    cedolino(2022, "PreWorkflow"),
    cedolino(2023, "Chiusa"),
    cedolino(2024, "Bozza"),
    cedolino(2025, "Controllata"),
    cedolino(2026, "Validata"),
  ];

  assert.deepEqual(anniContabilizzabili(lista), [2023, 2022]);
});

test("un record senza stato resta fail-closed e non viene promosso a bozza o storico", () => {
  assert.equal(statoEffettivo({ stato: undefined }), "Sconosciuto");
  assert.equal(alimentaAdempimenti(statoEffettivo({ stato: undefined })), false);
  assert.equal(etichettaDownloadCedolino(statoEffettivo({ stato: undefined })), "Download non disponibile");
  assert.equal(versioneWorkflow({ versione: undefined }), null);
  assert.equal(versioneWorkflow({ versione: 0 }), null);
  assert.equal(versioneWorkflow({ versione: 1 }), 1);
});
