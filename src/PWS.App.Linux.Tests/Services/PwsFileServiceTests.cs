using Microsoft.Extensions.Logging.Abstractions;
using PWS.App.Linux.Services;
using PWS.Core.Models;
using PWS.Core.Providers;
using PWS.Format.Crypto;
using PWS.IntegrationTests.Common;

namespace PWS.App.Linux.Tests.Services;

/// <summary>
/// <see cref="PwsFileService"/> su archivi .pws reali: il sito aperto è servito via HTTP
/// dal server loopback, interrogato come fa la WebView.
/// </summary>
[TestFixture]
public sealed class PwsFileServiceTests : ArchiveTestBase
{
    private const string BlogIndex = "<h1>Blog</h1>";

    private PwsFileService _files = null!;

    [SetUp]
    public void CreateFileService() =>
        _files = new PwsFileService(NullLoggerFactory.Instance, NullLogger<PwsFileService>.Instance);

    [TearDown]
    public void DisposeFileService() => _files.Dispose();

    [Test]
    public void Senza_archivi_aperti_non_ci_sono_provider_ne_server()
    {
        _files.CurrentProvider.Should().BeNull();
        _files.CurrentServer.Should().BeNull();
    }

    [Test]
    public async Task Il_primo_archivio_e_servito_dall_indirizzo_loopback()
    {
        var provider = await OpenProviderAsync(await PackSampleSiteAsync());

        _files.SetProvider(provider);

        _files.CurrentProvider.Should().BeSameAs(provider);
        _files.CurrentServer!.SiteId.Should().Be("docs");
        _files.CurrentServer.BaseAddress.Should().StartWith("http://127.0.0.1:");

        using var http = CreateHttpClient();
        (await http.GetStringAsync(_files.CurrentServer.BaseAddress)).Should().Be(SampleSite.IndexHtml);
    }

    [Test]
    public async Task Aprire_un_secondo_archivio_sostituisce_il_sito_e_spegne_il_vecchio_server()
    {
        var docs = await OpenProviderAsync(await PackSampleSiteAsync());
        var blog = await OpenProviderAsync(await PackBlogAsync());

        _files.SetProvider(docs);
        var oldAddress = _files.CurrentServer!.BaseAddress;
        _files.SetProvider(blog);
        var newAddress = _files.CurrentServer!.BaseAddress;

        using var http = CreateHttpClient();
        (await http.GetStringAsync(newAddress)).Should().Be(BlogIndex);

        var oldServer = async () => await http.GetAsync(oldAddress);
        await oldServer.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task Aprire_un_secondo_archivio_rilascia_il_provider_precedente()
    {
        var docs = await OpenProviderAsync(await PackSampleSiteAsync());
        var blog = await OpenProviderAsync(await PackBlogAsync());

        _files.SetProvider(docs);
        _files.SetProvider(blog);

        // Un provider rilasciato non legge più dall'archivio.
        using var response = await docs.GetAsync(ContentRequest.Get("pws://docs/index.html"));
        response.StatusCode.Should().Be(500);
    }

    [Test]
    public async Task FileOpened_viene_sollevato_con_il_provider_aperto()
    {
        var provider = await OpenProviderAsync(await PackSampleSiteAsync());
        PwsContentProvider? opened = null;
        _files.FileOpened += (_, p) => opened = p;

        _files.SetProvider(provider);

        opened.Should().BeSameAs(provider);
    }

    [Test]
    public async Task Dopo_Dispose_l_indirizzo_non_risponde_piu()
    {
        _files.SetProvider(await OpenProviderAsync(await PackSampleSiteAsync()));
        var address = _files.CurrentServer!.BaseAddress;

        _files.Dispose();

        _files.CurrentProvider.Should().BeNull();
        _files.CurrentServer.Should().BeNull();
        using var http = CreateHttpClient();
        var act = async () => await http.GetAsync(address);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private Task<string> PackBlogAsync() => PackAsync(
        PwsSigningKey.None(),
        ("blog", "Blog", CreateSiteDirectory("blog", new Dictionary<string, byte[]> { ["index.html"] = SampleSite.Utf8(BlogIndex) })));
}
