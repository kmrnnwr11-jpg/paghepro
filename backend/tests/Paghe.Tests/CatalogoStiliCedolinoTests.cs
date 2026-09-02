using Paghe.Core.Domain;
using Paghe.Documenti;
using Xunit;

namespace Paghe.Tests;

public sealed class CatalogoStiliCedolinoTests
{
    [Fact]
    public void Catalogo_ContieneQuindiciCombinazioniUnicheEParserFailClosed()
    {
        var catalogo = CatalogoStiliCedolino.Tutti;

        Assert.Equal(StileCedolino.Colori, CatalogoStiliCedolino.Predefinito);
        Assert.Equal("colori", CatalogoStiliCedolino.ChiavePredefinita);
        Assert.Equal(
            CatalogoStiliCedolino.ChiavePredefinita,
            CatalogoStiliCedolino.Chiave(CatalogoStiliCedolino.Predefinito));
        Assert.Equal(15, catalogo.Count);
        Assert.Equal(15, Enum.GetValues<StileCedolino>().Length);
        Assert.Equal(15, catalogo.Select(voce => voce.Stile).Distinct().Count());
        Assert.Equal(15, catalogo.Select(voce => voce.Chiave).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(15, catalogo.Select(voce => (voce.Famiglia, voce.TemaId)).Distinct().Count());
        Assert.IsNotType<OpzioneStileCedolino[]>(CatalogoStiliCedolino.Tutti);
        Assert.IsNotType<string[]>(CatalogoStiliCedolino.Chiavi);
        Assert.All(catalogo, voce =>
        {
            Assert.True(CatalogoStiliCedolino.ProvaDaChiave(voce.Chiave, out var riletto));
            Assert.Equal(voce.Stile, riletto);
            Assert.Equal(voce.Chiave, CatalogoStiliCedolino.Chiave(voce.Stile));
        });

        Assert.False(CatalogoStiliCedolino.ProvaDaChiave(null, out _));
        Assert.False(CatalogoStiliCedolino.ProvaDaChiave("", out _));
        Assert.False(CatalogoStiliCedolino.ProvaDaChiave("999", out _));
        Assert.False(CatalogoStiliCedolino.ProvaDaChiave("ClassicoBlu", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CatalogoStiliCedolino.Descrivi((StileCedolino)999));
    }

    [Fact]
    public void GenerazioneSenzaStile_UsaIlModulareBluPredefinito()
    {
        byte[] pdf = CedolinoPdf.Genera(
            new Cedolino
            {
                Anno = 2026,
                Mese = 7,
                TotaleCompetenze = 1_000m,
                TotaleTrattenute = 200m,
                NettoInBusta = 800m,
            },
            new IntestazioneCedolino { Azienda = "Alfa Srl", Dipendente = "Rossi Mario" });

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        Assert.Contains("DATI DEL LAVORATORE", testo);
        Assert.DoesNotContain("Come leggere questo cedolino", testo);
    }

    [Fact]
    public void Palette_HannoContrastoSufficientePerTestoBiancoSuAccento()
    {
        foreach (var voce in CatalogoStiliCedolino.Tutti)
        {
            double contrasto = Contrasto(voce.ColoreAnteprima, "#FFFFFF");
            Assert.True(contrasto >= 4.5d,
                $"Contrasto insufficiente per {voce.Chiave}: {contrasto:0.00}:1");
        }
    }

    [Fact]
    public void StileEnumNonDefinito_NonGeneraSilenziosamenteIlClassico()
    {
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            TotaleCompetenze = 1_000m,
            TotaleTrattenute = 200m,
            NettoInBusta = 800m,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino { Azienda = "Alfa Srl", Dipendente = "Rossi Mario" },
            cartellino: null,
            stile: (StileCedolino)999));
    }

    private static double Contrasto(string primo, string secondo)
    {
        double a = Luminanza(primo);
        double b = Luminanza(secondo);
        return (Math.Max(a, b) + 0.05d) / (Math.Min(a, b) + 0.05d);
    }

    private static double Luminanza(string esadecimale)
    {
        int valore = Convert.ToInt32(esadecimale.TrimStart('#'), 16);
        double Canale(int shift)
        {
            double canale = ((valore >> shift) & 0xFF) / 255d;
            return canale <= 0.04045d
                ? canale / 12.92d
                : Math.Pow((canale + 0.055d) / 1.055d, 2.4d);
        }

        return 0.2126d * Canale(16) + 0.7152d * Canale(8) + 0.0722d * Canale(0);
    }
}
