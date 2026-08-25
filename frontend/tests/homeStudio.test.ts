import assert from "node:assert/strict";
import test from "node:test";
import {
  chiaveStorageTenant,
  dimenticaAziendaCorrente,
  leggiAziendaCorrenteId,
  leggiUltimaAziendaId,
  salvaAziendaCorrenteId,
  scegliAziendaPreferita,
} from "../src/aziendaCorrente.ts";
import {
  aggiornaStrumentiRecenti,
  sezioneRichiedeAzienda,
  type StrumentoRecente,
} from "../src/homeStudio.ts";

class MemoriaStorage {
  private readonly valori = new Map<string, string>();

  getItem(chiave: string): string | null {
    return this.valori.get(chiave) ?? null;
  }

  setItem(chiave: string, valore: string): void {
    this.valori.set(chiave, valore);
  }

  removeItem(chiave: string): void {
    this.valori.delete(chiave);
  }

  clear(): void {
    this.valori.clear();
  }
}

const locale = new MemoriaStorage();
const sessione = new MemoriaStorage();
Object.defineProperty(globalThis, "localStorage", { value: locale, configurable: true });
Object.defineProperty(globalThis, "sessionStorage", { value: sessione, configurable: true });

function impostaTenant(tenantId: string): void {
  locale.setItem("tenant_corrente", tenantId);
}

test("la scelta azienda non ricade mai silenziosamente sul primo cliente", () => {
  locale.clear();
  sessione.clear();
  impostaTenant("studio-a");

  assert.equal(scegliAziendaPreferita([{ id: "azienda-1" }, { id: "azienda-2" }]), null);
});

test("azienda corrente e ultima azienda sono isolate per tenant", () => {
  locale.clear();
  sessione.clear();

  impostaTenant("studio-a");
  salvaAziendaCorrenteId("azienda-a");
  assert.equal(leggiAziendaCorrenteId(), "azienda-a");
  assert.equal(leggiUltimaAziendaId(), "azienda-a");
  assert.equal(chiaveStorageTenant("prova"), "prova:studio-a");

  impostaTenant("studio-b");
  assert.equal(leggiAziendaCorrenteId(), null);
  assert.equal(leggiUltimaAziendaId(), null);
  salvaAziendaCorrenteId("azienda-b");

  impostaTenant("studio-a");
  assert.equal(leggiAziendaCorrenteId(), "azienda-a");
  dimenticaAziendaCorrente();
  assert.equal(leggiAziendaCorrenteId(), null);
  assert.equal(leggiUltimaAziendaId(), "azienda-a");
});

test("il gate distingue le schermate aziendali dalla Home studio", () => {
  assert.equal(sezioneRichiedeAzienda("dipendenti"), true);
  assert.equal(sezioneRichiedeAzienda("cedolini"), true);
  assert.equal(sezioneRichiedeAzienda("agricoltura"), true);
  assert.equal(sezioneRichiedeAzienda("dashboard"), false);
  assert.equal(sezioneRichiedeAzienda("aziende"), false);
  assert.equal(sezioneRichiedeAzienda("pratiche"), false);
  assert.equal(sezioneRichiedeAzienda("elaborazioni"), false);
  assert.equal(sezioneRichiedeAzienda("calcolo"), false);
  assert.equal(sezioneRichiedeAzienda("adempimenti"), false);
  assert.equal(sezioneRichiedeAzienda("contabilita"), false);
  assert.equal(sezioneRichiedeAzienda("documenti"), false);
  assert.equal(sezioneRichiedeAzienda("f24"), false);
  assert.equal(sezioneRichiedeAzienda("scadenzario"), false);
});

test("gli strumenti recenti sono deduplicati, limitati e privi di aziende non valide", () => {
  const correnti: StrumentoRecente[] = [
    { sezione: "dipendenti", aziendaId: "azienda-a", usatoAlle: 10 },
    { sezione: "fatture", aziendaId: "rimossa", usatoAlle: 9 },
    { sezione: "presenze", aziendaId: "azienda-b", usatoAlle: 8 },
    { sezione: "cedolini", aziendaId: "azienda-a", usatoAlle: 7 },
    { sezione: "lipe", aziendaId: "azienda-b", usatoAlle: 6 },
  ];

  const aggiornati = aggiornaStrumentiRecenti(
    correnti,
    "dipendenti",
    "azienda-a",
    new Set(["azienda-a", "azienda-b"]),
    100,
  );

  assert.deepEqual(aggiornati, [
    { sezione: "dipendenti", aziendaId: "azienda-a", usatoAlle: 100 },
    { sezione: "presenze", aziendaId: "azienda-b", usatoAlle: 8 },
    { sezione: "cedolini", aziendaId: "azienda-a", usatoAlle: 7 },
    { sezione: "lipe", aziendaId: "azienda-b", usatoAlle: 6 },
  ]);
});
