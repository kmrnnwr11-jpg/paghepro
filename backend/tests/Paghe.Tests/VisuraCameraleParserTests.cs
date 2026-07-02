using Paghe.Core.PartiteIva;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test del parser euristico della visura camerale (estrazione dati azienda dal testo).</summary>
public sealed class VisuraCameraleParserTests
{
    private const string VisuraEsempio = """
        REGISTRO DELLE IMPRESE
        Denominazione: ALFA COSTRUZIONI SRL
        Codice fiscale: 01234567890
        Partita IVA: 09876543210
        Indirizzo: VIA ROMA 10 - 20121 MILANO (MI)
        Codice ATECO: 41.20.00
        Rappresentante legale: MARIO ROSSI
        """;

    [Fact]
    public void Estrai_LeggeICampiPrincipali()
    {
        var d = VisuraCameraleParser.Estrai(VisuraEsempio);

        Assert.Equal("ALFA COSTRUZIONI SRL", d.RagioneSociale);
        Assert.Equal("09876543210", d.PartitaIva);
        Assert.Equal("01234567890", d.CodiceFiscale);
        Assert.Equal("20121", d.Cap);
        Assert.Equal("MILANO", d.Comune);
        Assert.Equal("MI", d.Provincia);
        Assert.Equal("41.20.00", d.CodiceAteco);
        Assert.Equal("MARIO ROSSI", d.LegaleRappresentante);
        Assert.Equal("VIA ROMA 10", d.Indirizzo);
    }

    [Fact]
    public void Estrai_TestoVuoto_RestituisceTuttoNull()
    {
        var d = VisuraCameraleParser.Estrai("");
        Assert.Null(d.RagioneSociale);
        Assert.Null(d.PartitaIva);
        Assert.Null(d.Comune);
    }

    [Fact]
    public void Estrai_ComuneConEtichettaEsplicita_SuRigaSeparata()
    {
        // Layout da OCR di foto/scansione: sede su una riga, CAP e COMUNE etichettati a parte.
        const string visura = """
            CAMERA DI COMMERCIO DI MILANO
            DENOMINAZIONE: TECNOEDIL COSTRUZIONI S.R.L.
            PARTITA IVA: 12345678903
            SEDE LEGALE: VIA GIUSEPPE VERDI 15
            CAP: 20121 COMUNE: MILANO (MI)
            AMMINISTRATORE UNICO: BIANCHI MARCO
            """;

        var d = VisuraCameraleParser.Estrai(visura);

        Assert.Equal("VIA GIUSEPPE VERDI 15", d.Indirizzo);
        Assert.Equal("20121", d.Cap);
        Assert.Equal("MILANO", d.Comune);
        Assert.Equal("MI", d.Provincia);
        Assert.Equal("BIANCHI MARCO", d.LegaleRappresentante);
    }
}
