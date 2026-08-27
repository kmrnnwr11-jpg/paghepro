import assert from "node:assert/strict";
import test from "node:test";
import {
  applicaDatiVisura, aziendaDaProposta, campiDaCompletareAzienda, codiceFiscaleValido,
  datiContributiviMancanti, parsaTestoAzienda, partitaIvaValida, validaAzienda,
} from "../src/aziendaForm.ts";
import type { NuovaAzienda } from "../src/api.ts";

const aziendaBase: NuovaAzienda = {
  ragioneSociale: "Società Agricola Esempio S.r.l.",
  partitaIva: "12345670017",
  codiceFiscale: "12345670017",
  indirizzo: "Via del Campo 26",
  cap: "52100",
  comune: "Arezzo",
  provincia: "AR",
  settore: "AGRICOLTURA",
  codiceAteco: "01.46.00",
  tipoContribuente: "Societa",
  haDipendenti: true,
  assoggettataIva: true,
};

test("estrae il riepilogo societario con sede senza la parola CAP", () => {
  const dati = parsaTestoAzienda(`
    Denominazione: SOCIETA' AGRICOLA ESEMPIO S.R.L.
    Codice Fiscale / Partita IVA: 12345678901
    Sede Legale: VIA DEL CAMPO 26, 52100 AREZZO (AR)
    PEC: esempio@legalmail.it
    Attività Prevalente: Allevamento (Codice ATECO: 01.46.00)
    Amministratore Delegato: BIANCHI LUCA (nato a Cremona, C.F. BNCLCU91L11D150X)
    Data di assunzione: Dal 01/09/2023
  `);

  assert.equal(dati.ragioneSociale, "SOCIETA' AGRICOLA ESEMPIO S.R.L.");
  assert.equal(dati.partitaIva, "12345678901");
  assert.equal(dati.codiceFiscale, "12345678901");
  assert.equal(dati.indirizzo, "VIA DEL CAMPO 26");
  assert.equal(dati.cap, "52100");
  assert.equal(dati.comune, "AREZZO");
  assert.equal(dati.provincia, "AR");
  assert.equal(dati.pec, "esempio@legalmail.it");
  assert.equal(dati.email, undefined);
  assert.equal(dati.codiceAteco, "01.46.00");
  assert.equal(dati.settore, "AGRICOLTURA");
  assert.equal(dati.tipoContribuente, "Societa");
  assert.equal(dati.ccnlDefault, undefined);
  assert.equal(dati.livelloDefault, undefined);
  assert.equal(dati.legaleRappresentanteCognome, "BIANCHI");
  assert.equal(dati.legaleRappresentanteNome, "LUCA");
  assert.equal(dati.legaleRappresentanteCodiceFiscale, "BNCLCU91L11D150X");
  assert.equal(dati.haDipendenti, true);
});

test("il caricamento PDF conserva anche codice fiscale ATECO e rappresentante", () => {
  const risultato = applicaDatiVisura(aziendaBase, {
    ragioneSociale: "Società Agricola Dimostrativa S.r.l.",
    codiceFiscale: "10987654321",
    codiceAteco: "01.47.00",
    legaleRappresentante: "VERDI ANNA (nata a Firenze, C.F. VRDNNA80A01D612X)",
  });

  assert.equal(risultato.codiceFiscale, "10987654321");
  assert.equal(risultato.codiceAteco, "01.47.00");
  assert.equal(risultato.settore, "AGRICOLTURA");
  assert.equal(risultato.legaleRappresentanteCognome, "VERDI");
  assert.equal(risultato.legaleRappresentanteNome, "ANNA");
});

test("l'ATECO agricolo non inventa la forma giuridica", () => {
  const risultato = parsaTestoAzienda(`
    Denominazione: AZIENDA AGRICOLA DIMOSTRATIVA
    Codice Fiscale / Partita IVA: 12345678901
    Codice ATECO: 01.46.00
  `);

  assert.equal(risultato.settore, "AGRICOLTURA");
  assert.equal(risultato.tipoContribuente, undefined);
});

test("la modalità prova allenta solo i dati contributivi", () => {
  const ordinaria = validaAzienda(aziendaBase);
  assert.ok(ordinaria.matricolaInps);
  assert.ok(ordinaria.codiceSedeInps);
  assert.ok(ordinaria.cscInps);
  assert.ok(ordinaria.patInail);

  const provvisoria = validaAzienda(aziendaBase, { datiContributiviProvvisori: true });
  assert.equal(provvisoria.matricolaInps, undefined);
  assert.equal(provvisoria.patInail, undefined);
  assert.equal(provvisoria.ragioneSociale, undefined);
  assert.equal(provvisoria.codiceAteco, undefined);

  const senzaSede = validaAzienda({ ...aziendaBase, comune: "" }, { datiContributiviProvvisori: true });
  assert.ok(senzaSede.comune);

  const conZeri = validaAzienda({ ...aziendaBase, matricolaInps: "000000", cscInps: "00000" }, { datiContributiviProvvisori: true });
  assert.match(conZeri.matricolaInps ?? "", /Non usare 000000/);
  assert.match(conZeri.cscInps ?? "", /Non usare 00000/);
});

