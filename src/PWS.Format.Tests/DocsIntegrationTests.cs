using System.Diagnostics;
using PWS.Format.Crypto;
using PWS.Format.Packing;
using PWS.Format.Reading;
using Xunit;

namespace PWS.Format.Tests;

/// <summary>
/// Test di integrazione end-to-end: builda la documentazione Docusaurus reale del progetto,
/// la pacchetta in un .pws, la rilegge e verifica l'integrità.
/// </summary>
public sealed class DocsIntegrationTests
{
    private const string RepoRoot = "/data1/repo/PWS_MAUI";
    private const string DocsBuildDir = RepoRoot + "/docs/build";
    private const string DocsDir = RepoRoot + "/docs";

    // ── Build della documentazione ───────────────────────────────────────────

    [Fact]
    public async Task BuildDocs_CreatesPws_ReadsSuccessfully()
    {
        // 1. Build Docusaurus (se non già buildata)
        await EnsureDocsBuildExists();

        // 2. Pack docs/build/ → docs.pws
        var pwsPath = Path.Combine(Path.GetTempPath(), $"docs-test-{Guid.NewGuid()}.pws");
        try
        {
            var packer = new PwsPacker();
            await packer.PackAsync(
                new PwsPackOptions
                {
                    Sites =
                    [
                        new PwsSiteSource
                        {
                            Id              = "docs",
                            Title           = "PWS Browser Documentation",
                            EntryPoint      = "index.html",
                            SourceDirectory = DocsBuildDir,
                        },
                    ],
                    SigningKey = PwsSigningKey.None(), // Unsigned per semplicità
                },
                pwsPath);

            Assert.True(File.Exists(pwsPath), $"Il file .pws non è stato creato: {pwsPath}");

            // 3. Leggi il .pws appena creato
            using var reader = await PwsReader.OpenAsync(pwsPath);

            // 4. Verifica manifest
            Assert.Equal("1", reader.Manifest.Version);
            Assert.Single(reader.Manifest.Sites);
            Assert.Equal("docs", reader.Manifest.Sites[0].Id);

            // 5. Verifica site claims
            var site = reader.Sites[0];
            Assert.Equal("docs", site.SiteId);
            Assert.Equal("PWS Browser Documentation", site.Title);
            Assert.Equal("index.html", site.EntryPoint);
            Assert.StartsWith("sha256:", site.ContentHash);
            Assert.True(site.FileCount > 0, "FileCount deve essere > 0");

            // 6. Verifica filesystem virtuale
            var files = reader.FileSystem.ListFiles("docs");
            Assert.NotEmpty(files);

            // 7. Verifica file chiave
            Assert.True(reader.FileSystem.FileExists("sites/docs/index.html"),
                "index.html deve esistere");

            // 8. Leggi index.html e verifica che contenga "PWS"
            using var indexStream = reader.FileSystem.OpenSiteFile("docs", "index.html");
            var indexHtml = await new StreamReader(indexStream).ReadToEndAsync();
            Assert.Contains("PWS", indexHtml, StringComparison.OrdinalIgnoreCase);

            // 9. Verifica che esistano assets (CSS/JS)
            var assetFiles = files.Where(f => f.RelativePath.StartsWith("assets/")).ToList();
            Assert.NotEmpty(assetFiles);
        }
        finally
        {
            if (File.Exists(pwsPath))
                File.Delete(pwsPath);
        }
    }

    [Fact]
    public async Task BuildDocs_SignedWithEcDsa_VerifiesCorrectly()
    {
        await EnsureDocsBuildExists();

        var pwsPath = Path.Combine(Path.GetTempPath(), $"docs-signed-{Guid.NewGuid()}.pws");
        try
        {
            // Genera chiave ECDSA
            var (fullKey, _, _) = PwsSigningKey.GenerateEcDsa();

            var packer = new PwsPacker();
            await packer.PackAsync(
                new PwsPackOptions
                {
                    Sites =
                    [
                        new PwsSiteSource
                        {
                            Id              = "docs",
                            Title           = "PWS Docs (Signed)",
                            SourceDirectory = DocsBuildDir,
                        },
                    ],
                    SigningKey = fullKey,
                },
                pwsPath);

            // Apri e verifica firma
            using var reader = await PwsReader.OpenAsync(pwsPath);
            Assert.NotNull(reader.Manifest.PublicKey);
            Assert.StartsWith("ES256:", reader.Manifest.PublicKey);
            Assert.True(reader.Sites[0].IsVerified, "Il sito deve risultare verificato");
        }
        finally
        {
            if (File.Exists(pwsPath))
                File.Delete(pwsPath);
        }
    }

    [Fact]
    public async Task BuildDocs_RequireSignedTokens_UnsignedThrows()
    {
        await EnsureDocsBuildExists();

        var pwsPath = Path.Combine(Path.GetTempPath(), $"docs-unsigned-{Guid.NewGuid()}.pws");
        try
        {
            var packer = new PwsPacker();
            await packer.PackAsync(
                new PwsPackOptions
                {
                    Sites =
                    [
                        new PwsSiteSource
                        {
                            Id              = "docs",
                            Title           = "PWS Docs",
                            SourceDirectory = DocsBuildDir,
                        },
                    ],
                    SigningKey = PwsSigningKey.None(),
                },
                pwsPath);

            // Tentativo di aprire con RequireSignedTokens = true deve fallire
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                PwsReader.OpenAsync(pwsPath, new PwsOpenOptions { RequireSignedTokens = true }));
        }
        finally
        {
            if (File.Exists(pwsPath))
                File.Delete(pwsPath);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task EnsureDocsBuildExists()
    {
        // Se docs/build/ non esiste o è vuota, builda
        if (!Directory.Exists(DocsBuildDir) ||
            !Directory.EnumerateFileSystemEntries(DocsBuildDir).Any())
        {
            await BuildDocs();
        }

        // Verifica che index.html esista
        var indexPath = Path.Combine(DocsBuildDir, "index.html");
        if (!File.Exists(indexPath))
            throw new InvalidOperationException(
                $"docs/build/index.html non trovato dopo la build. " +
                $"Verifica che 'pnpm build' funzioni correttamente in {DocsDir}");
    }

    private static async Task BuildDocs()
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "pnpm",
            Arguments              = "build",
            WorkingDirectory       = DocsDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Impossibile avviare 'pnpm build'");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error  = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"pnpm build fallito (exit {process.ExitCode}):\n{output}\n{error}");
    }
}

