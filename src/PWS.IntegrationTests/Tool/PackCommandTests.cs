using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using PWS.Format.Crypto;
using PWS.Format.Reading;
using PWS.IntegrationTests.Common;
using PWS.Tool.Commands;

namespace PWS.IntegrationTests.Tool;

/// <summary>
/// Verbo <c>pack</c> di pwstool: da una directory o da uno zip reali a un archivio .pws
/// che <see cref="PwsReader"/> riapre e verifica.
/// </summary>
[TestFixture]
public sealed class PackCommandTests : ArchiveTestBase
{
    private static Task<int> PackAsync(PackOptions options) =>
        PackCommand.RunAsync(options, NullLogger.Instance);

    [Test]
    public async Task Pack_da_directory_crea_un_archivio_leggibile_con_tutti_i_file()
    {
        var output = PathInWorkDir("sito.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source     = CreateSiteDirectory("docs"),
            Output     = output,
            SiteId     = "docs",
            Title      = "Documentazione",
            EntryPoint = "index.html",
        });

        exitCode.Should().Be(0);
        using var reader = await PwsReader.OpenAsync(output);
        var site = reader.Sites.Should().ContainSingle().Subject;
        site.SiteId.Should().Be("docs");
        site.Title.Should().Be("Documentazione");
        site.EntryPoint.Should().Be("index.html");
        site.FileCount.Should().Be(SampleSite.Files.Count);
        site.IsVerified.Should().BeFalse();
        reader.FileSystem.ListFiles("docs").Select(f => f.RelativePath)
            .Should().BeEquivalentTo(SampleSite.Files.Keys);

        foreach (var (path, content) in SampleSite.Files)
        {
            await using var stream = reader.FileSystem.OpenSiteFile("docs", path);
            (await ReadBytesAsync(stream)).Should().Equal(content);
        }
    }

    [Test]
    public async Task Pack_senza_titolo_usa_l_id_del_sito()
    {
        var output = PathInWorkDir("sito.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = CreateSiteDirectory("manuale"),
            Output = output,
            SiteId = "manuale",
        });

        exitCode.Should().Be(0);
        using var reader = await PwsReader.OpenAsync(output);
        reader.Sites.Should().ContainSingle().Which.Title.Should().Be("manuale");
    }

    [Test]
    public async Task Pack_da_zip_include_gli_stessi_file_della_directory()
    {
        var zipPath = PathInWorkDir("sorgente.zip");
        ZipFile.CreateFromDirectory(CreateSiteDirectory("docs"), zipPath);
        var output = PathInWorkDir("da-zip.pws");

        var exitCode = await PackAsync(new PackOptions { Source = zipPath, Output = output, SiteId = "docs" });

        exitCode.Should().Be(0);
        using var reader = await PwsReader.OpenAsync(output);
        reader.FileSystem.ListFiles("docs").Select(f => f.RelativePath)
            .Should().BeEquivalentTo(SampleSite.Files.Keys);

        await using var css = reader.FileSystem.OpenSiteFile("docs", "assets/site.css");
        (await ReadTextAsync(css)).Should().Be(SampleSite.Css);
    }

    [Test]
    public async Task Pack_con_firma_ecdsa_salva_la_chiave_pubblica_e_produce_token_verificati()
    {
        var output = PathInWorkDir("firmato.pws");
        var keyOut = PathInWorkDir("chiave.pub");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = CreateSiteDirectory("docs"),
            Output = output,
            SiteId = "docs",
            Sign   = "ecdsa",
            KeyOut = keyOut,
        });

        exitCode.Should().Be(0);
        var exportedKey = (await File.ReadAllTextAsync(keyOut)).Trim();
        exportedKey.Should().StartWith("ES256:");

        using var reader = await PwsReader.OpenAsync(output, new PwsOpenOptions { RequireSignedTokens = true });
        reader.Manifest.PublicKey.Should().Be(exportedKey);
        reader.Sites.Should().ContainSingle().Which.IsVerified.Should().BeTrue();
    }

    [Test]
    public async Task Pack_con_firma_hmac_richiede_il_segreto_per_aprire_l_archivio()
    {
        var output = PathInWorkDir("hmac.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = CreateSiteDirectory("docs"),
            Output = output,
            SiteId = "docs",
            Sign   = "hmac:Segreto-Condiviso",
        });

        exitCode.Should().Be(0);

        // Il segreto conserva maiuscole e minuscole.
        using (var reader = await PwsReader.OpenAsync(output, new PwsOpenOptions
               {
                   VerificationKey = PwsSigningKey.FromHmac("Segreto-Condiviso"),
               }))
        {
            reader.Manifest.PublicKey.Should().BeNull();
            reader.Sites.Should().ContainSingle().Which.IsVerified.Should().BeTrue();
        }

        var withoutKey = async () => { using var reader = await PwsReader.OpenAsync(output); };
        await withoutKey.Should().ThrowAsync<InvalidDataException>();
    }

    [Test]
    public async Task Pack_crea_le_directory_mancanti_del_percorso_di_output()
    {
        var output = Path.Combine(WorkDir, "dist", "archivi", "sito.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = CreateSiteDirectory("docs"),
            Output = output,
        });

        exitCode.Should().Be(0);
        File.Exists(output).Should().BeTrue();
    }

    [Test]
    public async Task Pack_con_sorgente_inesistente_fallisce_senza_creare_l_archivio()
    {
        var output = PathInWorkDir("sito.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = Path.Combine(WorkDir, "non-esiste"),
            Output = output,
        });

        exitCode.Should().Be(1);
        File.Exists(output).Should().BeFalse();
    }

    [TestCase("rsa")]
    [TestCase("hmac:")]
    public async Task Pack_con_firma_non_valida_fallisce_senza_creare_l_archivio(string sign)
    {
        var output = PathInWorkDir("sito.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = CreateSiteDirectory("docs"),
            Output = output,
            Sign   = sign,
        });

        exitCode.Should().Be(1);
        File.Exists(output).Should().BeFalse();
    }

    [TestCase("mio sito")]
    [TestCase("docs_1")]
    [TestCase("città")]
    public async Task Pack_con_id_non_valido_come_host_fallisce_senza_creare_l_archivio(string siteId)
    {
        var output = PathInWorkDir("sito.pws");

        var exitCode = await PackAsync(new PackOptions
        {
            Source = CreateSiteDirectory("docs"),
            Output = output,
            SiteId = siteId,
        });

        exitCode.Should().Be(1);
        File.Exists(output).Should().BeFalse();
    }
}
