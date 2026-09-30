using PWS.Core.Models;
using PWS.Core.Providers;
using PWS.Format.Crypto;
using PWS.IntegrationTests.Common;

namespace PWS.IntegrationTests.Providers;

/// <summary>
/// <see cref="PwsContentProvider"/> su archivi .pws reali, creati con <c>PwsPacker</c>
/// e riaperti da disco con <c>PwsReader</c>.
/// </summary>
[TestFixture]
public sealed class PwsContentProviderTests : ArchiveTestBase
{
    [Test]
    public async Task GetAsync_file_esistente_restituisce_contenuto_e_mime()
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get("pws://docs/index.html"));

        response.StatusCode.Should().Be(200);
        response.IsSuccess.Should().BeTrue();
        response.MimeType.Should().Be("text/html");
        response.FinalUri.Should().Be("pws://docs/index.html");
        (await ReadTextAsync(response.Content)).Should().Be(SampleSite.IndexHtml);
    }

    [TestCase("assets/site.css",  "text/css")]
    [TestCase("assets/app.js",    "application/javascript")]
    [TestCase("img/logo.png",     "image/png")]
    [TestCase("guide/index.html", "text/html")]
    public async Task GetAsync_file_annidato_restituisce_byte_originali_e_mime_da_estensione(
        string relativePath, string expectedMime)
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get($"pws://docs/{relativePath}"));

        response.StatusCode.Should().Be(200);
        response.MimeType.Should().Be(expectedMime);
        (await ReadBytesAsync(response.Content)).Should().Equal(SampleSite.Files[relativePath]);
    }

    [Test]
    public async Task GetAsync_file_mancante_restituisce_404()
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get("pws://docs/manca.html"));

        response.StatusCode.Should().Be(404);
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task GetAsync_letture_ripetute_restituiscono_lo_stesso_contenuto()
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        for (var i = 0; i < 3; i++)
        {
            using var response = await provider.GetAsync(ContentRequest.Get("pws://docs/assets/site.css"));
            (await ReadTextAsync(response.Content)).Should().Be(SampleSite.Css);
        }
    }

    [Test]
    public async Task GetAsync_file_oltre_il_limite_della_cache_viene_servito_integro()
    {
        // Più grande di MaxCacheBytes: il provider lo legge ogni volta dallo zip.
        var large = SampleSite.CreateBytes((int)PwsContentProvider.MaxCacheBytes + 1024, seed: 7);
        var files = new Dictionary<string, byte[]>(SampleSite.Files) { ["video/grande.bin"] = large };
        var pws   = await PackAsync(PwsSigningKey.None(), ("docs", "Docs", CreateSiteDirectory("docs", files)));
        using var provider = await OpenProviderAsync(pws);

        for (var i = 0; i < 2; i++)
        {
            using var response = await provider.GetAsync(ContentRequest.Get("pws://docs/video/grande.bin"));

            response.StatusCode.Should().Be(200);
            response.MimeType.Should().Be("application/octet-stream");
            (await ReadBytesAsync(response.Content)).SequenceEqual(large).Should().BeTrue();
        }
    }

    [Test]
    public async Task Con_un_solo_sito_usa_il_sito_di_default_per_uri_senza_host()
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        provider.DefaultSiteId.Should().Be("docs");
        provider.CanHandle(new Uri("pws:///index.html")).Should().BeTrue();

        using var response = await provider.GetAsync(ContentRequest.Get("pws:///index.html"));

        response.StatusCode.Should().Be(200);
        (await ReadTextAsync(response.Content)).Should().Be(SampleSite.IndexHtml);
    }

    [Test]
    public async Task Con_piu_siti_ogni_host_serve_il_proprio_sito()
    {
        var blogFiles = new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8("<h1>Blog</h1>") };
        var pws = await PackAsync(
            PwsSigningKey.None(),
            ("docs", "Docs", CreateSiteDirectory("docs")),
            ("blog", "Blog", CreateSiteDirectory("blog", blogFiles)));
        using var provider = await OpenProviderAsync(pws);

        provider.DefaultSiteId.Should().BeNull();
        provider.CanHandle(new Uri("pws:///index.html")).Should().BeFalse();

        using var docs = await provider.GetAsync(ContentRequest.Get("pws://docs/index.html"));
        using var blog = await provider.GetAsync(ContentRequest.Get("pws://blog/index.html"));

        (await ReadTextAsync(docs.Content)).Should().Be(SampleSite.IndexHtml);
        (await ReadTextAsync(blog.Content)).Should().Be("<h1>Blog</h1>");
    }

    [Test]
    public async Task Con_piu_siti_il_sito_di_default_esplicito_serve_uri_senza_host()
    {
        var blogFiles = new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8("<h1>Blog</h1>") };
        var pws = await PackAsync(
            PwsSigningKey.None(),
            ("docs", "Docs", CreateSiteDirectory("docs")),
            ("blog", "Blog", CreateSiteDirectory("blog", blogFiles)));
        using var provider = await OpenProviderAsync(pws, defaultSiteId: "blog");

        using var response = await provider.GetAsync(ContentRequest.Get("pws:///index.html"));

        (await ReadTextAsync(response.Content)).Should().Be("<h1>Blog</h1>");
    }

    [TestCase("pws://docs/index.html",  true)]
    [TestCase("pws://altro/index.html", false)]
    [TestCase("pws://home",             false)]
    [TestCase("http://docs/index.html", false)]
    public async Task CanHandle_accetta_solo_schema_pws_con_host_di_un_sito_dell_archivio(
        string uri, bool expected)
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        provider.CanHandle(new Uri(uri)).Should().Be(expected);
    }

    [Test]
    public async Task GetAsync_su_archivio_firmato_ecdsa_serve_i_file()
    {
        var (key, _, _) = PwsSigningKey.GenerateEcDsa();
        var pws = await PackAsync(key, ("docs", "Docs", CreateSiteDirectory("docs")));
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get("pws://docs/index.html"));

        response.StatusCode.Should().Be(200);
        (await ReadTextAsync(response.Content)).Should().Be(SampleSite.IndexHtml);
    }

    [Test]
    public async Task GetAsync_dopo_Dispose_restituisce_500()
    {
        var pws = await PackSampleSiteAsync();
        var provider = await OpenProviderAsync(pws);
        provider.Dispose();

        using var response = await provider.GetAsync(ContentRequest.Get("pws://docs/index.html"));

        response.StatusCode.Should().Be(500);
    }

    [TestCase("pws://DOCS/index.html")]
    [TestCase("pws://Docs/index.html")]
    public async Task L_host_del_sito_non_distingue_le_maiuscole(string uri)
    {
        var pws = await PackSampleSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        provider.CanHandle(new Uri(uri)).Should().BeTrue();
        using var response = await provider.GetAsync(ContentRequest.Get(uri));

        response.StatusCode.Should().Be(200);
        (await ReadTextAsync(response.Content)).Should().Be(SampleSite.IndexHtml);
    }

    [TestCase("icona.svg",     "image/svg+xml")]
    [TestCase("font.woff2",    "font/woff2")]
    [TestCase("dati.bin",      "application/octet-stream")]
    [TestCase("senza-estensione", "application/octet-stream")]
    public async Task Il_tipo_mime_dipende_dall_estensione(string fileName, string expectedMime)
    {
        var files = new Dictionary<string, byte[]> { [fileName] = SampleSite.Utf8("x") };
        var pws   = await PackAsync(PwsSigningKey.None(), ("docs", "Docs", CreateSiteDirectory("docs", files)));
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get($"pws://docs/{fileName}"));

        response.StatusCode.Should().Be(200);
        response.MimeType.Should().Be(expectedMime);
    }

    [TestCase("pws://a/../b/index.html")]
    [TestCase("pws://a/%2e%2e/b/index.html")]
    [TestCase("pws://a/sub/../../b/index.html")]
    public async Task I_segmenti_punto_punto_non_escono_dal_sito_richiesto(string uri)
    {
        var pws = await PackTwoSitesAsync();
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get(uri));

        if (response.IsSuccess)
            (await ReadTextAsync(response.Content)).Should().NotBe(SiteBIndex);
    }

    [TestCase("pagine/citt%C3%A0%20vecchia.html", "pagine/città vecchia.html")]
    [TestCase("100%25.html",                      "100%.html")]
    [TestCase("100%2525.html",                    "100%25.html")]
    public async Task I_percorsi_codificati_vengono_decodificati_una_sola_volta(
        string requestedPath, string expectedFile)
    {
        var pws = await PackEncodedNamesSiteAsync();
        using var provider = await OpenProviderAsync(pws);

        using var response = await provider.GetAsync(ContentRequest.Get($"pws://docs/{requestedPath}"));

        response.StatusCode.Should().Be(200);
        (await ReadTextAsync(response.Content)).Should().Be(expectedFile);
    }

    private const string SiteAIndex = "<h1>Sito A</h1>";
    private const string SiteBIndex = "<h1>Sito B</h1>";

    private Task<string> PackTwoSitesAsync() => PackAsync(
        PwsSigningKey.None(),
        ("a", "A", CreateSiteDirectory("a", new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8(SiteAIndex) })),
        ("b", "B", CreateSiteDirectory("b", new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8(SiteBIndex) })));

    /// <summary>Sito i cui file contengono il proprio nome: la risposta dice quale file è stato servito.</summary>
    private Task<string> PackEncodedNamesSiteAsync()
    {
        string[] names = ["pagine/città vecchia.html", "100%.html", "100%25.html"];
        var files = names.ToDictionary(n => n, SampleSite.Utf8);
        return PackAsync(PwsSigningKey.None(), ("docs", "Docs", CreateSiteDirectory("docs", files)));
    }
}
