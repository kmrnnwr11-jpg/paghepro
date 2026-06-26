using Paghe.Core.Fatturazione;
using Xunit;

namespace Paghe.Tests;

public class CalcoloFatturaTests
{
    private static DatiAnagrafici Soggetto(string nome, string piva) => new()
    {
        Denominazione = nome, PartitaIva = piva,
        Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
    };

    private static Fattura FatturaCon(params RigaFattura[] righe) => new()
    {
        Numero = "1/2026", Data = new DateOnly(2026, 6, 26),
        Cedente = Soggetto("Alfa Srl", "01234567890"),
        Cessionario = Soggetto("Beta Spa", "09876543210"),
        Righe = righe.ToList(),
    };

    [Fact]
    public void Calcola_RigaUnica22_ImponibileImpostaTotale()
    {
        var t = CalcoloFattura.Calcola(FatturaCon(
            new RigaFattura { Descrizione = "Servizio", PrezzoUnitario = 100m, AliquotaIva = 22m }));

        Assert.Equal(100m, t.TotaleImponibile);
        Assert.Equal(22m, t.TotaleImposta);
        Assert.Equal(122m, t.TotaleDocumento);
        Assert.Single(t.Riepiloghi);
    }

    [Fact]
    public void Calcola_ConQuantita()
    {
        var t = CalcoloFattura.Calcola(FatturaCon(
            new RigaFattura { Descrizione = "Pezzi", Quantita = 3m, PrezzoUnitario = 50m, AliquotaIva = 22m }));

        Assert.Equal(150m, t.TotaleImponibile);
        Assert.Equal(33m, t.TotaleImposta);
        Assert.Equal(183m, t.TotaleDocumento);
    }

    [Fact]
    public void Calcola_MultiAliquota_UnRiepilogoPerAliquota()
    {
        var t = CalcoloFattura.Calcola(FatturaCon(
            new RigaFattura { Descrizione = "A", PrezzoUnitario = 100m, AliquotaIva = 22m },
            new RigaFattura { Descrizione = "B", PrezzoUnitario = 50m,  AliquotaIva = 10m }));

        Assert.Equal(2, t.Riepiloghi.Count);
        Assert.Equal(150m, t.TotaleImponibile);
        Assert.Equal(27m, t.TotaleImposta);   // 22 + 5
        Assert.Equal(177m, t.TotaleDocumento);

        // Ordinati per aliquota crescente.
        Assert.Equal(10m, t.Riepiloghi[0].Aliquota);
        Assert.Equal(22m, t.Riepiloghi[1].Aliquota);
    }

    [Fact]
    public void Calcola_StessaAliquota_AggregaImponibili()
    {
        var t = CalcoloFattura.Calcola(FatturaCon(
            new RigaFattura { Descrizione = "A", PrezzoUnitario = 100m, AliquotaIva = 22m },
            new RigaFattura { Descrizione = "B", PrezzoUnitario = 200m, AliquotaIva = 22m }));

        Assert.Single(t.Riepiloghi);
        Assert.Equal(300m, t.Riepiloghi[0].Imponibile);
        Assert.Equal(66m, t.Riepiloghi[0].Imposta);
    }

    [Fact]
    public void CalcolaSpesa_IvaParzialmenteDetraibile()
    {
        var spesa = new Spesa
        {
            Data = new DateOnly(2026, 6, 1), Descrizione = "Carburante", ContoCosto = "62.10",
            Imponibile = 1000m, AliquotaIva = 22m, PercentualeDetraibilita = 40m,
        };

        var (iva, ivaDetraibile, totale) = CalcoloFattura.CalcolaSpesa(spesa);

        Assert.Equal(220m, iva);
        Assert.Equal(88m, ivaDetraibile);   // 220 * 40%
        Assert.Equal(1220m, totale);
    }
}
