using Paghe.Core.AdeTelematico;

namespace Paghe.Tests.AdeTelematico;

public class ValidazioneF24Tests
{
    private static ModelloF24 F24Valido(IReadOnlyList<VoceErario>? erario = null) => new()
    {
        Contribuente = new ContribuenteF24
        {
            CodiceFiscale = "RSSMRA85T10A562S",
            Tipo = TipoSoggettoF24.PersonaFisica,
            Cognome = "Rossi",
            Nome = "Mario",
        },
        DataVersamento = new DateOnly(2026, 6, 16),
        Erario = erario ?? [new VoceErario("1001", 2026, ImportoDebito: 1500.00m)],
    };

    [Fact]
    public void Modello_valido_passa()
    {
        var esito = ValidazioneF24.Verifica(F24Valido());
        Assert.True(esito.Valido, string.Join("; ", esito.Errori.Select(e => e.Messaggio)));
    }

    [Fact]
    public void Senza_voci_e_invalido()
    {
        var f24 = new ModelloF24
        {
            Contribuente = new ContribuenteF24
            {
                CodiceFiscale = "RSSMRA85T10A562S",
                Tipo = TipoSoggettoF24.PersonaFisica,
                Cognome = "Rossi",
                Nome = "Mario",
            },
            DataVersamento = new DateOnly(2026, 6, 16),
        };

        var esito = ValidazioneF24.Verifica(f24);
        Assert.False(esito.Valido);
        Assert.Contains(esito.Errori, e => e.Campo == "Voci");
    }

    [Fact]
    public void Codice_fiscale_contribuente_errato_e_invalido()
    {
        var f24 = F24Valido();
        var rotto = new ModelloF24
        {
            Contribuente = new ContribuenteF24
            {
                CodiceFiscale = "RSSMRA85T10A562X",
                Tipo = TipoSoggettoF24.PersonaFisica,
                Cognome = "Rossi",
                Nome = "Mario",
            },
            DataVersamento = f24.DataVersamento,
            Erario = f24.Erario,
        };

        var esito = ValidazioneF24.Verifica(rotto);
        Assert.False(esito.Valido);
        Assert.Contains(esito.Errori, e => e.Campo == "Contribuente.CodiceFiscale");
    }

    [Fact]
    public void Riga_con_debito_e_credito_insieme_e_invalida()
    {
        var f24 = F24Valido([new VoceErario("1001", 2026, ImportoDebito: 100m, ImportoCredito: 50m)]);
        var esito = ValidazioneF24.Verifica(f24);
        Assert.False(esito.Valido);
        Assert.Contains(esito.Errori, e => e.Campo == "Erario.Importi");
    }

    [Fact]
    public void Saldo_negativo_e_invalido()
    {
        var f24 = F24Valido([new VoceErario("1001", 2026, ImportoCredito: 999m)]);
        var esito = ValidazioneF24.Verifica(f24);
        Assert.False(esito.Valido);
        Assert.Contains(esito.Errori, e => e.Campo == "SaldoFinale");
    }

    [Fact]
    public void Importo_con_tre_decimali_e_invalido()
    {
        var f24 = F24Valido([new VoceErario("1001", 2026, ImportoDebito: 10.123m)]);
        var esito = ValidazioneF24.Verifica(f24);
        Assert.False(esito.Valido);
        Assert.Contains(esito.Errori, e => e.Campo == "Erario.Importi");
    }

    [Fact]
    public void Totali_e_saldo_calcolati()
    {
        var f24 = F24Valido(
        [
            new VoceErario("1001", 2026, ImportoDebito: 1000m),
            new VoceErario("1002", 2026, ImportoCredito: 250m),
        ]);

        Assert.Equal(1000m, f24.TotaleDebiti);
        Assert.Equal(250m, f24.TotaleCrediti);
        Assert.Equal(750m, f24.SaldoFinale);
    }
}
