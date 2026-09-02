const ENDPOINT_GESTIONE_SESSIONE = new Set([
  "/api/auth/registra",
  "/api/auth/login",
  "/api/auth/refresh",
  "/api/auth/logout",
  "/api/auth/mobile/registra",
  "/api/auth/mobile/login",
  "/api/auth/mobile/refresh",
  "/api/auth/mobile/logout",
  "/api/auth/google",
]);

/** Solo questi endpoint creano, rinnovano o chiudono la sessione e non devono auto-rinnovarsi. */
export function endpointGestioneSessione(path: string): boolean {
  return ENDPOINT_GESTIONE_SESSIONE.has(path.split("?", 1)[0]);
}
