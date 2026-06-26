using Paghe.Core.Calcolo;
using Paghe.Core.Contabilita;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;

// Demo del motore di calcolo cedolino — CCNL Commercio (valori di esempio).
// I parametri normativi sono ILLUSTRATIVI: vanno validati con un Consulente del Lavoro.

var parametri = Seed.Parametri2026();
var ccnl = Seed.CcnlCommercio2026();
var motore = new MotoreCalcoloCedolino(parametri, ccnl);

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== DEMO MOTORE CALCOLO CEDOLINO — valori di esempio, non certificati ===\n");

// --- Caso 1: impiegato full-time, 3° livello, Milano, 2 scatti ---------------------
var rapporto1 = new RapportoLavoro
{
    CodiceCcnl = "COMMERCIO_TERZIARIO",
    Livello = "3",
    Qualifica = Qualifica.Impiegato,
    NumeroMensilita = 14,
    ScattiAnzianita = 2,
    Regione = "Lombardia",
    CodiceCatastaleComune = "F205", // Milano
};
var cartellino1 = new Cartellino
{
    Anno = 2026, Mese = 6,
    OreStraordinario = 8m,
};
var cedolino1 = motore.Calcola(rapporto1, cartellino1);
Console.WriteLine(StampaCedolino.Testo(cedolino1,
    "Mario Rossi — Impiegato 3° liv. full-time (Commercio)"));

// --- Caso 2: impiegato part-time 50%, 5° livello, Roma, nessuno scatto -------------
var rapporto2 = new RapportoLavoro
{
    CodiceCcnl = "COMMERCIO_TERZIARIO",
    Livello = "5",
    Qualifica = Qualifica.Impiegato,
    NumeroMensilita = 13,
    PartTime = true,
    PercentualePartTime = 50m,
    Regione = "Lazio",
    CodiceCatastaleComune = "H501", // Roma
};
var cartellino2 = new Cartellino { Anno = 2026, Mese = 6 };
var cedolino2 = motore.Calcola(rapporto2, cartellino2);
Console.WriteLine(StampaCedolino.Testo(cedolino2,
    "Giulia Bianchi — Impiegata 5° liv. part-time 50% (Commercio)"));

// --- Bilancio SRL riclassificato (schema CEE) su piano conti e saldi di esempio -----
var bilancio = RiclassificazioneBilancio.Riclassifica(
    SeedPianoConti.PianoConti(), SeedPianoConti.SaldiEsempio());
Console.WriteLine(StampaBilancio.Testo(bilancio, "Bilancio SRL di esempio — esercizio 2026"));
