import type { RichiestaGrantAi, RigaCedolinoMeseAi } from "./api";

export const DIMENSIONE_LOTTO_CEDOLINI_AURORA = 10;

export interface AmbitoControlloCedolini {
  aziendaId: string;
  periodo: string;
}

export function ambitoControlloCedolini(
  aziendaId: string,
  periodo: string,
): AmbitoControlloCedolini {
  return { aziendaId, periodo };
}

export function stessoAmbitoControlloCedolini(
  sinistro: AmbitoControlloCedolini | null,
  destro: AmbitoControlloCedolini | null,
): boolean {
  return sinistro !== null
    && destro !== null
    && sinistro.aziendaId === destro.aziendaId
    && sinistro.periodo === destro.periodo;
}

export function richiestaControlloCedolini(
  aziendaId: string,
  periodo: string,
): RichiestaGrantAi {
  return {
    purposeCode: "PREPARAZIONE_MESE_PAGHE",
    modalita: "Guidata",
    scopeLevel: "S2",
    aziendaId,
    periodo,
    risorse: ["Cedolini"],
    operazioni: ["read"],
    durataMinuti: 30,
  };
}

/** Le letture di cui gli strumenti di modifica hanno bisogno (stesse dipendenze dei tool A3). */
export const LETTURE_PREPARAZIONE_MESE = [
  "Aziende", "Dipendenti", "Rapporti", "Cartellini", "Cedolini", "PraticheF24",
] as const;

/**
 * Mandato «Prepara il mese»: letture governate più modifiche con conferma (presenze e
 * cedolino bozza). I permessi sono coppie esplicite risorsa/operazione: il Governor vieta
 * il prodotto cartesiano, quindi draft e apply valgono SOLO su Cartellini e Cedolini.
 */
export function richiestaPreparazioneMese(
  aziendaId: string,
  periodo: string,
): RichiestaGrantAi {
  return {
    purposeCode: "PREPARAZIONE_MESE_PAGHE",
    modalita: "Guidata",
    scopeLevel: "S2",
    aziendaId,
    periodo,
    risorse: [...LETTURE_PREPARAZIONE_MESE],
    operazioni: ["read", "draft", "apply_reversible"],
    durataMinuti: 60,
    permessi: [
      ...LETTURE_PREPARAZIONE_MESE.map((risorsa) => ({ risorsa, operazione: "read" })),
      { risorsa: "Cartellini", operazione: "draft" },
      { risorsa: "Cartellini", operazione: "apply_reversible" },
      { risorsa: "Cedolini", operazione: "draft" },
      { risorsa: "Cedolini", operazione: "apply_reversible" },
    ],
  };
}

/**
 * Un mandato di sola lettura — come quello che il server emette da solo col consenso
 * pieno — non blocca il pannello: non occupa lo slot A3, scade da sé e può essere
 * revocato prima di emettere il mandato «Prepara il mese».
 */
export function mandatoSoloLettura(grant: { operazioni: string[] }): boolean {
  return grant.operazioni.length === 1 && grant.operazioni[0] === "read";
}

/**
 * Mandato emesso in automatico dalla chat col consenso pieno: finalità dedicata
 * ASSISTENTE_CHAT (di sola lettura o con proposte confermabili) oppure il vecchio
 * mandato read-only. Non è un «residuo» da segnalare: il pannello lo revoca da sé
 * prima di emettere il mandato «Prepara il mese», e la chat ne riemetterà uno
 * alla prossima domanda, se serve.
 */
export function mandatoAutomaticoAurora(
  grant: { operazioni: string[]; purposeCode: string },
): boolean {
  return grant.purposeCode === "ASSISTENTE_CHAT" || mandatoSoloLettura(grant);
}

export function contaStatiCedolini(righe: RigaCedolinoMeseAi[]) {
  return righe.reduce<Record<RigaCedolinoMeseAi["stato"], number>>(
    (totali, riga) => ({ ...totali, [riga.stato]: totali[riga.stato] + 1 }),
    { cartellino_presente: 0, da_compilare: 0, gia_elaborato: 0, bloccato: 0, protetto: 0, agricolo: 0 },
  );
}
