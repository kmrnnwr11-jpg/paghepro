using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;

namespace Paghe.Tests.AdeTelematico;

public class ImportazioneUnilavTests
{
    // UNILAV sintetico con namespace (la lettura deve essere namespace-agnostica) e sezioni standard.
    private const string UnilavXml = """
        <Moduli xmlns="urn:CO">
          <DatoreLavoro>
            <CodiceFiscale>12345678901</CodiceFiscale>
            <Denominazione>Acme Srl</Denominazione>
          </DatoreLavoro>
          <Lavoratore>
            <CodiceFiscale>RSSMRA85T10A562S</CodiceFiscale>
            <Cognome>Rossi</Cognome>
            <Nome>Mario</Nome>
            <Sesso>M</Sesso>
            <DataNascita>1985-12-10</DataNascita>
            <ComuneNascita>Aosta</ComuneNascita>
            <Cittadinanza>Italia</Cittadinanza>
            <Comune>Milano</Comune>
            <Provincia>MI</Provincia>
            <Cap>20100</Cap>
          </Lavoratore>
          <InizioRapporto>
            <DataInizio>2026-06-01</DataInizio>
            <TipoContratto>Tempo indeterminato</TipoContratto>
            <CCNL>COMMERCIO_TERZIARIO</CCNL>
            <Qualifica>Impiegato amministrativo</Qualifica>
            <Livello>3</Livello>
            <TipoOrario>P</TipoOrario>
            <PercentualePartTime>50</PercentualePartTime>
            <OreSettimanali>20</OreSettimanali>
            <Retribuzione>900,00</Retribuzione>
          </InizioRapporto>
        </Moduli>
        """;

    [Fact]
    public void Riconosce_datore_lavoratore_rapporto()
    {
        var r = ImportazioneUnilav.Importa(UnilavXml);

        Assert.Equal("12345678901", r.Datore.CodiceFiscale);
        Assert.Equal("Acme Srl", r.Datore.Denominazione);

        Assert.Equal("RSSMRA85T10A562S", r.Lavoratore.CodiceFiscale);
        Assert.Equal("Rossi", r.Lavoratore.Cognome);
        Assert.Equal("Mario", r.Lavoratore.Nome);
        Assert.Equal(new DateOnly(1985, 12, 10), r.Lavoratore.DataNascita);
        Assert.Equal("Milano", r.Lavoratore.ComuneDomicilio);
        Assert.Equal("MI", r.Lavoratore.ProvinciaDomicilio);

        Assert.Equal(new DateOnly(2026, 6, 1), r.Rapporto.DataInizio);
        Assert.Equal("COMMERCIO_TERZIARIO", r.Rapporto.Ccnl);
        Assert.Equal("3", r.Rapporto.Livello);
        Assert.Equal(50m, r.Rapporto.PercentualePartTime);
        Assert.Equal(900.00m, r.Rapporto.RetribuzionePrevista);
    }

    [Fact]
    public void Mappa_rapporto_di_dominio_con_part_time()
    {
        var r = ImportazioneUnilav.Importa(UnilavXml);
        var rl = ImportazioneUnilav.CostruisciRapportoLavoro(r);

        Assert.Equal("COMMERCIO_TERZIARIO", rl.CodiceCcnl);
        Assert.Equal("3", rl.Livello);
        Assert.Equal(Qualifica.Impiegato, rl.Qualifica);
        Assert.True(rl.PartTime);
        Assert.Equal(50m, rl.PercentualePartTime);
    }

    [Fact]
    public void Ccnl_mancante_segnala_e_blocca_mappatura()
    {
        const string senzaCcnl = """
            <Moduli>
              <Lavoratore><CodiceFiscale>RSSMRA85T10A562S</CodiceFiscale></Lavoratore>
              <InizioRapporto><DataInizio>2026-06-01</DataInizio></InizioRapporto>
            </Moduli>
            """;

        var r = ImportazioneUnilav.Importa(senzaCcnl);
        Assert.Contains(r.Avvertenze, a => a.Contains("CCNL"));

        var ex = Assert.Throws<ArgumentException>(() => ImportazioneUnilav.CostruisciRapportoLavoro(r));
        Assert.Contains("CCNL", ex.Message);
    }

    [Fact]
    public void Xml_non_valido_lancia()
    {
        Assert.Throws<ArgumentException>(() => ImportazioneUnilav.Importa("non-xml"));
        Assert.Throws<ArgumentException>(() => ImportazioneUnilav.Importa(""));
    }

    [Theory]
    [InlineData("Operaio generico", Qualifica.Operaio)]
    [InlineData("Quadro direttivo", Qualifica.Quadro)]
    [InlineData("Dirigente", Qualifica.Dirigente)]
    [InlineData("Apprendista", Qualifica.Apprendista)]
    [InlineData("Impiegato", Qualifica.Impiegato)]
    [InlineData(null, Qualifica.Impiegato)]
    public void Mappa_qualifica(string? testo, Qualifica atteso) =>
        Assert.Equal(atteso, ImportazioneUnilav.MappaQualifica(testo));
}
