using Paghe.Core.Fatturazione;
using Paghe.Documenti;
using Xunit;
using Xunit.Abstractions;

namespace Paghe.Tests;

public class FatturaPaValidatorTests
{
    private readonly ITestOutputHelper _output;
    public FatturaPaValidatorTests(ITestOutputHelper output) => _output = output;

    private static Fattura FatturaValida(string provincia = "MI") => new()
    {
        Numero = "1/2026",
        Data = new DateOnly(2026, 6, 26),
        Cedente = new DatiAnagrafici
        {
            Denominazione = "Alfa Srl", PartitaIva = "01234567897",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = provincia,
        },
        Cessionario = new DatiAnagrafici
        {
            Denominazione = "Beta Spa", PartitaIva = "09876543217",
            Indirizzo = "Via Po 2", Cap = "10100", Comune = "Torino", Provincia = "TO",
        },
        CodiceDestinatario = "ABCDEFG",
        Righe =
        {
            new RigaFattura { Descrizione = "Consulenza", PrezzoUnitario = 100m, AliquotaIva = 22m },
            new RigaFattura { Descrizione = "Materiali", Quantita = 2m, PrezzoUnitario = 50m, AliquotaIva = 10m },
        },
    };

    [Fact]
    public void Valida_FatturaConforme_NessunErrore()
    {
        string xml = FatturaPaXml.Genera(FatturaValida());
        var errori = FatturaPaValidator.Valida(xml);

        foreach (var e in errori) _output.WriteLine(e); // diagnostica in caso di fallimento
        Assert.Empty(errori);
        Assert.True(FatturaPaValidator.EValido(xml));
    }

    [Fact]
    public void Valida_ProvinciaNonConforme_RestituisceErrori()
    {
        // "MILANO" non rispetta il pattern [A-Z]{2} previsto dallo schema.
        string xml = FatturaPaXml.Genera(FatturaValida(provincia: "MILANO"));
        var errori = FatturaPaValidator.Valida(xml);

        Assert.NotEmpty(errori);
        Assert.False(FatturaPaValidator.EValido(xml));
    }

    [Fact]
    public void Valida_XmlMalformato_RestituisceErrore()
    {
        var errori = FatturaPaValidator.Valida("<questo non e' fatturapa></x>");
        Assert.NotEmpty(errori);
    }
}
