using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test del conguaglio fiscale IRPEF di fine anno.</summary>
public sealed class ConguaglioFiscaleTests
{
    private static readonly ScaglioneIrpef[] Scaglioni =
    [
        new(0m, 28000m, 0.23m),
        new(28000m, 50000m, 0.35m),
        new(50000m, null, 0.43m),
    ];

    [Fact]
    public void Calcola_ADebito()
    {
        // imponibile 30000 → lorda = 28000×23% + 2000×35% = 6440 + 700 = 7140
        // netta = 7140 - 1000 = 6140 ; conguaglio = 6140 - 5000 = +1140 (a debito)
        var r = ConguaglioFiscale.Calcola(30000m, irpefGiaTrattenuta: 5000m, detrazioniSpettanti: 1000m, Scaglioni);

        Assert.Equal(7140m, r.IrpefLordaAnnua);
        Assert.Equal(6140m, r.IrpefNettaAnnua);
        Assert.Equal(1140m, r.Conguaglio);
        Assert.True(r.ADebito);
    }

    [Fact]
    public void Calcola_ACredito()
    {
        // stessa netta 6140, ma trattenuta 7000 → conguaglio -860 (a credito)
        var r = ConguaglioFiscale.Calcola(30000m, irpefGiaTrattenuta: 7000m, detrazioniSpettanti: 1000m, Scaglioni);

        Assert.Equal(-860m, r.Conguaglio);
        Assert.False(r.ADebito);
    }

    [Fact]
    public void Calcola_DetrazioniSuperioriIrpef_NettaAzzerata()
    {
        // imponibile 5000 → lorda 1150 ; detrazioni 2000 → netta non negativa (0)
        var r = ConguaglioFiscale.Calcola(5000m, irpefGiaTrattenuta: 0m, detrazioniSpettanti: 2000m, Scaglioni);

        Assert.Equal(1150m, r.IrpefLordaAnnua);
        Assert.Equal(0m, r.IrpefNettaAnnua);
        Assert.Equal(0m, r.Conguaglio);
    }
}