test("controlla il checksum di Partita IVA e Codice Fiscale", () => {
  assert.equal(partitaIvaValida("12345670017"), true);
  assert.equal(partitaIvaValida("12345670018"), false);
  assert.equal(partitaIvaValida("12345678901"), false);
  assert.equal(codiceFiscaleValido("12345670017"), true);
  assert.equal(codiceFiscaleValido("RSSMRA80A01H501U"), true);
  assert.equal(codiceFiscaleValido("RSSMRA80A01H501X"), false);
  assert.equal(codiceFiscaleValido("BNCGLI93B41F205X"), false);

  const errori = validaAzienda({
    ...aziendaBase,
    partitaIva: "12345670018",
    codiceFiscale: "RSSMRA80A01H501X",
  }, { datiContributiviProvvisori: true });
  assert.match(errori.partitaIva ?? "", /checksum/);
  assert.match(errori.codiceFiscale ?? "", /checksum/);
});

test("datiContributiviMancanti elenca i codici assenti solo per le aziende con dipendenti", () => {
  // Azienda senza dipendenti: nessun adempimento contributivo, nessun avviso.
  assert.deepEqual(datiContributiviMancanti({ haDipendenti: false }), []);

  // Con dipendenti e anagrafica vuota: tutti i codici che bloccano F24 e UniEmens.
  assert.deepEqual(datiContributiviMancanti({ haDipendenti: true }), [
    "matricola INPS", "codice sede INPS", "CSC INPS", "P.A.T. INAIL", "CCNL", "livello CCNL",
    "codici autorizzazione INPS / NESSUNO", "FIS o altro fondo / NESSUNO",
    "agevolazione / NESSUNA", "aliquota INPS dipendente", "aliquota INPS datore",
    "fonte profilo contributivo",
  ]);

  const profiloCompleto = {
    codiciAutorizzazioneInps: "NESSUNO",
    codiceFondoIntegrazioneSalariale: "FIS",
    codiceAgevolazioneContributiva: "NESSUNA",
    aliquotaInpsDipendente: 0.0949,
    aliquotaInpsDatore: 0.2788,
    fonteProfiloContributivo: "Prospetto consulente 2026-07",
  };

  // Completa: l'archivio non deve segnalare nulla.
  assert.deepEqual(datiContributiviMancanti({
    ...profiloCompleto,
    haDipendenti: true,
    matricolaInps: "8900000010", codiceSedeInps: "0001", cscInps: "11801",
    patInail: "80000103", ccnlDefault: "COMMERCIO", livelloDefault: "3",
  }), []);

  // Gli spazi non contano come valore inserito.
  assert.deepEqual(datiContributiviMancanti({
    ...profiloCompleto,
    haDipendenti: true,
    matricolaInps: "   ", codiceSedeInps: "0001", cscInps: "11801",
    patInail: "80000103", ccnlDefault: "COMMERCIO", livelloDefault: "3",
  }), ["matricola INPS"]);
});

test("aziendaDaProposta fonde proposta del server e parser locale senza inventare nulla", () => {
  const form = aziendaDaProposta(
    {
      ragioneSociale: "JOBTIME SRLS",
      partitaIva: "12345678903",
      codiceFiscale: "12345678903",
      indirizzo: "VIA FRANCESCO BARACCA 19",
      cap: "43036",
      comune: "FIDENZA",
      provincia: "pr",
    },
    [
      "**Denominazione**: JOBTIME SRLS",
      "**PEC**: jobtime.srls@pec.it",
      "**Sede legale**: Via Francesco Baracca n. 19 - 43036 Fidenza (PR)",
    ].join("\n"),
  );

  // La proposta del server vince; il parser locale aggiunge solo ciò che il server non ha.
  assert.equal(form.ragioneSociale, "JOBTIME SRLS");
  assert.equal(form.partitaIva, "12345678903");
  assert.equal(form.indirizzo, "VIA FRANCESCO BARACCA 19");
  assert.equal(form.provincia, "PR");
  assert.equal(form.pec, "jobtime.srls@pec.it");
  // «SRLS» senza punti è una società: il tipo contribuente si deduce dalla ragione sociale.
  assert.equal(form.tipoContribuente, "Societa");
  // Nessun dato contributivo inventato: restano vuoti, da completare in scheda.
  assert.equal(form.matricolaInps, undefined);
  assert.equal(form.ccnlDefault, undefined);
  assert.equal(form.haDipendenti, true);
});

test("campiDaCompletareAzienda elenca i campi vuoti che servono per lavorare", () => {
  const campi = campiDaCompletareAzienda({
    id: "a1",
    ragioneSociale: "JOBTIME SRLS",
    partitaIva: "12345678903",
    indirizzo: "VIA FRANCESCO BARACCA 19",
    cap: "43036",
    comune: "FIDENZA",
    provincia: "PR",
    haDipendenti: true,
  });

  assert.deepEqual(campi, [
    "codiceCatastale", "pec", "settore", "codiceAteco", "tipoContribuente",
    "matricolaInps", "codiceSedeInps", "cscInps", "patInail", "ccnlDefault", "livelloDefault",
  ]);

  // Senza dipendenti i codici contributivi non vengono segnalati.
  const senzaDipendenti = campiDaCompletareAzienda({
    id: "a2",
    ragioneSociale: "Studio Prova",
    pec: "studio@pec.it",
    codiceCatastale: "B034",
    settore: "COMMERCIO_SERVIZI",
    codiceAteco: "47.91.10",
    tipoContribuente: "Societa",
    indirizzo: "Via Roma 1",
    cap: "43036",
    comune: "Fidenza",
    provincia: "PR",
    haDipendenti: false,
  });
  assert.deepEqual(senzaDipendenti, []);
});
