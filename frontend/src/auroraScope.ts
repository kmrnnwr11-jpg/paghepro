import type { GrantAiEmesso, ManifestGrantAi } from "./api";

export function chiaveScopeAurora(aziendaId: string | null | undefined, periodo: string): string {
  return `${aziendaId ?? "nessuna-azienda"}:${periodo}`;
}

export function chiaveScopeConversazioneAurora(
  aziendaId: string | null | undefined,
  periodo: string,
  sezione: string,
): string {
  return `${chiaveScopeAurora(aziendaId, periodo)}:${sezione}`;
}

export function mandatoPerScope(
  mandato: GrantAiEmesso | null,
  aziendaId: string | null | undefined,
  periodo: string,
): GrantAiEmesso | null {
  if (!mandato) return null;
  return mandato.manifest.aziendaId === (aziendaId ?? null)
    && mandato.manifest.periodo === periodo
    ? mandato
    : null;
}

export function leasePerScopeAurora(
  mandato: GrantAiEmesso | null,
  aziendaId: string | null | undefined,
  periodo: string,
): string | null {
  return mandatoPerScope(mandato, aziendaId, periodo)?.lease ?? null;
}

export function mandatiAttiviUnici(
  mandati: ManifestGrantAi[],
): ManifestGrantAi[] {
  const unici = new Map<string, ManifestGrantAi>();
  for (const manifest of mandati) {
    // Solo il server decide se un grant dichiarato Attivo sia ancora spendibile.
    if (manifest.stato === "Attivo") unici.set(manifest.grantId, manifest);
  }
  return [...unici.values()];
}

export function mandatiDaRevocare(
  mandato: GrantAiEmesso | null,
  residui: ManifestGrantAi[],
  inventario: ManifestGrantAi[] = [],
): ManifestGrantAi[] {
  const unici = new Map<string, ManifestGrantAi>();
  for (const manifest of [
    ...(mandato ? [mandato.manifest] : []),
    ...residui,
    ...inventario,
  ]) {
    if (manifest.stato === "Attivo") unici.set(manifest.grantId, manifest);
  }
  return [...unici.values()];
}

export function mandatoCompatibileConAmbito(
  manifest: ManifestGrantAi,
  aziendaId: string,
  periodo: string,
): boolean {
  return manifest.purposeCode === "PREPARAZIONE_MESE_PAGHE"
    && manifest.aziendaId === aziendaId
    && manifest.periodo === periodo;
}

export function aggiornaManifestMandatoSeCorrente(
  corrente: GrantAiEmesso | null,
  manifestServer: ManifestGrantAi,
  grantIdAtteso: string,
): GrantAiEmesso | null {
  if (!corrente
    || corrente.manifest.grantId !== grantIdAtteso
    || manifestServer.grantId !== grantIdAtteso) return corrente;
  return { ...corrente, manifest: manifestServer };
}
