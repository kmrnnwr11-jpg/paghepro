using Paghe.Core.AdeTelematico;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test della mappatura CCNL portale UNILAV → CCNL interno del motore.</summary>
public sealed class MappaturaCcnlTests
{
    [Theory]
    [InlineData("0190 - AGRICOLTURA - Operai florovivaisti", "AGRICOLTURA_OPERAI")]
    [InlineData("7370 - TRASPORTO MERCI - Art. (App.Ass. 1/2011)", "TRASPORTI_LOGISTICA")]
    [InlineData("CCNL Edilizia Industria", "EDILIZIA_INDUSTRIA")]
    [InlineData("Metalmeccanici - industria", "METALMECCANICI_INDUSTRIA")]
    [InlineData("Turismo - Pubblici esercizi", "TURISMO_PUBBLICI_ESERCIZI")]
    [InlineData("Commercio e terziario", "COMMERCIO_TERZIARIO")]
    [InlineData("COMMERCIO_TERZIARIO", "COMMERCIO_TERZIARIO")]
    public void Riconosce_IlCcnlInterno(string ccnlUnilav, string atteso) =>
        Assert.Equal(atteso, MappaturaCcnl.Riconosci(ccnlUnilav)?.CodiceInterno);

    [Fact]
    public void Sconosciuto_RestituisceNull() =>
        Assert.Null(MappaturaCcnl.Riconosci("9999 - SETTORE NON PREVISTO"));

    [Fact]
    public void Vuoto_RestituisceNull() =>
        Assert.Null(MappaturaCcnl.Riconosci(null));
}
