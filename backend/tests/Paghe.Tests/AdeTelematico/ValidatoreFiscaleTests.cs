using Paghe.Core.AdeTelematico;

namespace Paghe.Tests.AdeTelematico;

public class ValidatoreFiscaleTests
{
    [Theory]
    [InlineData("RSSMRA85T10A562S")]   // codice fiscale persona fisica con carattere di controllo corretto
    [InlineData("rssmra85t10a562s")]   // case-insensitive
    public void CodiceFiscalePersonaFisica_valido(string cf) =>
        Assert.True(ValidatoreFiscale.CodiceFiscalePersonaFisicaValido(cf));

    [Theory]
    [InlineData("RSSMRA85T10A562X")]   // carattere di controllo errato
    [InlineData("RSSMRA85T10A562")]    // troppo corto
    [InlineData("")]
    [InlineData(null)]
    public void CodiceFiscalePersonaFisica_non_valido(string? cf) =>
        Assert.False(ValidatoreFiscale.CodiceFiscalePersonaFisicaValido(cf));

    [Theory]
    [InlineData("01234567897")]        // check digit mod 10 corretto
    public void PartitaIva_valida(string piva) =>
        Assert.True(ValidatoreFiscale.PartitaIvaValida(piva));

    [Theory]
    [InlineData("01234567890")]        // check digit errato
    [InlineData("0123456789")]         // 10 cifre
    [InlineData("0123456789A")]        // contiene lettere
    [InlineData(null)]
    public void PartitaIva_non_valida(string? piva) =>
        Assert.False(ValidatoreFiscale.PartitaIvaValida(piva));

    [Theory]
    [InlineData("RSSMRA85T10A562S")]   // 16 char → CF persona fisica
    [InlineData("01234567897")]        // 11 cifre → trattato come partita IVA
    public void CodiceFiscale_accetta_pf_e_soggetti_numerici(string valore) =>
        Assert.True(ValidatoreFiscale.CodiceFiscaleValido(valore));
}
