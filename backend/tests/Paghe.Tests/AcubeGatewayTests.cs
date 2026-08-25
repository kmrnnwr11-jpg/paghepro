using System.Net;
using System.Text;
using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Persistence.Trasmissione;
using Xunit;

namespace Paghe.Tests;

public sealed class AcubeGatewayTests
{
    [Fact]
    public async Task Acube_Login_Invio_LeggeEsito()
    {
        var gateway = new SdiGatewayAcube(
            new HttpClient(new RoutingHandler()),
            new AcubeOptions
            {
                Email = "studio@example.it",
                Password = "secret",
                Environment = "sandbox",
                BaseUrlSandbox = "https://api-sandbox.acubeapi.com",
            });

        var risultato = await gateway.TrasmettiAsync(
            new FatturaDaTrasmettere(Guid.NewGuid(), "IT01234567897_00001.xml", Encoding.UTF8.GetBytes("<xml/>"), Firmato: false));

        Assert.Equal(StatoTrasmissioneSdi.Inviata, risultato.Stato);
        Assert.Equal("u-1", risultato.IdentificativoSdi);

        var esito = await gateway.InterrogaEsitoAsync("u-1");
        Assert.NotNull(esito);
        Assert.Equal(StatoTrasmissioneSdi.ConsegnataSdi, esito!.Stato);
    }

    // Handler che instrada le 3 chiamate del flusso A-Cube: login, invio, lettura esito.
    private sealed class RoutingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            var code = HttpStatusCode.OK;
            string json;

            if (url.Contains("/login"))
                json = """{"token":"JWT.fake.token"}""";
            else if (request.Method == HttpMethod.Post && url.EndsWith("/invoices"))
            {
                json = """{"uuid":"u-1"}""";
                code = HttpStatusCode.Accepted;
            }
            else if (request.Method == HttpMethod.Get && url.Contains("/invoices/u-1"))
                json = """{"notifications":[{"type":"RC","code":"RC"}]}""";
            else
            {
                json = "{}";
                code = HttpStatusCode.NotFound;
            }

            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
