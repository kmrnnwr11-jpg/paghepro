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

// --- Caso 3: ANNO COMPLETO — 12 buste paga → Certificazione Unica → conguaglio -------
// Simula la prova "dodici mesi + CU" per un dipendente, tutto in memoria (valori di esempio).
Console.WriteLine("\n=== ANNO COMPLETO 2026 — 12 buste → CU → conguaglio (esempio) ===\n");

var rapportoAnno = new RapportoLavoro
{
    CodiceCcnl = "COMMERCIO_TERZIARIO",
    Livello = "3",
    Qualifica = Qualifica.Impiegato,
    NumeroMensilita = 14,
    ScattiAnzianita = 1,
    Regione = "Lombardia",
    CodiceCatastaleComune = "F205", // Milano
};

decimal impIrpef = 0m, ritenuteIrpef = 0m, addReg = 0m, addCom = 0m, netto = 0m, tfr = 0m;
Console.WriteLine("Mese  Netto in busta   IRPEF netta");
for (int mese = 1; mese <= 12; mese++)
{
    var c = motore.Calcola(rapportoAnno, new Cartellino { Anno = 2026, Mese = mese });
    impIrpef += c.ImponibileIrpef;
    ritenuteIrpef += c.IrpefNetta;
    addReg += c.AddizionaleRegionale;
    addCom += c.AddizionaleComunale;
    netto += c.NettoInBusta;
    tfr += c.QuotaTfr;
    Console.WriteLine($" {mese,2}   {c.NettoInBusta,12:N2}   {c.IrpefNetta,10:N2}");
}

Console.WriteLine("\n--- CERTIFICAZIONE UNICA (CU) 2026 — totali certificati ---");
Console.WriteLine("Dipendente:                 Mario Rossi");
Console.WriteLine($"Imponibile IRPEF annuo:     {impIrpef,12:N2}");
Console.WriteLine($"Ritenute IRPEF operate:     {ritenuteIrpef,12:N2}");
Console.WriteLine($"Addizionale regionale:      {addReg,12:N2}");
Console.WriteLine($"Addizionale comunale:       {addCom,12:N2}");
Console.WriteLine($"TFR accantonato nell'anno:  {tfr,12:N2}");
Console.WriteLine($"Netto erogato nell'anno:    {netto,12:N2}");

// Conguaglio fiscale di fine anno: ricalcolo IRPEF annua vs trattenute mensili.
var detrazioneAnnua = CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(impIrpef, 365);
var conguaglio = ConguaglioFiscale.Calcola(impIrpef, ritenuteIrpef, detrazioneAnnua, parametri.ScaglioniIrpef);
Console.WriteLine("\n--- CONGUAGLIO FISCALE DI FINE ANNO ---");
Console.WriteLine($"IRPEF lorda annua:          {conguaglio.IrpefLordaAnnua,12:N2}");
Console.WriteLine($"Detrazioni lavoro dip.:     {conguaglio.DetrazioniSpettanti,12:N2}");
Console.WriteLine($"IRPEF netta dovuta:         {conguaglio.IrpefNettaAnnua,12:N2}");
Console.WriteLine($"Già trattenuta (12 mesi):   {conguaglio.IrpefGiaTrattenuta,12:N2}");
Console.WriteLine($"Conguaglio:                 {conguaglio.Conguaglio,12:N2}  ({(conguaglio.ADebito ? "a DEBITO" : "a CREDITO")})");

Console.WriteLine("\n(Valori di esempio, non certificati. La CU e i Redditi PF (\"Unico\") ufficiali si");
Console.WriteLine(" producono/controllano col software ufficiale dell'Agenzia Entrate e si inviano via Entratel.)");
