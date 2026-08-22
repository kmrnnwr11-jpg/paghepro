import test from "node:test";
import assert from "node:assert/strict";
import {
  aggiornaManifestMandatoSeCorrente,
  chiaveScopeAurora,
  chiaveScopeConversazioneAurora,
  leasePerScopeAurora,
  mandatoCompatibileConAmbito,
  mandatoPerScope,
  mandatiAttiviUnici,
  mandatiDaRevocare,
} from "../src/auroraScope.ts";
import type { GrantAiEmesso } from "../src/api.ts";

const mandato: GrantAiEmesso = {
  lease: "lease-segreta",
  manifest: {
    grantId: "grant-1",
    purposeCode: "PREPARAZIONE_MESE_PAGHE",
    modalita: "Guidata",
    scopeLevel: "S2",
    aziendaId: "azienda-a",
    periodo: "2026-08",
    risorse: ["Cedolini"],
    operazioni: ["read"],
    autonomiaMax: "Proposta",
    scadeUtc: "2026-08-31T12:00:00Z",
    maxLetture: 10,
    lettureUsate: 0,
    stato: "Attivo",
    descrizione: "test",
  },
};

test("Aurora usa la lease soltanto nello scope esatto azienda-periodo", () => {
  assert.equal(mandatoPerScope(mandato, "azienda-a", "2026-08"), mandato);
  assert.equal(leasePerScopeAurora(mandato, "azienda-a", "2026-08"), "lease-segreta");
  assert.equal(leasePerScopeAurora(mandato, "azienda-b", "2026-08"), null);
  assert.equal(leasePerScopeAurora(mandato, "azienda-a", "2026-09"), null);
});

test("la chiave dello scope cambia anche quando cambia soltanto il periodo", () => {
  assert.notEqual(
    chiaveScopeAurora("azienda-a", "2026-08"),
    chiaveScopeAurora("azienda-a", "2026-09"),
  );
});

test("la conversazione cambia scope quando cambia soltanto la sezione", () => {
  assert.notEqual(
    chiaveScopeConversazioneAurora("azienda-a", "2026-08", "Cedolini"),
    chiaveScopeConversazioneAurora("azienda-a", "2026-08", "Presenze"),
  );
  assert.equal(
    chiaveScopeAurora("azienda-a", "2026-08"),
    chiaveScopeAurora("azienda-a", "2026-08"),
  );
});

test("l'inventario conserva ogni grant Attivo senza interpretarne la scadenza", () => {
  const duplicato = { ...mandato.manifest };
  const revocato = { ...mandato.manifest, grantId: "grant-2", stato: "Revocato" };
  const scaduto = { ...mandato.manifest, grantId: "grant-3", scadeUtc: "2026-07-01T00:00:00Z" };
  const scadenzaIlleggibile = { ...mandato.manifest, grantId: "grant-4", scadeUtc: "non-valida" };

  assert.deepEqual(
    mandatiAttiviUnici([mandato.manifest, duplicato, revocato, scaduto, scadenzaIlleggibile])
      .map((grant) => grant.grantId),
    ["grant-1", "grant-3", "grant-4"],
  );
});

test("la chiusura deduplica mandato corrente, residui e inventario server", () => {
  const altro = { ...mandato.manifest, grantId: "grant-2" };
  const scadutoMaAttivo = {
    ...mandato.manifest,
    grantId: "grant-3",
    scadeUtc: "2026-07-01T00:00:00Z",
  };
  assert.deepEqual(
    mandatiDaRevocare(mandato, [mandato.manifest, altro, scadutoMaAttivo], [altro])
      .map((grant) => grant.grantId),
    ["grant-1", "grant-2", "grant-3"],
  );
});

test("riconosce soltanto i grant emessi per l'ambito richiesto", () => {
  assert.equal(mandatoCompatibileConAmbito(mandato.manifest, "azienda-a", "2026-08"), true);
  assert.equal(mandatoCompatibileConAmbito(mandato.manifest, "azienda-b", "2026-08"), false);
});

test("una riconciliazione quota aggiorna soltanto lo stesso grant ancora corrente", () => {
  const manifestAggiornato = { ...mandato.manifest, lettureUsate: 20, maxLetture: 250 };
  assert.equal(
    aggiornaManifestMandatoSeCorrente(mandato, manifestAggiornato, "grant-1")?.manifest.lettureUsate,
    20,
  );

  const nuovoMandato = {
    ...mandato,
    lease: "lease-nuova",
    manifest: { ...mandato.manifest, grantId: "grant-nuovo", lettureUsate: 0 },
  };
  assert.equal(
    aggiornaManifestMandatoSeCorrente(nuovoMandato, manifestAggiornato, "grant-1"),
    nuovoMandato,
  );
});
