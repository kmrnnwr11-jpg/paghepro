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
    maxLetture: DIMENSIONE_LOTTO_CEDOLINI_AURORA,
  };
}

export function contaStatiCedolini(righe: RigaCedolinoMeseAi[]) {
  return righe.reduce<Record<RigaCedolinoMeseAi["stato"], number>>(
    (totali, riga) => ({ ...totali, [riga.stato]: totali[riga.stato] + 1 }),
    { cartellino_presente: 0, da_compilare: 0, gia_elaborato: 0, bloccato: 0, protetto: 0 },
  );
}
