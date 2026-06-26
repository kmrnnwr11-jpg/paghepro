using Paghe.Core.AdeTelematico;

namespace Paghe.Tests.AdeTelematico;

public class PraticaTelematicaTests
{
    private static PraticaTelematica NuovaF24() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoModelloTelematico.F24, 2026);

    [Fact]
    public void Parte_in_bozza()
    {
        Assert.Equal(StatoPraticaTelematica.Bozza, NuovaF24().Stato);
    }

    [Fact]
    public void Percorso_felice_fino_a_ricevuta()
    {
        var p = NuovaF24();
        p.Transiziona(StatoPraticaTelematica.Generata);
        p.Transiziona(StatoPraticaTelematica.ControllataOk);
        p.Transiziona(StatoPraticaTelematica.ProntaPerDesktopTelematico);
        p.Transiziona(StatoPraticaTelematica.InviataManualmente);
        p.Transiziona(StatoPraticaTelematica.RicevutaAcquisita);

        Assert.Equal(StatoPraticaTelematica.RicevutaAcquisita, p.Stato);
    }

    [Fact]
    public void Transizione_non_ammessa_lancia()
    {
        var p = NuovaF24();
        Assert.Throws<InvalidOperationException>(() =>
            p.Transiziona(StatoPraticaTelematica.InviataManualmente));
    }

    [Fact]
    public void Controllo_ko_puo_tornare_in_bozza()
    {
        var p = NuovaF24();
        p.Transiziona(StatoPraticaTelematica.Generata);
        p.Transiziona(StatoPraticaTelematica.ControllataKo);
        p.Transiziona(StatoPraticaTelematica.Bozza);

        Assert.Equal(StatoPraticaTelematica.Bozza, p.Stato);
    }

    [Fact]
    public void Ricevuta_acquisita_e_terminale()
    {
        var p = NuovaF24();
        p.Transiziona(StatoPraticaTelematica.Generata);
        p.Transiziona(StatoPraticaTelematica.ControllataOk);
        p.Transiziona(StatoPraticaTelematica.ProntaPerDesktopTelematico);
        p.Transiziona(StatoPraticaTelematica.InviataManualmente);
        p.Transiziona(StatoPraticaTelematica.RicevutaAcquisita);

        Assert.False(p.PuoTransizionareA(StatoPraticaTelematica.Bozza));
    }
}
