const PREFISSO_AZIENDA_CORRENTE = "paghepro:azienda-corrente";
const PREFISSO_ULTIMA_AZIENDA = "paghepro:ultima-azienda";
export const AZIENDA_CORRENTE_CAMBIATA = "paghepro:azienda-corrente-cambiata";

function tenantDellaSessione(): string {
  return localStorage.getItem("tenant_corrente") || "senza-tenant";
}

export function chiaveStorageTenant(prefisso: string): string {
  return `${prefisso}:${tenantDellaSessione()}`;
}

export function leggiAziendaCorrenteId(): string | null {
  try {
    // La scelta vale per la sessione: a un nuovo accesso il professionista conferma
    // esplicitamente il cliente prima di avviare elaborazioni o adempimenti.
    return sessionStorage.getItem(chiaveStorageTenant(PREFISSO_AZIENDA_CORRENTE));
  } catch {
    return null;
  }
}

export function leggiUltimaAziendaId(): string | null {
  try {
    return localStorage.getItem(chiaveStorageTenant(PREFISSO_ULTIMA_AZIENDA));
  } catch {
    return null;
  }
}

export function salvaAziendaCorrenteId(id: string): void {
  try {
    sessionStorage.setItem(chiaveStorageTenant(PREFISSO_AZIENDA_CORRENTE), id);
    localStorage.setItem(chiaveStorageTenant(PREFISSO_ULTIMA_AZIENDA), id);
  } catch {
    /* storage non disponibile: la pagina resta comunque utilizzabile */
  }
  try {
    window.dispatchEvent(new CustomEvent(AZIENDA_CORRENTE_CAMBIATA, { detail: { id } }));
  } catch {
    /* ambiente senza window: nessun aggiornamento live necessario */
  }
}

export function dimenticaAziendaCorrente(): void {
  try {
    sessionStorage.removeItem(chiaveStorageTenant(PREFISSO_AZIENDA_CORRENTE));
  } catch {
    /* storage non disponibile: nessuna scelta da cancellare */
  }
}

export function scegliAziendaPreferita<T extends { id: string }>(aziende: T[]): T | null {
  const idCorrente = leggiAziendaCorrenteId();
  return idCorrente ? aziende.find((a) => a.id === idCorrente) ?? null : null;
}
