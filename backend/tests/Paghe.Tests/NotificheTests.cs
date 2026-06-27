using System.Net;
using Paghe.Core.Notifiche;
using Xunit;

namespace Paghe.Tests;

public sealed class NotificheTests
{
    // Handler finto: cattura l'ultima richiesta (con il corpo) e risponde con uno stato fisso.
    private sealed class CatturaHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _stato;
        public HttpRequestMessage? Richiesta { get; private set; }
        public string CorpoRichiesta { get; private set; } = "";
        public string? Authorization { get; private set; }

        public CatturaHandler(HttpStatusCode stato = HttpStatusCode.Created) => _stato = stato;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Richiesta = request;
            Authorization = request.Headers.Authorization?.ToString();
            CorpoRichiesta = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_stato) { Content = new StringContent("{\"ok\":true}") };
        }
    }

    [Fact]
    public void Formattatore_RimuoveOggettoECompatta()
    {
        var msg = FormattatoreNotifiche.DaTesto("Titolo", "Oggetto: ignora\n\nPrima riga.\nSeconda riga.");
        Assert.Equal("Titolo", msg.Oggetto);
        Assert.DoesNotContain("Oggetto:", msg.TestoBreve);
        Assert.Equal("Prima riga. Seconda riga.", msg.TestoBreve);
    }

    [Fact]
    public void Formattatore_TroncaSeTroppoLungo()
    {
        var lungo = string.Join(" ", Enumerable.Repeat("parola", 200));
        var msg = FormattatoreNotifiche.DaTesto("T", lungo, lunghezzaMax: 50);
        Assert.True(msg.TestoBreve.Length <= 50);
        Assert.EndsWith("…", msg.TestoBreve);
    }

    [Fact]
    public async Task Manuale_NonInvia_PreparaSoltanto()
    {
        var esito = await new NotificatoreManuale().InviaAsync("chiunque", Msg());
        Assert.False(esito.Inviato);
        Assert.Equal(CanaleNotifica.Manuale, esito.Canale);
    }

    [Fact]
    public async Task Router_UsaCanaleDiDefault_QuandoNonSpecificato()
    {
        var opt = new NotificheOptions { Canale = "Manuale" };
        var svc = new NotificheService(opt, new NotificatoreManuale(),
            new NotificatoreEmailSmtp(opt),
            new NotificatoreSms(new HttpClient(new CatturaHandler()), opt),
            new NotificatoreWhatsApp(new HttpClient(new CatturaHandler()), opt));

        var esito = await svc.InviaAsync(null, "x", Msg());
        Assert.Equal(CanaleNotifica.Manuale, esito.Canale);
    }

    [Fact]
    public async Task Sms_NonConfigurato_NonInvia()
    {
        var opt = new NotificheOptions();
        var sms = new NotificatoreSms(new HttpClient(new CatturaHandler()), opt);
        var esito = await sms.InviaAsync("+393331112233", Msg());
        Assert.False(esito.Inviato);
        Assert.Contains("non configurato", esito.Dettaglio);
    }

    [Fact]
    public async Task Sms_Configurato_ChiamaTwilioConFormEBasicAuth()
    {
        var handler = new CatturaHandler(HttpStatusCode.Created);
        var opt = new NotificheOptions
        {
            Sms = new SmsOptions { AccountSid = "AC123", AuthToken = "tok", Mittente = "+39000" },
        };
        var sms = new NotificatoreSms(new HttpClient(handler), opt);

        var esito = await sms.InviaAsync("+393331112233", Msg());

        Assert.True(esito.Inviato);
        Assert.Contains("/2010-04-01/Accounts/AC123/Messages.json", handler.Richiesta!.RequestUri!.ToString());
        Assert.StartsWith("Basic ", handler.Authorization);
        Assert.Contains("To=", handler.CorpoRichiesta);
        Assert.Contains("From=", handler.CorpoRichiesta);
        Assert.Contains("Body=", handler.CorpoRichiesta);
    }

    [Fact]
    public async Task WhatsApp_Configurato_ChiamaMetaConBearerEJson()
    {
        var handler = new CatturaHandler(HttpStatusCode.OK);
        var opt = new NotificheOptions
        {
            WhatsApp = new WhatsAppOptions { PhoneNumberId = "999", Token = "TKN", Versione = "v21.0" },
        };
        var wa = new NotificatoreWhatsApp(new HttpClient(handler), opt);

        var esito = await wa.InviaAsync("393331112233", Msg());

        Assert.True(esito.Inviato);
        Assert.Contains("/v21.0/999/messages", handler.Richiesta!.RequestUri!.ToString());
        Assert.Equal("Bearer TKN", handler.Authorization);
        Assert.Contains("\"messaging_product\":\"whatsapp\"", handler.CorpoRichiesta);
        Assert.Contains("\"type\":\"text\"", handler.CorpoRichiesta);
    }

    [Fact]
    public async Task WhatsApp_RispostaErrore_NonInvia()
    {
        var handler = new CatturaHandler(HttpStatusCode.BadRequest);
        var opt = new NotificheOptions
        {
            WhatsApp = new WhatsAppOptions { PhoneNumberId = "999", Token = "TKN" },
        };
        var wa = new NotificatoreWhatsApp(new HttpClient(handler), opt);

        var esito = await wa.InviaAsync("393331112233", Msg());
        Assert.False(esito.Inviato);
        Assert.Contains("Errore WhatsApp 400", esito.Dettaglio);
    }

    private static MessaggioNotifica Msg() =>
        FormattatoreNotifiche.DaTesto("Promemoria", "Oggetto: x\nIl DURC scade il 30/06/2026.");
}
