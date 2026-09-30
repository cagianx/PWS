using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PWS.Core.Hosting;
using PWS.Core.Providers;
using PWS.Format.Crypto;
using PWS.IntegrationTests.Common;

namespace PWS.IntegrationTests.Hosting;

/// <summary>
/// <see cref="LoopbackContentServer"/> su un archivio .pws reale, interrogato via HTTP
/// come fa la WebView dell'app.
/// </summary>
[TestFixture]
public sealed class LoopbackContentServerTests : ArchiveTestBase
{
    private readonly List<IDisposable> _resources = [];

    [TearDown]
    public void DisposeResources()
    {
        // In ordine inverso: client, server, provider.
        for (var i = _resources.Count - 1; i >= 0; i--)
            _resources[i].Dispose();
        _resources.Clear();
    }

    private T Track<T>(T resource) where T : IDisposable
    {
        _resources.Add(resource);
        return resource;
    }

    private async Task<(LoopbackContentServer Server, HttpClient Http)> StartServerAsync(
        string pwsPath, string siteId = "docs")
    {
        var provider = Track(await OpenProviderAsync(pwsPath));
        var server   = Track(new LoopbackContentServer(
            provider, siteId, NullLogger<LoopbackContentServer>.Instance));
        var http     = Track(CreateHttpClient(new Uri(server.BaseAddress)));
        return (server, http);
    }

    [Test]
    public async Task BaseAddress_e_su_loopback_con_porta_assegnata()
    {
        var (server, _) = await StartServerAsync(await PackSampleSiteAsync());

        server.BaseAddress.Should().Be($"http://127.0.0.1:{server.Port}/");
        server.Port.Should().BePositive();
        server.SiteId.Should().Be("docs");
    }

    [Test]
    public async Task Get_radice_serve_la_pagina_iniziale()
    {
        var (_, http) = await StartServerAsync(await PackSampleSiteAsync());

        using var response = await http.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Be(SampleSite.IndexHtml);
    }

    [TestCase("assets/site.css", "text/css")]
    [TestCase("assets/app.js",   "application/javascript")]
    [TestCase("img/logo.png",    "image/png")]
    public async Task Get_asset_restituisce_byte_originali_e_content_type(string path, string mime)
    {
        var (_, http) = await StartServerAsync(await PackSampleSiteAsync());

        using var response = await http.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(mime);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(SampleSite.Files[path]);
    }

    [TestCase("guide")]
    [TestCase("guide/")]
    public async Task Get_percorso_senza_estensione_serve_index_html_della_directory(string path)
    {
        var (_, http) = await StartServerAsync(await PackSampleSiteAsync());

        using var response = await http.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(SampleSite.GuideHtml);
    }

    [TestCase("manca.png")]
    [TestCase("manca")]
    [TestCase("assets/manca.css")]
    public async Task Get_risorsa_inesistente_restituisce_404(string path)
    {
        var (_, http) = await StartServerAsync(await PackSampleSiteAsync());

        using var response = await http.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Metodo_diverso_da_GET_restituisce_405()
    {
        var (_, http) = await StartServerAsync(await PackSampleSiteAsync());

        using var response = await http.PostAsync("index.html", new StringContent("x"));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Test]
    public async Task Richieste_concorrenti_ricevono_ciascuna_il_proprio_file()
    {
        // ZipArchive non è thread-safe: il server serializza le letture dallo zip.
        // Molte richieste parallele su file diversi non devono corrompere i contenuti.
        var (_, http) = await StartServerAsync(await PackSampleSiteAsync());
        var paths = SampleSite.Files.Keys.ToArray();

        var requests = Enumerable.Range(0, 40).Select(async i =>
        {
            var path = paths[i % paths.Length];
            var body = await http.GetByteArrayAsync(path);
            return (Path: path, Body: body);
        });
        var results = await Task.WhenAll(requests);

        results.Should().AllSatisfy(r => r.Body.Should().Equal(SampleSite.Files[r.Path]));
    }

    [Test]
    public async Task File_oltre_il_limite_della_cache_viene_servito_integro_a_ogni_richiesta()
    {
        var large = SampleSite.CreateBytes(6 * 1024 * 1024, seed: 11);
        var files = new Dictionary<string, byte[]>(SampleSite.Files) { ["video/grande.bin"] = large };
        var pws   = await PackAsync(PwsSigningKey.None(), ("docs", "Docs", CreateSiteDirectory("docs", files)));
        var (_, http) = await StartServerAsync(pws);

        for (var i = 0; i < 2; i++)
        {
            var body = await http.GetByteArrayAsync("video/grande.bin");
            body.SequenceEqual(large).Should().BeTrue();
        }
    }

    [Test]
    public async Task Due_archivi_aperti_hanno_server_distinti_con_il_proprio_contenuto()
    {
        var blogFiles = new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8("<h1>Blog</h1>") };
        var docsPws   = await PackSampleSiteAsync();
        var blogPws   = await PackAsync(PwsSigningKey.None(), ("blog", "Blog", CreateSiteDirectory("blog", blogFiles)));

        var (docsServer, docsHttp) = await StartServerAsync(docsPws, "docs");
        var (blogServer, blogHttp) = await StartServerAsync(blogPws, "blog");

        docsServer.Port.Should().NotBe(blogServer.Port);
        (await docsHttp.GetStringAsync("/")).Should().Be(SampleSite.IndexHtml);
        (await blogHttp.GetStringAsync("/")).Should().Be("<h1>Blog</h1>");
    }

    [Test]
    public async Task Dopo_Dispose_il_server_non_accetta_connessioni()
    {
        var pws      = await PackSampleSiteAsync();
        var provider = Track(await OpenProviderAsync(pws));
        var server   = new LoopbackContentServer(provider, "docs", NullLogger<LoopbackContentServer>.Instance);
        var address  = new Uri(server.BaseAddress);

        server.Dispose();

        using var http = CreateHttpClient(address);
        var act = async () => await http.GetAsync("/");
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [TestCase("/../b/index.html")]
    [TestCase("/%2e%2e/b/index.html")]
    [TestCase("/sub/../../b/index.html")]
    public async Task I_segmenti_punto_punto_non_escono_dal_sito_servito(string rawPath)
    {
        var pws = await PackAsync(
            PwsSigningKey.None(),
            ("a", "A", CreateSiteDirectory("a", new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8("<h1>Sito A</h1>") })),
            ("b", "B", CreateSiteDirectory("b", new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8("<h1>Sito B</h1>") })));
        var provider = Track(await OpenProviderAsync(pws, defaultSiteId: "a"));
        var server   = Track(new LoopbackContentServer(provider, "a", NullLogger<LoopbackContentServer>.Instance));

        // Socket grezzo: HttpClient normalizzerebbe il percorso prima di inviarlo.
        var control = await SendRawGetAsync(server.Port, "/index.html");
        var raw     = await SendRawGetAsync(server.Port, rawPath);

        control.Should().Contain("Sito A");
        raw.Should().NotContain("Sito B");
    }

    private static async Task<string> SendRawGetAsync(int port, string rawPath)
    {
        using var tcp = new System.Net.Sockets.TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        await using var stream = tcp.GetStream();

        var request = $"GET {rawPath} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(request));

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
