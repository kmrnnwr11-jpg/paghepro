const CHIAVE_AZIENDA_CORRENTE = "aziendaCorrenteId";
export const AZIENDA_CORRENTE_CAMBIATA = "paghepro:azienda-corrente-cambiata";

export function leggiAziendaCorrenteId(): string | null {
  try {
    return localStorage.getItem(CHIAVE_AZIENDA_CORRENTE);
  } catch {
    return null;
  }
}

export function salvaAziendaCorrenteId(id: string): void {
  try {
    localStorage.setItem(CHIAVE_AZIENDA_CORRENTE, id);
  } catch {
    /* storage non disponibile: la pagina resta comunque utilizzabile */
  }
  try {
    window.dispatchEvent(new CustomEvent(AZIENDA_CORRENTE_CAMBIATA, { detail: { id } }));
  } catch {
    /* ambiente senza window: nessun aggiornamento live necessario */
  }
}

export function scegliAziendaPreferita<T extends { id: string }>(aziende: T[]): T | null {
  const idCorrente = leggiAziendaCorrenteId();
  return (idCorrente ? aziende.find((a) => a.id === idCorrente) : undefined) ?? aziende[0] ?? null;
}
