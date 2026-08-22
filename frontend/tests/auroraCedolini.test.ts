import test from "node:test";
import assert from "node:assert/strict";
import {
  ambitoControlloCedolini,
  DIMENSIONE_LOTTO_CEDOLINI_AURORA,
  contaStatiCedolini,
  richiestaControlloCedolini,
  stessoAmbitoControlloCedolini,
} from "../src/auroraCedolini.ts";
import type { RigaCedolinoMeseAi } from "../src/api.ts";

test("crea un mandato S2 vincolato ad azienda, mese e dieci letture", () => {
  const richiesta = richiestaControlloCedolini("azienda-1", "2026-07");
  assert.deepEqual(richiesta, {
    purposeCode: "PREPARAZIONE_MESE_PAGHE",
    modalita: "Guidata",
    scopeLevel: "S2",
    aziendaId: "azienda-1",
    periodo: "2026-07",
    risorse: ["Cedolini"],
    operazioni: ["read"],
    durataMinuti: 30,
    maxLetture: DIMENSIONE_LOTTO_CEDOLINI_AURORA,
  });
});

test("riepiloga correttamente un lotto misto", () => {
  const stati: RigaCedolinoMeseAi["stato"][] = [
    "cartellino_presente", "cartellino_presente", "cartellino_presente",
    "cartellino_presente", "cartellino_presente", "cartellino_presente",
    "bloccato", "bloccato", "gia_elaborato", "protetto",
  ];
  const righe = stati.map((stato, indice) => ({
    rapportoId: String(indice),
    nominativo: `Dipendente ${indice}`,
    stato,
    motivo: "test",
    cedolinoId: null,
    statoCedolino: null,
  }));
  assert.deepEqual(contaStatiCedolini(righe), {
    cartellino_presente: 6,
    da_compilare: 0,
    gia_elaborato: 1,
    bloccato: 2,
    protetto: 1,
  });
});

test("il preflight vale soltanto per la stessa azienda e lo stesso mese", () => {
  const approvato = ambitoControlloCedolini("azienda-1", "2026-07");
  assert.equal(
    stessoAmbitoControlloCedolini(
      approvato,
      ambitoControlloCedolini("azienda-1", "2026-07"),
    ),
    true,
  );
  assert.equal(
    stessoAmbitoControlloCedolini(
      approvato,
      ambitoControlloCedolini("azienda-2", "2026-07"),
    ),
    false,
  );
  assert.equal(
    stessoAmbitoControlloCedolini(
      approvato,
      ambitoControlloCedolini("azienda-1", "2026-08"),
    ),
    false,
  );
  assert.equal(stessoAmbitoControlloCedolini(approvato, null), false);
});
