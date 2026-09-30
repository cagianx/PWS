using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using PWS.Format.Crypto;
using PWS.IntegrationTests.Common;
using PWS.Tool.Commands;

namespace PWS.IntegrationTests.Tool;

/// <summary>
/// Verbo <c>validate</c> di pwstool su archivi reali: integri, firmati, manomessi, corrotti.
/// </summary>
[TestFixture]
public sealed class ValidateCommandTests : ArchiveTestBase
{
    private static Task<int> ValidateAsync(ValidateOptions options) =>
        ValidateCommand.RunAsync(options, NullLogger.Instance);

    [TestCase(false)]
    [TestCase(true)]
    public async Task Validate_archivio_integro_non_firmato_ha_successo(bool verbose)
    {
        var pws = await PackSampleSiteAsync();

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws, Verbose = verbose });

        exitCode.Should().Be(0);
    }

    [Test]
    public async Task Validate_con_require_signed_rifiuta_archivio_non_firmato()
    {
        var pws = await PackSampleSiteAsync();

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws, RequireSigned = true });

        exitCode.Should().Be(1);
    }

    [Test]
    public async Task Validate_con_require_signed_accetta_archivio_ecdsa_con_chiave_nel_manifest()
    {
        var (key, _, _) = PwsSigningKey.GenerateEcDsa();
        var pws = await PackAsync(key, ("docs", "Docs", CreateSiteDirectory("docs")));

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws, RequireSigned = true });

        exitCode.Should().Be(0);
    }

    [Test]
    public async Task Validate_accetta_la_chiave_pubblica_da_file_o_come_stringa()
    {
        var (key, _, export) = PwsSigningKey.GenerateEcDsa();
        var pws     = await PackAsync(key, ("docs", "Docs", CreateSiteDirectory("docs")));
        var keyFile = PathInWorkDir("chiave.pub");
        await File.WriteAllTextAsync(keyFile, export + Environment.NewLine);

        var fromFile   = await ValidateAsync(new ValidateOptions { FilePath = pws, Key = keyFile });
        var fromString = await ValidateAsync(new ValidateOptions { FilePath = pws, Key = export });

        fromFile.Should().Be(0);
        fromString.Should().Be(0);
    }

    [Test]
    public async Task Validate_rifiuta_una_chiave_pubblica_diversa_da_quella_di_firma()
    {
        var (key, _, _)       = PwsSigningKey.GenerateEcDsa();
        var (_, _, otherKey)  = PwsSigningKey.GenerateEcDsa();
        var pws = await PackAsync(key, ("docs", "Docs", CreateSiteDirectory("docs")));

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws, Key = otherKey });

        exitCode.Should().Be(1);
    }

    [TestCase("hmac:segreto", 0)]
    [TestCase("hmac:sbagliato", 1)]
    [TestCase(null, 1)]
    public async Task Validate_archivio_hmac_ha_successo_solo_con_il_segreto_giusto(string? key, int expected)
    {
        var pws = await PackAsync(PwsSigningKey.FromHmac("segreto"), ("docs", "Docs", CreateSiteDirectory("docs")));

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws, Key = key });

        exitCode.Should().Be(expected);
    }

    [Test]
    public async Task Validate_file_inesistente_fallisce()
    {
        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = PathInWorkDir("manca.pws") });

        exitCode.Should().Be(1);
    }

    [Test]
    public async Task Validate_file_che_non_e_uno_zip_fallisce()
    {
        var path = PathInWorkDir("corrotto.pws");
        await File.WriteAllBytesAsync(path, SampleSite.CreateBytes(4096, seed: 3));

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = path });

        exitCode.Should().Be(1);
    }

    [Test]
    public async Task Validate_zip_senza_manifest_fallisce()
    {
        var path = PathInWorkDir("senza-manifest.pws");
        ZipFile.CreateFromDirectory(CreateSiteDirectory("docs"), path);

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = path });

        exitCode.Should().Be(1);
    }

    [Test]
    public async Task Validate_archivio_con_file_modificato_dopo_il_packing_fallisce()
    {
        var pws = await PackSampleSiteAsync();
        ReplaceEntry(pws, "sites/docs/index.html", "<h1>contenuto manomesso</h1>");

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws });

        exitCode.Should().Be(1);
    }

    [Test]
    public async Task Validate_archivio_con_file_aggiunto_dopo_il_packing_fallisce()
    {
        var pws = await PackSampleSiteAsync();
        using (var zip = ZipFile.Open(pws, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry("sites/docs/intruso.js");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("alert('x')");
        }

        var exitCode = await ValidateAsync(new ValidateOptions { FilePath = pws });

        exitCode.Should().Be(1);
    }

    /// <summary>Sostituisce il contenuto di una entry dell'archivio, come farebbe un attaccante.</summary>
    private static void ReplaceEntry(string archivePath, string entryName, string newContent)
    {
        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        zip.GetEntry(entryName)!.Delete();
        var entry = zip.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(newContent);
    }
}
