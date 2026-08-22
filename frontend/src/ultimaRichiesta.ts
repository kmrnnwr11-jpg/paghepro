/**
 * Invalida le risposte asincrone precedenti quando cambia lo scope della schermata.
 * Serve a impedire che una risposta tardiva mostri record appartenenti al dipendente
 * selezionato in precedenza.
 */
export function creaSequenziatoreRichieste() {
  let generazione = 0;

  return {
    apri() {
      const propriaGenerazione = ++generazione;
      return {
        attiva: () => generazione === propriaGenerazione,
        invalida: () => {
          if (generazione === propriaGenerazione) generazione += 1;
        },
      };
    },
  };
}
