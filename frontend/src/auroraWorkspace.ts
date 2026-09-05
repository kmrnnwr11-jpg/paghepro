export interface StatoSchedeAurora {
  ids: string[];
  attiva: string | null;
}

interface ArchivioMinimo {
  getItem(chiave: string): string | null;
  setItem(chiave: string, valore: string): void;
}

interface ArchivioEnumerabile extends ArchivioMinimo {
  readonly length: number;
  key(indice: number): string | null;
  removeItem(chiave: string): void;
}

function identita(archivio: ArchivioMinimo): { tenant: string; utente: string } | null {
  const tenant = archivio.getItem("tenant_corrente")?.trim();
  const utente = archivio.getItem("utente_corrente")?.trim();
  return tenant && utente ? { tenant, utente } : null;
}

export function chiaveSchedeAurora(archivio: ArchivioMinimo = localStorage): string | null {
  const corrente = identita(archivio);
  return corrente
    ? `paghepro:aurora-schede:v1:${corrente.tenant}:${corrente.utente}`
    : null;
}

/**
 * Bonifica la cache transcript della vecchia versione. Non viene importata perché la chiave
 * tenant-only non permette di dimostrare quale utente l'abbia scritta.
 */
export function eliminaCronologiaAuroraLegacy(
  archivio: ArchivioEnumerabile = localStorage,
): void {
  // Lo stesso origin può contenere cache di studi usati in passato. Si elimina l'intero
  // prefisso, non soltanto il tenant corrente: nessun transcript legacy è attribuibile con
  // certezza a un utente e quindi nessuno viene importato.
  for (let indice = archivio.length - 1; indice >= 0; indice--) {
    const chiave = archivio.key(indice);
    if (chiave?.startsWith("paghepro:aurora-conversazioni:"))
      archivio.removeItem(chiave);
  }
}

/**
 * Sul dispositivo persistono soltanto ID opachi delle schede, mai titoli o transcript. La
 * sorgente dei contenuti resta il server cifrato e un altro utente non condivide la chiave.
 */
export function leggiSchedeAurora(
  archivio: ArchivioMinimo = localStorage,
): StatoSchedeAurora {
  const chiave = chiaveSchedeAurora(archivio);
  if (!chiave) return { ids: [], attiva: null };
  try {
    const valore = JSON.parse(archivio.getItem(chiave) ?? "null") as Partial<StatoSchedeAurora> | null;
    const ids = Array.isArray(valore?.ids)
      ? [...new Set(valore.ids.filter((id): id is string => typeof id === "string" && id.length > 0))]
      : [];
    const attiva = typeof valore?.attiva === "string" && ids.includes(valore.attiva)
      ? valore.attiva
      : ids[0] ?? null;
    return { ids, attiva };
  } catch {
    return { ids: [], attiva: null };
  }
}

export function salvaSchedeAurora(
  stato: StatoSchedeAurora,
  archivio: ArchivioMinimo = localStorage,
): void {
  const chiave = chiaveSchedeAurora(archivio);
  if (!chiave) return;
  const ids = [...new Set(stato.ids.filter(Boolean))];
  const attiva = stato.attiva && ids.includes(stato.attiva) ? stato.attiva : ids[0] ?? null;
  try {
    archivio.setItem(chiave, JSON.stringify({ ids, attiva }));
  } catch {
    // La cronologia non dipende dallo storage locale: al massimo non si ripristina il layout tab.
  }
}

export function apriSchedaAurora(
  stato: StatoSchedeAurora,
  id: string,
): StatoSchedeAurora {
  return {
    ids: stato.ids.includes(id) ? stato.ids : [...stato.ids, id],
    attiva: id,
  };
}

export function chiudiSchedaAurora(
  stato: StatoSchedeAurora,
  id: string,
): StatoSchedeAurora {
  const indice = stato.ids.indexOf(id);
  const ids = stato.ids.filter((corrente) => corrente !== id);
  if (stato.attiva !== id) return { ids, attiva: stato.attiva };
  return { ids, attiva: ids[Math.min(Math.max(indice, 0), ids.length - 1)] ?? null };
}
