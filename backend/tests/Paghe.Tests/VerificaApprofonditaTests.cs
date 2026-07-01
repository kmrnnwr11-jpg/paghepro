using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica approfondita "a tappeto" del motore cedolino, con formule di controllo
/// <b>riscritte in modo indipendente nel test</b> (scaglioni IRPEF, aliquote INPS,
/// addizionali) e invarianti contabili su tutti i livelli, qualifiche, regioni e i 12 mesi.
/// Scopo: dare confidenza che i conti tornino in tanti scenari diversi, non su un solo caso.
/// </summary>
public sealed class VerificaApprofonditaTests
{
    private static readonly ParametriNormativi P = Seed.Parametri2026();
    private static readonly Ccnl C = Seed.CcnlCommercio2026();
    private static MotoreCalcoloCedolino Motore() => new(P, C);

    private static readonly string[] Livelli = { "Quadro", "1", "2", "3", "4", "5", "6", "7" };

    private static RapportoLavoro Rapporto(
        string livello, Qualifica q = Qualifica.Impiegato,
        string regione = "", string comune = "") => new()
    {
        CodiceCcnl = "COMMERCIO_TERZIARIO",
        Livello = livello,
        Qualifica = q,
        NumeroMensilita = 14,
        Regione = regione,
        CodiceCatastaleComune = comune,
    };

    // ---- Formule di controllo indipendenti (scritte a mano qui nel test) ------------------

    /// <summary>IRPEF lorda annua ricalcolata con gli scaglioni 2026 cablati a mano.</summary>
    private static decimal IrpefAttesa(decimal reddito)
    {
        if (reddito <= 0m) return 0m;
        decimal i = Math.Min(reddito, 28000m) * 0.23m;
        if (reddito > 28000m) i += (Math.Min(reddito, 50000m) - 28000m) * 0.33m;
        if (reddito > 50000m) i += (reddito - 50000m) * 0.43m;
        return i;
    }

    private static decimal AliquotaInpsDip(Qualifica q) => q == Qualifica.Apprendista ? 0.0584m : 0.0919m;

    // =======================================================================================
    // 1) IRPEF a scaglioni: il motore = ricalcolo indipendente, su tanti redditi e ai confini
    // =======================================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    [InlineData(14613)]    // il nostro impiegato liv.3
    [InlineData(27999.99)]
    [InlineData(28000)]    // confine 23% -> 33%
    [InlineData(28000.01)]
    [InlineData(35000)]
    [InlineData(49999.99)]
    [InlineData(50000)]    // confine 33% -> 43%
    [InlineData(50000.01)]
    [InlineData(75000)]
    [InlineData(120000)]
    public void Irpef_AScaglioni_CoincideConRicalcoloIndipendente(double redditoD)
    {
        decimal reddito = (decimal)redditoD;
        decimal motore = CalcoloIrpef.IrpefLordaAnnua(reddito, P.ScaglioniIrpef);
        Assert.Equal(IrpefAttesa(reddito), motore, 4);
    }

    [Theory]
    [InlineData(10000, 20000)]
    [InlineData(27000, 29000)]
    [InlineData(49000, 51000)]
    [InlineData(60000, 200000)]
    public void Irpef_Monotona_PiuRedditoNonMenoImposta(double basso, double alto)
    {
        decimal iBasso = CalcoloIrpef.IrpefLordaAnnua((decimal)basso, P.ScaglioniIrpef);
        decimal iAlto = CalcoloIrpef.IrpefLordaAnnua((decimal)alto, P.ScaglioniIrpef);
        Assert.True(iAlto >= iBasso, $"IRPEF non monotòna: {alto}->{iAlto} < {basso}->{iBasso}");
    }

    // =======================================================================================
    // 2) Invarianti contabili su OGNI livello, più regioni, tutti i 12 mesi
    // =======================================================================================
    [Fact]
    public void Invarianti_SuTuttiLivelliRegioniEMesi()
    {
        var residenze = new[]
        {
            ("Lombardia", "F205"), // Milano
            ("Lazio", "H501"),     // Roma
            ("Campania", "F839"),  // Napoli
            ("", ""),              // senza residenza fiscale impostata
        };

        int scenari = 0;
        foreach (var liv in Livelli)
        foreach (var (reg, com) in residenze)
        for (int mese = 1; mese <= 12; mese++)
        {
            var c = Motore().Calcola(Rapporto(liv, Qualifica.Impiegato, reg, com),
                new Cartellino { Anno = 2026, Mese = mese });
            scenari++;

            // (a) netto = competenze - trattenute, sempre
            Assert.Equal(c.TotaleCompetenze - c.TotaleTrattenute, c.NettoInBusta);
            // (b) trattenute = somma esatta delle componenti
            Assert.Equal(
                c.ContributiDipendente + c.IrpefNetta + c.AddizionaleRegionale + c.AddizionaleComunale,
                c.TotaleTrattenute);
            // (c) costo azienda = competenze + contributi datore + TFR
            Assert.Equal(
                Arrotondamento.Euro(c.TotaleCompetenze + c.ContributiDatore + c.QuotaTfr),
                c.CostoAzienda);
            // (d) niente importi assurdi
            Assert.True(c.IrpefNetta >= 0m, "IRPEF netta mai negativa");
            Assert.True(c.NettoInBusta > 0m && c.NettoInBusta < c.TotaleCompetenze, "Netto tra 0 e lordo");
            Assert.True(c.ContributiDatore > c.ContributiDipendente, "Datore > dipendente");
            // (e) addizionali presenti solo se c'è la residenza fiscale
            if (reg == "") Assert.Equal(0m, c.AddizionaleRegionale);
            if (com == "") Assert.Equal(0m, c.AddizionaleComunale);
            if (reg != "") Assert.True(c.AddizionaleRegionale > 0m, "Con regione, addizionale > 0");
            if (com != "") Assert.True(c.AddizionaleComunale > 0m, "Con comune, addizionale > 0");
        }

        Assert.Equal(8 * 4 * 12, scenari); // 384 cedolini verificati
    }

