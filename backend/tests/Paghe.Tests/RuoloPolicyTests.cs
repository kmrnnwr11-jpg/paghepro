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
    // Il Governor usa POST con manifest/lease, ma il percorso G2 non modifica dati di dominio.
    [InlineData("sola_lettura", "POST", "/api/ai/governor/preflight", true)]
    [InlineData("sola_lettura", "POST", "/api/ai/governor/grants", true)]
    [InlineData("sola_lettura", "POST", "/api/ai/governor/query", true)]
    [InlineData("sola_lettura", "POST", "/api/ai/governor/grants/abc/revoca", true)]
    [InlineData("sola_lettura", "POST", "/api/ai/chat/azione", false)]
    // La conferma di una proposta applica una scrittura: mai al ruolo sola_lettura.
    [InlineData("sola_lettura", "POST", "/api/ai/chat/conferma", false)]
    [InlineData("operatore", "POST", "/api/ai/chat/conferma", true)]
    // anonimizzazione GDPR: scrittura riservata all'admin
    [InlineData("admin", "POST", "/api/dipendenti/abc/anonimizza", true)]
    [InlineData("operatore", "POST", "/api/dipendenti/abc/anonimizza", false)]
    [InlineData("sola_lettura", "GET", "/api/dipendenti/abc/anonimizza", true)]
    // Posta: la credenziale della casella email/PEC si collega/rimuove solo da admin.
    [InlineData("admin", "POST", "/api/posta/caselle", true)]
    [InlineData("operatore", "POST", "/api/posta/caselle", false)]
    [InlineData("operatore", "DELETE", "/api/posta/caselle/abc", false)]
    [InlineData("operatore", "GET", "/api/posta/caselle", true)]
    [InlineData("operatore", "POST", "/api/posta/messaggi/abc/conferma", true)]
    // Validazione, chiusura e immagine autentica del datore sono riservate all'admin.
    [InlineData("admin", "POST", "/api/cedolini/abc/valida", true)]
    [InlineData("operatore", "POST", "/api/cedolini/abc/valida", false)]
    [InlineData("operatore", "POST", "/api/cedolini/abc/chiudi", false)]
    [InlineData("operatore", "POST", "/api/aziende/abc/firma-timbro", false)]
    [InlineData("superadmin", "DELETE", "/api/aziende/abc/firma-timbro", true)]
    // Il registro attività contiene informazioni di sicurezza: anche in lettura è solo admin.
    [InlineData("admin", "GET", "/api/audit", true)]
    [InlineData("superadmin", "GET", "/api/audit", true)]
    [InlineData("operatore", "GET", "/api/audit", false)]
    [InlineData("sola_lettura", "GET", "/api/audit", false)]
    [InlineData("ruolo_inventato", "GET", "/api/aziende", false)]
    public void PuoEseguire_RispettaIlRuolo(string ruolo, string metodo, string path, bool atteso) =>
        Assert.Equal(atteso, RuoloPolicy.PuoEseguire(ruolo, metodo, path));
}
