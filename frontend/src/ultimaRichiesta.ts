/**
 * Invalida le risposte asincrone precedenti quando cambia lo scope della schermata.
 * Serve a impedire che una risposta tardiva mostri record appartenenti al dipendente
 * selezionato in precedenza.
 */
export function creaSequenziatoreRichieste() {
  let generazione = 0;
  let controller: AbortController | null = null;

  return {
    apri() {
      controller?.abort();
      controller = new AbortController();
      const propriaGenerazione = ++generazione;
      const proprioController = controller;
      return {
        signal: proprioController.signal,
        attiva: () => generazione === propriaGenerazione && !proprioController.signal.aborted,
        invalida: () => {
          if (generazione === propriaGenerazione) {
            generazione += 1;
            proprioController.abort();
            if (controller === proprioController) controller = null;
          }
        },
      };
    },
    invalida() {
      generazione += 1;
      controller?.abort();
      controller = null;
    },
  };
}
