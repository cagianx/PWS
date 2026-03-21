using System.Text;
using PWS.Format.Crypto;
using PWS.Format.Packing;
using PWS.Format.Reading;
using Xunit;

namespace PWS.Format.Tests;

/// <summary>
/// End-to-end tests: pack a site, open it with PwsReader, verify manifest,
/// claims and virtual filesystem behaviour.
/// </summary>
public sealed class PackerReaderTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PwsSiteSource MakeSite(
        string id      = "docs",
        string title   = "My Docs",
        string entry   = "index.html",
        string content = "<html>hello</html>")
    {
        var site = new PwsSiteSource { Id = id, Title = title, EntryPoint = entry };
        site.AddFile("index.html",      () => Utf8Stream(content));
        site.AddFile("assets/main.css", () => Utf8Stream("body{}"));
        return site;
    }

    private static Stream Utf8Stream(string s) =>
        new MemoryStream(Encoding.UTF8.GetBytes(s));

    private static async Task<MemoryStream> PackAsync(PwsPackOptions options)
    {
        var ms = new MemoryStream();
        await new PwsPacker().PackAsync(options, ms);
        ms.Position = 0;
        return ms;
    }

    // ── Manifest integrity ───────────────────────────────────────────────────

    [Fact]
    public async Task Manifest_Version_IsOne()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite()] });
        using var reader = await PwsReader.OpenAsync(ms);
        Assert.Equal("1", reader.Manifest.Version);
    }

    [Fact]
    public async Task Manifest_Created_IsRecent()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var ms     = await PackAsync(new PwsPackOptions { Sites = [MakeSite()] });
        var after  = DateTimeOffset.UtcNow.AddSeconds(2);

        using var reader = await PwsReader.OpenAsync(ms);
        Assert.InRange(reader.Manifest.Created, before, after);
    }

    // ── None (unsigned) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Pack_None_ManifestHasNullPublicKey()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite()] });
        using var reader = await PwsReader.OpenAsync(ms);
        Assert.Null(reader.Manifest.PublicKey);
    }

    [Fact]
    public async Task Pack_None_SiteClaimsCorrect()
    {
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite("docs", "My Docs", "index.html")],
        });
        using var reader = await PwsReader.OpenAsync(ms);

        var site = Assert.Single(reader.Sites);
        Assert.Equal("docs",     site.SiteId);
        Assert.Equal("My Docs",  site.Title);
        Assert.Equal("index.html", site.EntryPoint);
        Assert.StartsWith("sha256:", site.ContentHash);
        Assert.Equal(2,          site.FileCount);
        Assert.False(site.IsVerified);
    }

    // ── HMAC signing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Pack_Hmac_VerifiesCorrectly()
    {
        var key = PwsSigningKey.FromHmac("super-secret");
        var ms  = await PackAsync(new PwsPackOptions
        {
            Sites      = [MakeSite()],
            SigningKey = key,
        });

        using var reader = await PwsReader.OpenAsync(ms,
            new PwsOpenOptions { VerificationKey = key });

        Assert.True(reader.Sites[0].IsVerified);
    }

    [Fact]
    public async Task Pack_Hmac_WrongKey_Throws()
    {
        var signingKey  = PwsSigningKey.FromHmac("correct");
        var wrongKey    = PwsSigningKey.FromHmac("wrong");
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite()], SigningKey = signingKey,
        });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            PwsReader.OpenAsync(ms, new PwsOpenOptions { VerificationKey = wrongKey }));
    }

    // ── ECDSA signing ────────────────────────────────────────────────────────

    [Fact]
    public async Task Pack_EcDsa_PublicKeyEmbeddedInManifest()
    {
        var (full, _, export) = PwsSigningKey.GenerateEcDsa();
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite()], SigningKey = full,
        });

        using var reader = await PwsReader.OpenAsync(ms);
        Assert.Equal(export, reader.Manifest.PublicKey);
    }

    [Fact]
    public async Task Pack_EcDsa_AutoVerifiesFromEmbeddedPublicKey()
    {
        var (full, _, _) = PwsSigningKey.GenerateEcDsa();
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite()], SigningKey = full,
        });

        // No VerificationKey needed — public key is read from manifest
        using var reader = await PwsReader.OpenAsync(ms);
        Assert.True(reader.Sites[0].IsVerified);
    }

    [Fact]
    public async Task Pack_EcDsa_WrongVerificationKey_Throws()
    {
        var (full, _, _)        = PwsSigningKey.GenerateEcDsa();
        var (_, wrongPub, _)    = PwsSigningKey.GenerateEcDsa();
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite()], SigningKey = full,
        });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            PwsReader.OpenAsync(ms, new PwsOpenOptions { VerificationKey = wrongPub }));
    }

    // ── Content hash ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Pack_ContentHash_MatchesMerkleHasher()
    {
        var htmlBytes = Encoding.UTF8.GetBytes("<html>hello</html>");
        var cssBytes  = Encoding.UTF8.GetBytes("body{}");

        var site = new PwsSiteSource { Id = "test", Title = "T" };
        site.AddFile("index.html",      () => new MemoryStream(htmlBytes));
        site.AddFile("assets/main.css", () => new MemoryStream(cssBytes));

        var ms = await PackAsync(new PwsPackOptions { Sites = [site] });
        using var reader = await PwsReader.OpenAsync(ms);

        var expectedHash = MerkleHasher.Compute(
        [
            ("assets/main.css", cssBytes),   // order shouldn't matter
            ("index.html",      htmlBytes),
        ]);

        Assert.Equal(expectedHash, reader.Sites[0].ContentHash);
    }

    [Fact]
    public async Task Pack_ContentHash_ChangeInFile_ProducesDifferentHash()
    {
        static async Task<string> HashFor(string content)
        {
            var site = new PwsSiteSource { Id = "s", Title = "S" };
            site.AddFile("index.html", () => Utf8Stream(content));
            var ms = await PackAsync(new PwsPackOptions { Sites = [site] });
            using var reader = await PwsReader.OpenAsync(ms);
            return reader.Sites[0].ContentHash;
        }

        var h1 = await HashFor("<html>v1</html>");
        var h2 = await HashFor("<html>v2</html>");
        Assert.NotEqual(h1, h2);
    }

    // ── Multiple sites ───────────────────────────────────────────────────────

    [Fact]
    public async Task Pack_MultipleSites_AllSitesPresent()
    {
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite("docs"), MakeSite("blog"), MakeSite("api")],
        });
        using var reader = await PwsReader.OpenAsync(ms);

        Assert.Equal(3, reader.Manifest.Sites.Count);
        Assert.Equal(3, reader.Sites.Count);
        Assert.Equal(["docs", "blog", "api"],
            reader.Sites.Select(s => s.SiteId).ToArray());
    }

    [Fact]
    public async Task Pack_MultipleSites_GetSite_ReturnsCorrect()
    {
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite("docs", "Docs"), MakeSite("blog", "Blog")],
        });
        using var reader = await PwsReader.OpenAsync(ms);

        Assert.Equal("Docs", reader.GetSite("docs")!.Title);
        Assert.Equal("Blog", reader.GetSite("blog")!.Title);
        Assert.Null(reader.GetSite("missing"));
    }

    // ── RequireSignedTokens ───────────────────────────────────────────────────

    [Fact]
    public async Task Open_RequireSignedTokens_UnsignedPackage_Throws()
    {
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite()], SigningKey = PwsSigningKey.None(),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            PwsReader.OpenAsync(ms, new PwsOpenOptions { RequireSignedTokens = true }));
    }

    [Fact]
    public async Task Open_RequireSignedTokens_SignedPackage_Succeeds()
    {
        var (full, _, _) = PwsSigningKey.GenerateEcDsa();
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite()], SigningKey = full,
        });

        using var reader = await PwsReader.OpenAsync(ms,
            new PwsOpenOptions { RequireSignedTokens = true });

        Assert.True(reader.Sites[0].IsVerified);
    }

    // ── Virtual filesystem ────────────────────────────────────────────────────

    [Fact]
    public async Task FileSystem_ListFiles_NoFilter_ReturnsAll()
    {
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite("a"), MakeSite("b")],
        });
        using var reader = await PwsReader.OpenAsync(ms);
        var all = reader.FileSystem.ListFiles();

        // Each site has 2 files → 4 total
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public async Task FileSystem_ListFiles_FilterBySite_ReturnsOnlySiteFiles()
    {
        var ms = await PackAsync(new PwsPackOptions
        {
            Sites = [MakeSite("docs"), MakeSite("blog")],
        });
        using var reader = await PwsReader.OpenAsync(ms);

        var docsFiles = reader.FileSystem.ListFiles("docs");
        Assert.Equal(2, docsFiles.Count);
        Assert.All(docsFiles, f => Assert.Equal("docs", f.SiteId));
    }

    [Fact]
    public async Task FileSystem_ListFiles_ContainsCorrectRelativePaths()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite()] });
        using var reader = await PwsReader.OpenAsync(ms);

        var paths = reader.FileSystem.ListFiles().Select(f => f.RelativePath).ToHashSet();
        Assert.Contains("index.html",      paths);
        Assert.Contains("assets/main.css", paths);
    }

    [Fact]
    public async Task FileSystem_OpenSiteFile_ReturnsCorrectContent()
    {
        const string html = "<html>unique-content-xyz</html>";
        var site = new PwsSiteSource { Id = "docs", Title = "Docs" };
        site.AddFile("index.html", () => Utf8Stream(html));

        var ms = await PackAsync(new PwsPackOptions { Sites = [site] });
        using var reader = await PwsReader.OpenAsync(ms);

        using var stream  = reader.FileSystem.OpenSiteFile("docs", "index.html");
        var content = await new StreamReader(stream).ReadToEndAsync();
        Assert.Equal(html, content);
    }

    [Fact]
    public async Task FileSystem_OpenFile_ByArchivePath_Succeeds()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite("s")] });
        using var reader = await PwsReader.OpenAsync(ms);

        using var stream = reader.FileSystem.OpenFile("sites/s/index.html");
        Assert.NotNull(stream);
    }

    [Fact]
    public async Task FileSystem_FileExists_KnownFile_ReturnsTrue()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite("x")] });
        using var reader = await PwsReader.OpenAsync(ms);

        Assert.True(reader.FileSystem.FileExists("sites/x/index.html"));
        Assert.False(reader.FileSystem.FileExists("sites/x/missing.html"));
    }

    [Fact]
    public async Task FileSystem_OpenFile_Missing_Throws()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite()] });
        using var reader = await PwsReader.OpenAsync(ms);

        Assert.Throws<FileNotFoundException>(() =>
            reader.FileSystem.OpenFile("sites/docs/no-such-file.html"));
    }

    [Fact]
    public async Task FileSystem_OpenSiteFile_UnknownSite_Throws()
    {
        var ms = await PackAsync(new PwsPackOptions { Sites = [MakeSite()] });
        using var reader = await PwsReader.OpenAsync(ms);

        Assert.Throws<KeyNotFoundException>(() =>
            reader.FileSystem.OpenSiteFile("nonexistent", "index.html"));
    }

    // ── SourceDirectory packing ───────────────────────────────────────────────

    [Fact]
    public async Task Pack_FromDirectory_AllFilesIncluded()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(dir, "assets"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "index.html"),  "<html/>");
            await File.WriteAllTextAsync(Path.Combine(dir, "assets", "main.css"), "body{}");

            var ms = await PackAsync(new PwsPackOptions
            {
                Sites =
                [
                    new PwsSiteSource
                    {
                        Id              = "dir-site",
                        Title           = "Dir Site",
                        SourceDirectory = dir,
                    },
                ],
            });
            using var reader = await PwsReader.OpenAsync(ms);

            Assert.Equal(2, reader.FileSystem.ListFiles("dir-site").Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

