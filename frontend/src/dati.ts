// Tutti i comuni italiani (caricati da /comuni.json, ~7900 voci).
// Scegliendo il comune l'app riempie provincia, CAP, regione e codice catastale.

export interface Comune {
  nome: string;
  cat: string;   // codice catastale (per le addizionali comunali)
  prov: string;  // sigla provincia
  reg: string;   // regione
  cap: string;
}

type Riga = [string, string, string, string, string];

let cache: Comune[] | null = null;

export async function caricaComuni(): Promise<Comune[]> {
  if (cache) return cache;
  const res = await fetch("/comuni.json");
  const raw = (await res.json()) as Riga[];
  cache = raw.map(([nome, cat, prov, reg, cap]) => ({ nome, cat, prov, reg, cap }));
  return cache;
}

export function cercaComuni(lista: Comune[], query: string, max = 8): Comune[] {
  const s = query.trim().toLowerCase();
  if (s.length < 2) return [];
  const inizio = lista.filter((c) => c.nome.toLowerCase().startsWith(s));
  return inizio.slice(0, max);
}
