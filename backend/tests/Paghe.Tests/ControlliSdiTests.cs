using Paghe.Core.Fatturazione;
using Xunit;

namespace Paghe.Tests;

public class ControlliSdiTests
{
    private static DatiAnagrafici Soggetto(string nome, string? piva = "01234567897", string? cf = null) => new()
    {
        Denominazione = nome, PartitaIva = piva, CodiceFiscale = cf,
        Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
    };

    private static Fattura Fattura(DatiAnagrafici? cessionario = null, params RigaFattura[] righe) => new()
    {
        Numero = "1/2026", Data = new DateOnly(2026, 6, 26),
        Cedente = Soggetto("Alfa Srl"),
        Cessionario = cessionario ?? Soggetto("Beta Spa", "09876543217"),
        Righe = righe.Length > 0
            ? righe.ToList()
            : new List<RigaFattura> { new() { Descrizione = "Servizio", PrezzoUnitario = 100m, AliquotaIva = 22m } },
    };

    [Fact]
    public void Verifica_FatturaCorretta_NessunErrore()
    {
        Assert.Empty(ControlliSdi.Verifica(Fattura()));
        Assert.True(ControlliSdi.EConforme(Fattura()));
    }

    [Fact]
    public void Verifica_AliquotaZeroSenzaNatura_Errore00400()
    {
        var f = Fattura(null, new RigaFattura { Descrizione = "Esente", PrezzoUnitario = 100m, AliquotaIva = 0m });
        var errori = ControlliSdi.Verifica(f);
        Assert.Contains(errori, e => e.Codice == "00400");
    }

    [Fact]
    public void Verifica_AliquotaZeroConNatura_Ok()
    {
        var f = Fattura(null, new RigaFattura
        {
            Descrizione = "Operazione esente", PrezzoUnitario = 100m, AliquotaIva = 0m, NaturaIva = "N4",
        });
        Assert.DoesNotContain(ControlliSdi.Verifica(f), e => e.Codice == "00400");
    }

    [Fact]
    public void Verifica_NaturaConAliquotaNonZero_Errore00401()
    {
        var f = Fattura(null, new RigaFattura
        {
            Descrizione = "Incoerente", PrezzoUnitario = 100m, AliquotaIva = 22m, NaturaIva = "N1",
        });
        Assert.Contains(ControlliSdi.Verifica(f), e => e.Codice == "00401");
    }

    [Fact]
    public void Verifica_CessionarioSenzaPivaNeCf_Errore00417()
    {
        var cessionarioSenzaId = Soggetto("Beta Spa", piva: null, cf: null);
        var errori = ControlliSdi.Verifica(Fattura(cessionarioSenzaId));
        Assert.Contains(errori, e => e.Codice == "00417");
    }

    [Fact]
    public void Verifica_CessionarioSoloCodiceFiscale_Ok()
    {
        var cessionario = Soggetto("Mario Rossi", piva: null, cf: "RSSMRA80A01F205X");
        Assert.DoesNotContain(ControlliSdi.Verifica(Fattura(cessionario)), e => e.Codice == "00417");
    }
}
