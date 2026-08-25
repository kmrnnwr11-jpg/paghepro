export interface DatiGateRapportoForm {
  regione: string;
  codiceCatastaleComune: string;
  dataDecorrenzaDomicilioFiscale: string;
  riferimentoProgressiviFiscaliPregressi: string;
  rolResiduiIniziali: string;
  exFestivitaResidueIniziali: string;
  addizionaleRegionaleConguaglioAnnuo: string;
  addizionaleComunaleSaldoAnnuo: string;
  addizionaleComunaleAccontoAnnuo: string;
}

export interface DatiGateRapportoPayload {
  regione: string;
  codiceCatastaleComune: string;
  dataDecorrenzaDomicilioFiscale: string;
  riferimentoProgressiviFiscaliPregressi: string;
  rolResiduiIniziali: number;
  exFestivitaResidueIniziali: number;
  addizionaleRegionaleConguaglioAnnuo: number;
  addizionaleComunaleSaldoAnnuo: number;
  addizionaleComunaleAccontoAnnuo: number;
}

export type EsitoDatiGateRapporto =
  | { ok: true; valore: DatiGateRapportoPayload }
  | { ok: false; errori: string[] };

function dataIsoValida(valore: string): boolean {
  const parti = /^(\d{4})-(\d{2})-(\d{2})$/.exec(valore);
  if (!parti) return false;
  const anno = Number(parti[1]);
  const mese = Number(parti[2]);
  const giorno = Number(parti[3]);
  const data = new Date(Date.UTC(anno, mese - 1, giorno));
  return data.getUTCFullYear() === anno
    && data.getUTCMonth() === mese - 1
    && data.getUTCDate() === giorno;
}

function leggiNumeroEsplicito(
  valore: string,
  etichetta: string,
  errori: string[],
  nonNegativo = false,
): number | undefined {
  const testo = valore.trim();
  if (!testo) {
    errori.push(`${etichetta}: inserisci un valore, anche 0 quando il dato certificato è zero.`);
    return undefined;
  }
  const numero = Number(testo.replace(",", "."));
  if (!Number.isFinite(numero)) {
    errori.push(`${etichetta}: inserisci un numero valido.`);
    return undefined;
  }
  if (nonNegativo && numero < 0) {
    errori.push(`${etichetta}: il valore non può essere negativo.`);
    return undefined;
  }
  return numero;
}

/**
 * Prepara i dati che il gate del cedolino archiviabile deve poter distinguere da un dato assente.
 * In particolare, una stringa vuota non viene mai convertita implicitamente nel numero zero.
 */
export function preparaDatiGateRapporto(form: DatiGateRapportoForm): EsitoDatiGateRapporto {
  const errori: string[] = [];
  const regione = form.regione.trim();
  const codiceCatastaleComune = form.codiceCatastaleComune.trim().toUpperCase();
  const dataDecorrenzaDomicilioFiscale = form.dataDecorrenzaDomicilioFiscale.trim();
  const riferimentoProgressiviFiscaliPregressi =
    form.riferimentoProgressiviFiscaliPregressi.trim();

  if (!regione) errori.push("Indica la regione del domicilio fiscale.");
  if (!/^[A-Z]\d{3}$/.test(codiceCatastaleComune)) {
    errori.push("Indica un codice catastale valido di quattro caratteri (per esempio F205).");
  }
  if (!dataIsoValida(dataDecorrenzaDomicilioFiscale)) {
    errori.push("Indica una decorrenza valida del domicilio fiscale.");
  }
  if (!riferimentoProgressiviFiscaliPregressi) {
    errori.push("Indica la fonte dei progressivi fiscali o l'attestazione di assenza di pregressi.");
  }

  const rolResiduiIniziali = leggiNumeroEsplicito(
    form.rolResiduiIniziali,
    "Saldo iniziale ROL",
    errori,
  );
  const exFestivitaResidueIniziali = leggiNumeroEsplicito(
    form.exFestivitaResidueIniziali,
    "Saldo iniziale ex festività",
    errori,
  );
  const addizionaleRegionaleConguaglioAnnuo = leggiNumeroEsplicito(
    form.addizionaleRegionaleConguaglioAnnuo,
    "Conguaglio addizionale regionale",
    errori,
    true,
  );
  const addizionaleComunaleSaldoAnnuo = leggiNumeroEsplicito(
    form.addizionaleComunaleSaldoAnnuo,
    "Saldo addizionale comunale",
    errori,
    true,
  );
  const addizionaleComunaleAccontoAnnuo = leggiNumeroEsplicito(
    form.addizionaleComunaleAccontoAnnuo,
    "Acconto addizionale comunale",
    errori,
    true,
  );

  if (errori.length > 0
    || rolResiduiIniziali === undefined
    || exFestivitaResidueIniziali === undefined
    || addizionaleRegionaleConguaglioAnnuo === undefined
    || addizionaleComunaleSaldoAnnuo === undefined
    || addizionaleComunaleAccontoAnnuo === undefined) {
    return { ok: false, errori };
  }

  return {
    ok: true,
    valore: {
      regione,
      codiceCatastaleComune,
      dataDecorrenzaDomicilioFiscale,
      riferimentoProgressiviFiscaliPregressi,
      rolResiduiIniziali,
      exFestivitaResidueIniziali,
      addizionaleRegionaleConguaglioAnnuo,
      addizionaleComunaleSaldoAnnuo,
      addizionaleComunaleAccontoAnnuo,
    },
  };
}
