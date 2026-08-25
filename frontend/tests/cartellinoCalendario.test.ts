import assert from "node:assert/strict";
import test from "node:test";
import {
  calendarioIniziale,
  applicaProgressiviSettimanali,
  cambiaCausaleGiornata,
  validaCalendarioArchiviabile,
} from "../src/cartellinoCalendario.ts";

test("luglio 2026 precompila lunedi-venerdi senza trasformare il sabato in lavoro", () => {
  const calendario = calendarioIniziale(2026, 7, 8);

  assert.equal(calendario.length, 31);
  assert.equal(
    calendario.filter((giornata) => giornata.causale === "Lavorata")
      .reduce((totale, giornata) => totale + giornata.oreOrdinarie, 0),
    184,
  );

  const orePerSettimana = new Map<string, number>();
  for (const giornata of calendario) {
    const data = new Date(`${giornata.data}T00:00:00`);
    const fineSettimana = data.getDay() === 0 || data.getDay() === 6;
    assert.equal(giornata.causale, fineSettimana ? "RiposoSettimanale" : "Lavorata");
    assert.equal(giornata.oreOrdinarie, fineSettimana ? 0 : 8);

    const lunedi = new Date(data);
    lunedi.setDate(data.getDate() - ((data.getDay() + 6) % 7));
    const chiaveSettimana = lunedi.toISOString().slice(0, 10);
    orePerSettimana.set(
      chiaveSettimana,
      (orePerSettimana.get(chiaveSettimana) ?? 0) + giornata.oreOrdinarie,
    );
  }
  assert.ok([...orePerSettimana.values()].every((ore) => ore <= 40));
});

test("ogni causale di assenza incompatibile azzera tutte le ore di straordinario", () => {
  const [lavorata] = calendarioIniziale(2026, 7, 8);
  const conStraordinario = {
    ...lavorata,
    oreStraordinario: 2,
    oreStraordinarioNotturno: 1,
    oreStraordinarioFestivo: 0.5,
  };

  const incompatibili = [
    "Ferie",
    "Malattia",
    "Infortunio",
    "PermessoRetribuito",
    "ExFestivita",
    "AssenzaNonRetribuita",
  ] as const;
  for (const causale of incompatibili) {
    const cambiata = cambiaCausaleGiornata(conStraordinario, causale, 8);
    assert.equal(cambiata.causale, causale);
    assert.equal(cambiata.oreOrdinarie, 0);
    assert.equal(cambiata.oreStraordinario, 0);
    assert.equal(cambiata.oreStraordinarioNotturno, 0);
    assert.equal(cambiata.oreStraordinarioFestivo, 0);
  }
});

test("un calendario completo e coerente e archiviabile", () => {
  const conRiportoVerificato = applicaProgressiviSettimanali(
    calendarioIniziale(2026, 7, 8),
    16,
  );
  assert.deepEqual(validaCalendarioArchiviabile(2026, 7, conRiportoVerificato), []);
});

test("un mese iniziato a settimana avviata resta bloccato senza il riporto precedente", () => {
  const problemi = validaCalendarioArchiviabile(2026, 7, calendarioIniziale(2026, 7, 8));
  assert.ok(problemi.some((problema) => /progressivo verificabile/i.test(problema.messaggio)));
});

test("blocca calendario assente, incompleto e giornate con causali sovrapposte", () => {
  const assente = validaCalendarioArchiviabile(2026, 7, undefined);
  assert.equal(assente.length, 1);
  assert.match(assente[0].messaggio, /calendario giorno per giorno/i);

  const incompleto = calendarioIniziale(2026, 7, 8).slice(0, -1);
  assert.ok(validaCalendarioArchiviabile(2026, 7, incompleto)
    .some((problema) => /una sola riga per ogni giorno/i.test(problema.messaggio)));

  const sovrapposto = calendarioIniziale(2026, 7, 8);
  sovrapposto[0] = {
    ...sovrapposto[0],
    causale: "Ferie",
    oreOrdinarie: 8,
    oreStraordinario: 2,
  };
  const problemi = validaCalendarioArchiviabile(2026, 7, sovrapposto);
  assert.ok(problemi.some((problema) => /sovrappone ad altre ore/i.test(problema.messaggio)));
  assert.ok(problemi.some((problema) => /straordinario non puo sovrapporsi/i.test(problema.messaggio)));
  assert.ok(problemi.every((problema) => problema.bloccante));
});
