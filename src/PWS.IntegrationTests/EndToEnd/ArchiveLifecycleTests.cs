using Microsoft.Extensions.Logging.Abstractions;
using PWS.Core.Hosting;
using PWS.Core.Providers;
using PWS.Format.Reading;
using PWS.IntegrationTests.Common;
using PWS.Tool.Commands;

namespace PWS.IntegrationTests.EndToEnd;

/// <summary>
/// Percorso completo di un sito, senza la UI: <c>pwstool pack</c> crea l'archivio firmato,
/// <c>pwstool validate</c> lo verifica, l'app lo apre con <see cref="PwsReader"/> e
/// <see cref="PwsContentProvider"/> e la WebView lo scarica dal <see cref="LoopbackContentServer"/>.
/// </summary>
[TestFixture]
public sealed class ArchiveLifecycleTests : ArchiveTestBase
{
    [Test]
    public async Task Sito_impacchettato_dalla_cli_viene_servito_via_http_identico_all_originale()
    {
        // pack
        var archive = PathInWorkDir("sito.pws");
        var keyFile = PathInWorkDir("sito.pub");
        var packed  = await PackCommand.RunAsync(new PackOptions
        {
            Source = CreateSiteDirectory("docs"),
            Output = archive,
            SiteId = "docs",
            Title  = "Documentazione",
            Sign   = "ecdsa",
            KeyOut = keyFile,
        }, NullLogger.Instance);
        packed.Should().Be(0);

        // validate
        var validated = await ValidateCommand.RunAsync(new ValidateOptions
        {
            FilePath      = archive,
            RequireSigned = true,
            Key           = keyFile,
        }, NullLogger.Instance);
        validated.Should().Be(0);

        // apertura e pubblicazione su loopback, come in PwsFileService.SetProvider
        var reader = await PwsReader.OpenAsync(archive, new PwsOpenOptions { RequireSignedTokens = true });
        using var provider = new PwsContentProvider(reader);
        provider.DefaultSiteId.Should().Be("docs");

        using var server = new LoopbackContentServer(
            provider, provider.DefaultSiteId!, NullLogger<LoopbackContentServer>.Instance);
        using var http = CreateHttpClient(new Uri(server.BaseAddress));

        // ogni file del sito arriva al browser byte per byte
        foreach (var (path, content) in SampleSite.Files)
            (await http.GetByteArrayAsync(path)).Should().Equal(content, because: $"'{path}' deve essere servito intatto");

        (await http.GetStringAsync("/")).Should().Be(SampleSite.IndexHtml);
    }
}
