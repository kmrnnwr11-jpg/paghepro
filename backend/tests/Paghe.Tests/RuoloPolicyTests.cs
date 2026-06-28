using Paghe.Persistence.Sicurezza;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test della policy RBAC per ruolo (decisione pura applicata dal middleware API).</summary>
public sealed class RuoloPolicyTests
{
    [Theory]
    // admin e operatore: possono scrivere
    [InlineData("admin", "POST", "/api/dipendenti", true)]
    [InlineData("operatore", "POST", "/api/aziende", true)]
    [InlineData("operatore", "PUT", "/api/fatture/1", true)]
    // sola_lettura: solo lettura
    [InlineData("sola_lettura", "GET", "/api/dipendenti", true)]
    [InlineData("sola_lettura", "POST", "/api/dipendenti", false)]
    [InlineData("sola_lettura", "DELETE", "/api/aziende/1", false)]
    // anonimizzazione GDPR: scrittura riservata all'admin
    [InlineData("admin", "POST", "/api/dipendenti/abc/anonimizza", true)]
    [InlineData("operatore", "POST", "/api/dipendenti/abc/anonimizza", false)]
    [InlineData("sola_lettura", "GET", "/api/dipendenti/abc/anonimizza", true)]
    public void PuoEseguire_RispettaIlRuolo(string ruolo, string metodo, string path, bool atteso) =>
        Assert.Equal(atteso, RuoloPolicy.PuoEseguire(ruolo, metodo, path));
}
