using PWS.Core.Models;
using PWS.Core.Providers;
using PWS.IntegrationTests.Common;

namespace PWS.IntegrationTests.Providers;

/// <summary>
/// <see cref="ApiContentProvider"/> contro un server HTTP reale su loopback.
/// </summary>
[TestFixture]
public sealed class ApiContentProviderTests : ArchiveTestBase
{
    private static readonly TestHttpResponse JsonOk =
        new(200, "application/json", """{"stato":"ok"}""");

    [Test]
    public async Task GetAsync_http_restituisce_stato_mime_e_corpo_del_server()
    {
        using var server = new TestHttpServer(_ => JsonOk);
        using var http   = CreateHttpClient();
        var provider     = new ApiContentProvider(http, server.BaseAddress);
        var target       = new Uri(server.BaseAddress, "dati/stato");

        using var response = await provider.GetAsync(ContentRequest.Get(target));

        response.StatusCode.Should().Be(200);
        response.MimeType.Should().Be("application/json");
        response.FinalUri.Should().Be(target.ToString());
        (await ReadTextAsync(response.Content)).Should().Be(JsonOk.Body);
    }

    [Test]
    public async Task GetAsync_schema_api_viene_risolto_sul_base_address()
    {
        using var server = new TestHttpServer(_ => JsonOk);
        using var http   = CreateHttpClient();
        var provider     = new ApiContentProvider(http, server.BaseAddress, "api");

        using var response = await provider.GetAsync(ContentRequest.Get("api://servizio/articoli?pagina=2"));

        response.StatusCode.Should().Be(200);
        server.Requests.Should().ContainSingle()
            .Which.PathAndQuery.Should().Be("/articoli?pagina=2");
    }

    [Test]
    public async Task GetAsync_inoltra_gli_header_della_richiesta()
    {
        using var server = new TestHttpServer(_ => JsonOk);
        using var http   = CreateHttpClient();
        var provider     = new ApiContentProvider(http, server.BaseAddress);
        var request      = new ContentRequest
        {
            Uri     = new Uri(server.BaseAddress, "dati"),
            Headers = new Dictionary<string, string> { ["X-Pws-Test"] = "42" },
        };

        using var response = await provider.GetAsync(request);

        response.StatusCode.Should().Be(200);
        server.Requests.Should().ContainSingle()
            .Which.Headers.Should().ContainKey("X-Pws-Test")
            .WhoseValue.Should().Be("42");
    }

    [Test]
    public async Task GetAsync_propaga_lo_stato_di_errore_del_server()
    {
        using var server = new TestHttpServer(_ => new TestHttpResponse(404, "text/plain", "non trovato"));
        using var http   = CreateHttpClient();
        var provider     = new ApiContentProvider(http, server.BaseAddress);

        using var response = await provider.GetAsync(ContentRequest.Get(new Uri(server.BaseAddress, "manca")));

        response.StatusCode.Should().Be(404);
        response.IsSuccess.Should().BeFalse();
        response.MimeType.Should().Be("text/plain");
    }

    [Test]
    public async Task GetAsync_server_irraggiungibile_restituisce_503()
    {
        // Porta libera su cui nessuno è in ascolto: la connessione viene rifiutata.
        var unreachable = new Uri($"http://127.0.0.1:{FreeTcpPort.Next()}/");
        using var http  = CreateHttpClient();
        var provider    = new ApiContentProvider(http, unreachable);

        using var response = await provider.GetAsync(ContentRequest.Get(new Uri(unreachable, "dati")));

        response.StatusCode.Should().Be(503);
        response.IsSuccess.Should().BeFalse();
    }

    [TestCase("http://esempio.it/",  true)]
    [TestCase("https://esempio.it/", true)]
    [TestCase("api://servizio/x",    true)]
    [TestCase("pws://docs/x",        false)]
    public void CanHandle_accetta_http_https_e_gli_schemi_aggiuntivi(string uri, bool expected)
    {
        using var http = CreateHttpClient();
        var provider   = new ApiContentProvider(http, new Uri("http://127.0.0.1/"), "api");

        provider.CanHandle(new Uri(uri)).Should().Be(expected);
    }

    [Test]
    public void CanHandle_senza_schemi_aggiuntivi_rifiuta_api()
    {
        using var http = CreateHttpClient();
        var provider   = new ApiContentProvider(http, new Uri("http://127.0.0.1/"));

        provider.CanHandle(new Uri("api://servizio/x")).Should().BeFalse();
    }
}
