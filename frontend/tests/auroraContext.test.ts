import test from "node:test";
import assert from "node:assert/strict";
import {
  MASSIMO_CARATTERI_CONTESTO_AURORA,
  anteprimaContestoAurora,
  creaContestoAurora,
  leggiTestoSelezionatoAurora,
} from "../src/auroraContext.ts";

test("il contesto vuoto non viene acquisito", () => {
  assert.equal(creaContestoAurora("  \0  ", "Cedolini"), null);
});

test("il contesto è monouso, pulito e limitato", () => {
  const contesto = creaContestoAurora(
    `  riga\0 ${"x".repeat(MASSIMO_CARATTERI_CONTESTO_AURORA + 20)}  `,
    "Cedolini\0",
  );
  assert.ok(contesto);
  assert.equal(contesto.testo.length, MASSIMO_CARATTERI_CONTESTO_AURORA);
  assert.equal(contesto.testo.includes("\0"), false);
  assert.equal(contesto.sezione, "Cedolini");
  assert.equal(contesto.troncato, true);
});

test("l'anteprima non altera il testo da inviare", () => {
  const contesto = creaContestoAurora("Prima riga\nseconda riga molto lunga", "Fatture");
  assert.ok(contesto);
  assert.equal(anteprimaContestoAurora(contesto, 18), "Prima riga seconda…");
  assert.equal(contesto.testo, "Prima riga\nseconda riga molto lunga");
});

function conSelezioneFinta(attraversaPrivato: boolean, azione: () => void, rangeCount = 1) {
  const nodePrecedente = globalThis.Node;
  const windowPrecedente = globalThis.window;
  const privato = {};
  const area = {
    nodeType: 1,
    parentElement: null,
    closest: (selettore: string) => selettore === "[data-aurora-selectable]" ? area : null,
    querySelectorAll: () => attraversaPrivato ? [privato] : [],
  };
  const selezione = {
    rangeCount,
    isCollapsed: false,
    anchorNode: area,
    focusNode: area,
    toString: () => "testo scelto",
    getRangeAt: () => ({
      commonAncestorContainer: area,
      intersectsNode: (elemento: unknown) => elemento === privato,
    }),
  };

  Object.defineProperty(globalThis, "Node", { configurable: true, value: { ELEMENT_NODE: 1 } });
  Object.defineProperty(globalThis, "window", {
    configurable: true,
    value: { getSelection: () => selezione },
  });
  try {
    azione();
  } finally {
    if (nodePrecedente === undefined) delete (globalThis as { Node?: unknown }).Node;
    else Object.defineProperty(globalThis, "Node", { configurable: true, value: nodePrecedente });
    if (windowPrecedente === undefined) delete (globalThis as { window?: unknown }).window;
    else Object.defineProperty(globalThis, "window", { configurable: true, value: windowPrecedente });
  }
}

test("la selezione esplicita nell'area autorizzata viene letta", () => {
  conSelezioneFinta(false, () => {
    assert.equal(leggiTestoSelezionatoAurora(), "testo scelto");
  });
});

test("una selezione che attraversa un'area privata viene rifiutata", () => {
  conSelezioneFinta(true, () => {
    assert.equal(leggiTestoSelezionatoAurora(), "");
  });
});

test("una selezione discontinua con più range viene rifiutata", () => {
  conSelezioneFinta(false, () => {
    assert.equal(leggiTestoSelezionatoAurora(), "");
  }, 2);
});
