using System.Net;
using System.Text;
using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Persistence.Trasmissione;
using Xunit;

namespace Paghe.Tests;

public sealed class SdiGatewayProviderRestTests
{
    [Fact]
    public async Task Trasmetti_RispostaOk_MappaInviataEId()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"id":"ABC-123","status":"sent"}""");
        var gateway = new SdiGatewayProviderRest(
            new HttpClient(handler),
            new SdiProviderOptions { BaseUrl = "https://api.provider.test/v1", EndpointInvio = "invoices", ApiKey = "k" });

        var risultato = await gateway.TrasmettiAsync(NuovaFattura());

        Assert.Equal(StatoTrasmissioneSdi.Inviata, risultato.Stato);
        Assert.Equal("ABC-123", risultato.IdentificativoSdi);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("invoices", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task Trasmetti_RispostaConsegnata_MappaConsegnataSdi()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"id":"X","status":"delivered"}""");
        var gateway = new SdiGatewayProviderRest(new HttpClient(handler),
            new SdiProviderOptions { BaseUrl = "https://x/", EndpointInvio = "i" });

        var risultato = await gateway.TrasmettiAsync(NuovaFattura());

        Assert.Equal(StatoTrasmissioneSdi.ConsegnataSdi, risultato.Stato);
    }

    [Fact]
    public async Task Trasmetti_RispostaErrore_MappaErrore()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, """{"error":"invalid"}""");
        var gateway = new SdiGatewayProviderRest(new HttpClient(handler),
            new SdiProviderOptions { BaseUrl = "https://x/", EndpointInvio = "i" });

        var risultato = await gateway.TrasmettiAsync(NuovaFattura());

        Assert.Equal(StatoTrasmissioneSdi.Errore, risultato.Stato);
        Assert.Equal("400", risultato.CodiceErrore);
    }

    [Fact]
    public async Task InterrogaEsito_LeggeStatoDalProvider()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"id":"X","status":"rejected"}""");
        var gateway = new SdiGatewayProviderRest(new HttpClient(handler),
            new SdiProviderOptions { BaseUrl = "https://x/", EndpointEsito = "invoices/{id}" });

        var esito = await gateway.InterrogaEsitoAsync("X");

        Assert.NotNull(esito);
        Assert.Equal(StatoTrasmissioneSdi.ScartataSdi, esito!.Stato);
        Assert.Contains("invoices/X", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Simulato_TrasmetteEConsegnaEsito()
    {
        var gateway = new SdiGatewaySimulato();

        var risultato = await gateway.TrasmettiAsync(NuovaFattura());
        Assert.Equal(StatoTrasmissioneSdi.Inviata, risultato.Stato);
        Assert.False(string.IsNullOrWhiteSpace(risultato.IdentificativoSdi));

        var esito = await gateway.InterrogaEsitoAsync(risultato.IdentificativoSdi!);
        Assert.NotNull(esito);
        Assert.Equal(StatoTrasmissioneSdi.ConsegnataSdi, esito!.Stato);
    }

    private static FatturaDaTrasmettere NuovaFattura() =>
        new(Guid.NewGuid(), "IT01234567890_00001.xml", Encoding.UTF8.GetBytes("<xml/>"), Firmato: false);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHandler(HttpStatusCode code, string body)
        {
            _code = code;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_code)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