    // =======================================================================================
    // 3) INPS c/dipendente per qualifica = imponibile * aliquota (ricalcolo indipendente)
    // =======================================================================================
    [Theory]
    [InlineData(Qualifica.Operaio)]
    [InlineData(Qualifica.Impiegato)]
    [InlineData(Qualifica.Quadro)]
    [InlineData(Qualifica.Dirigente)]
    [InlineData(Qualifica.Apprendista)]
    public void Inps_Dipendente_PerQualifica(Qualifica q)
    {
        var c = Motore().Calcola(Rapporto("3", q), new Cartellino { Anno = 2026, Mese = 6 });
        decimal atteso = Arrotondamento.Euro(c.ImponibileInps * AliquotaInpsDip(q));
        Assert.Equal(atteso, c.ContributiDipendente);
    }

    // =======================================================================================
    // 4) ADDIZIONALI su ANNO INTERO: ogni mese = ricalcolo indipendente, somma 12 mesi coerente
    // =======================================================================================
    [Theory]
    [InlineData("Lombardia", "F205")] // Milano
    [InlineData("Lazio", "H501")]     // Roma
    [InlineData("Campania", "F839")]  // Napoli
    [InlineData("Veneto", "L736")]    // Venezia
    public void Addizionali_AnnoIntero_CoerentiConAliquote(string regione, string comune)
    {
        decimal aliqReg = P.AddizionaleRegionale[regione];
        decimal aliqCom = P.AddizionaleComunale[comune];

        decimal sommaReg = 0m, sommaCom = 0m;
        decimal redditoAnnuo = 0m;

        for (int mese = 1; mese <= 12; mese++)
        {
            var c = Motore().Calcola(Rapporto("3", Qualifica.Impiegato, regione, comune),
                new Cartellino { Anno = 2026, Mese = mese });

            redditoAnnuo = c.ImponibileIrpef * 12m; // base presuntiva (uguale ogni mese)
            decimal attesoReg = Arrotondamento.Euro(redditoAnnuo * aliqReg / 12m);
            decimal attesoCom = Arrotondamento.Euro(redditoAnnuo * aliqCom / 12m);

            Assert.Equal(attesoReg, c.AddizionaleRegionale);
            Assert.Equal(attesoCom, c.AddizionaleComunale);

            sommaReg += c.AddizionaleRegionale;
            sommaCom += c.AddizionaleComunale;
        }

        // La somma dei 12 mesi ricade sull'addizionale annua dovuta (tolleranza arrotondamenti).
        Assert.True(Math.Abs(sommaReg - redditoAnnuo * aliqReg) <= 0.20m,
            $"Add. regionale annua: somma 12 mesi {sommaReg:F2} vs attesa {redditoAnnuo * aliqReg:F2}");
        Assert.True(Math.Abs(sommaCom - redditoAnnuo * aliqCom) <= 0.20m,
            $"Add. comunale annua: somma 12 mesi {sommaCom:F2} vs attesa {redditoAnnuo * aliqCom:F2}");
    }

    [Fact]
    public void Addizionali_ZeroSenzaResidenzaFiscale()
    {
        var c = Motore().Calcola(Rapporto("3"), new Cartellino { Anno = 2026, Mese = 6 });
        Assert.Equal(0m, c.AddizionaleRegionale);
        Assert.Equal(0m, c.AddizionaleComunale);
    }

    // =======================================================================================
    // 5) Incapienza: con reddito molto basso l'IRPEF netta è 0, mai negativa
    // =======================================================================================
    [Fact]
    public void Incapienza_RedditoBasso_IrpefNettaZero()
    {
        var partTimeBasso = new RapportoLavoro
        {
            CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "7", Qualifica = Qualifica.Impiegato,
            NumeroMensilita = 14, PartTime = true, PercentualePartTime = 30m,
        };
        var c = Motore().Calcola(partTimeBasso, new Cartellino { Anno = 2026, Mese = 6 });
        Assert.Equal(0m, c.IrpefNetta);
        Assert.True(c.NettoInBusta > 0m);
    }

    // =======================================================================================
    // 6) Coerenza annuale (come la CU): 12 mensilità uguali sommano a 12x il singolo mese
    // =======================================================================================
    [Fact]
    public void CoerenzaAnnuale_SommaDodiciMesi_AlimentaLaCu()
    {
        var rapporto = Rapporto("3", Qualifica.Impiegato, "Lombardia", "F205");
        decimal impAnnuo = 0m, ritAnnue = 0m, nettoAnnuo = 0m;

        for (int mese = 1; mese <= 12; mese++)
        {
            var c = Motore().Calcola(rapporto, new Cartellino { Anno = 2026, Mese = mese });
            impAnnuo += c.ImponibileIrpef;
            ritAnnue += c.IrpefNetta;
            nettoAnnuo += c.NettoInBusta;
        }

        var unMese = Motore().Calcola(rapporto, new Cartellino { Anno = 2026, Mese = 6 });
        Assert.Equal(unMese.ImponibileIrpef * 12m, impAnnuo);
        Assert.Equal(unMese.IrpefNetta * 12m, ritAnnue);
        Assert.Equal(unMese.NettoInBusta * 12m, nettoAnnuo);
        Assert.True(impAnnuo > 0m && ritAnnue >= 0m && nettoAnnuo > 0m);
    }
}
