import assert from "node:assert/strict";
import test from "node:test";
import { endpointGestioneSessione } from "../src/apiSessione.ts";

test("solo login, registrazione, refresh e logout saltano l'auto-rinnovo", () => {
  assert.equal(endpointGestioneSessione("/api/auth/login"), true);
  assert.equal(endpointGestioneSessione("/api/auth/mobile/refresh"), true);
  assert.equal(endpointGestioneSessione("/api/auth/logout?ovunque=1"), true);
  assert.equal(endpointGestioneSessione("/api/auth/cambia-password"), false);
  assert.equal(endpointGestioneSessione("/api/auth/2fa/stato"), false);
  assert.equal(endpointGestioneSessione("/api/auth/2fa/attiva"), false);
});
