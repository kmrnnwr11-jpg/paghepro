import assert from "node:assert/strict";
import test from "node:test";
import {
  calendarioIniziale,
  applicaProgressiviSettimanali,
  cambiaCausaleGiornata,
  festivitaNazionali,
  validaCalendarioArchiviabile,
} from "../src/cartellinoCalendario.ts";

test("le festività nazionali dell'anno sono undici, Pasquetta compresa e calcolata", () => {
  const feste2026 = festivitaNazionali(2026);
  assert.equal(feste2026.size, 11);
  assert.equal(feste2026.get("2026-01-01"), "Capodanno");
  assert.equal(feste2026.get("2026-08-15"), "Ferragosto");
  assert.equal(feste2026.get("2026-12-25"), "Natale");
  // Pasqua 2026 = 5 aprile, quindi il lunedì dell'Angelo è il 6.
  assert.equal(feste2026.get("2026-04-06"), "Lunedì dell'Angelo");
  // Pasqua 2027 = 28 marzo: la mobile cambia anno per anno.
  assert.equal(festivitaNazionali(2027).get("2027-03-29"), "Lunedì dell'Angelo");
});

test("dicembre 2026 nasce con Natale e Immacolata già segnati come festività", () => {
  const calendario = calendarioIniziale(2026, 12, 8);
  const perData = new Map(calendario.map((giornata) => [giornata.data, giornata]));

  // 8 dicembre (martedì) e 25 dicembre (venerdì): festivi senza ore ordinarie.
  assert.equal(perData.get("2026-12-08")?.causale, "Festivita");
  assert.equal(perData.get("2026-12-08")?.oreOrdinarie, 0);
  assert.equal(perData.get("2026-12-25")?.causale, "Festivita");
  // Santo Stefano cade di sabato: resta riposo settimanale, non si sovrappone.
  assert.equal(perData.get("2026-12-26")?.causale, "RiposoSettimanale");
  // Un giorno lavorativo qualunque resta precompilato dall'orario.
  assert.equal(perData.get("2026-12-09")?.causale, "Lavorata");
  assert.equal(perData.get("2026-12-09")?.oreOrdinarie, 8);
});

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
