using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;

namespace Paghe.Tests.AdeTelematico;

public class F24DaCedoliniTests
{
    private static readonly DatiSostitutoImposta Sostituto =
        new("01234567897", "Acme Srl", MatricolaInps: "1234567890", CodiceSedeInps: "7100");

    [Fact]
    public void Costruisce_f24_da_due_paghe()
    {
        var righe = new List<RigaPagaPerF24>
        {
            new(RitenutaIrpef: 300m, AddizionaleRegionale: 20m, AddizionaleComunale: 10m,
                ContributiInpsDipendente: 150m, ContributiInpsDatore: 450m,
                CodiceRegione: "12", CodiceComuneCatastale: "F205"),
            new(RitenutaIrpef: 200m, AddizionaleRegionale: 15m, AddizionaleComunale: 5m,
                ContributiInpsDipendente: 100m, ContributiInpsDatore: 300m,
                CodiceRegione: "12", CodiceComuneCatastale: "F205"),
        };

        var f24 = F24DaCedolini.Costruisci(Sostituto, righe, annoPeriodo: 2026, mesePeriodo: 6);

        Assert.Equal(500m, f24.Erario.Single(e => e.CodiceTributo == "1001").ImportoDebito);
        Assert.Equal(35m, f24.Regioni.Single().ImportoDebito);          // 20 + 15
        Assert.Equal(15m, f24.TributiLocali.Single().ImportoDebito);    // 10 + 5
        Assert.Equal(1000m, f24.Inps.Single().ImportoDebito);           // 150+450+100+300
        Assert.Equal(1550m, f24.SaldoFinale);

        // L'F24 prodotto deve superare i controlli interni.
        Assert.True(ValidazioneF24.Verifica(f24).Valido);
    }

    [Fact]
    public void Estrae_riga_da_cedolino()
    {
        var cedolino = new Cedolino
        {
            Anno = 2026, Mese = 6,
            IrpefNetta = 250m, AddizionaleRegionale = 18m, AddizionaleComunale = 9m,
            ContributiDipendente = 140m, ContributiDatore = 420m,
        };

        var riga = RigaPagaPerF24.DaCedolino(cedolino, "12", "F205");

        Assert.Equal(250m, riga.RitenutaIrpef);
        Assert.Equal(18m, riga.AddizionaleRegionale);
        Assert.Equal(140m, riga.ContributiInpsDipendente);
        Assert.Equal(420m, riga.ContributiInpsDatore);
    }

    [Fact]
    public void Scadenza_versamento_e_il_16_del_mese_successivo()
    {
        Assert.Equal(new DateOnly(2026, 7, 16), F24DaCedolini.ScadenzaVersamento(2026, 6));
        Assert.Equal(new DateOnly(2027, 1, 16), F24DaCedolini.ScadenzaVersamento(2026, 12));
    }

    [Fact]
    public void Periodo_inps_valorizzato()
    {
        var righe = new List<RigaPagaPerF24>
        {
            new(RitenutaIrpef: 0m, AddizionaleRegionale: 0m, AddizionaleComunale: 0m,
                ContributiInpsDipendente: 100m, ContributiInpsDatore: 200m),
        };

        var f24 = F24DaCedolini.Costruisci(Sostituto, righe, annoPeriodo: 2026, mesePeriodo: 6);
        var inps = f24.Inps.Single();

        Assert.Equal("06/2026", inps.PeriodoDa);
        Assert.Equal("1234567890", inps.MatricolaCodice);
        Assert.Equal(300m, inps.ImportoDebito);
    }
}
