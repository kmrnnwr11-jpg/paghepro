import test from "node:test";
import assert from "node:assert/strict";
import {
  ambitoControlloCedolini,
  DIMENSIONE_LOTTO_CEDOLINI_AURORA,
  contaStatiCedolini,
  LETTURE_PREPARAZIONE_MESE,
  mandatoSoloLettura,
  richiestaControlloCedolini,
  richiestaPreparazioneMese,
  stessoAmbitoControlloCedolini,
} from "../src/auroraCedolini.ts";
import type { RigaCedolinoMeseAi } from "../src/api.ts";

test("crea una richiesta S2 con lotto UI separato dal budget deciso dal server", () => {
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
  });
  assert.equal(DIMENSIONE_LOTTO_CEDOLINI_AURORA, 10);
  assert.equal("maxLetture" in richiesta, false);
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
    agricolo: 0,
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

test("il mandato Prepara il mese chiede permessi espliciti, mai il prodotto cartesiano", () => {
  const r = richiestaPreparazioneMese("azienda-a", "2026-08");
  assert.equal(r.purposeCode, "PREPARAZIONE_MESE_PAGHE");
  assert.equal(r.scopeLevel, "S2");
  assert.deepEqual(r.operazioni, ["read", "draft", "apply_reversible"]);
  const permessi = (r.permessi ?? []).map((p) => `${p.risorsa}|${p.operazione}`);
  // Ogni lettura richiesta dagli strumenti A3, e draft/apply SOLO su Cartellini e Cedolini.
  for (const risorsa of LETTURE_PREPARAZIONE_MESE)
    assert.ok(permessi.includes(`${risorsa}|read`), `manca ${risorsa}|read`);
  assert.ok(permessi.includes("Cartellini|draft"));
  assert.ok(permessi.includes("Cartellini|apply_reversible"));
  assert.ok(permessi.includes("Cedolini|draft"));
  assert.ok(permessi.includes("Cedolini|apply_reversible"));
  assert.equal(permessi.filter((p) => p.endsWith("|draft") || p.endsWith("|apply_reversible")).length, 4);
  assert.equal(permessi.length, LETTURE_PREPARAZIONE_MESE.length + 4);
});

test("solo un mandato in pura lettura non blocca il pannello", () => {
  // Il mandato automatico del consenso pieno è read-only: non è un residuo da segnalare.
  assert.equal(mandatoSoloLettura({ operazioni: ["read"] }), true);
  // Il mandato «Prepara il mese» (A3) e qualunque mandato con scritture bloccano.
  assert.equal(mandatoSoloLettura({ operazioni: ["read", "draft", "apply_reversible"] }), false);
  assert.equal(mandatoSoloLettura({ operazioni: ["draft"] }), false);
  assert.equal(mandatoSoloLettura({ operazioni: [] }), false);
});
