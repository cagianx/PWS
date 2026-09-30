using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PWS.Format.Packing;
using PWS.Format.Reading;
using Xunit;

namespace PWS.Format.Tests;

/// <summary>
/// Inputs that must be rejected: malformed archives on open, invalid site
/// definitions on pack. No archive is opened or produced from them.
/// </summary>
public sealed class InvalidInputTests : IDisposable
{
    private readonly string _workDir =
        Path.Combine(Path.GetTempPath(), "pws-format-" + Guid.NewGuid().ToString("N"));

    public InvalidInputTests() => Directory.CreateDirectory(_workDir);

    public void Dispose() => Directory.Delete(_workDir, recursive: true);

    // ── Reader ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("")]
    public async Task Open_MalformedManifest_ThrowsInvalidData(string manifestJson)
    {
        var archive = ZipWithManifest(manifestJson);

        await Assert.ThrowsAsync<InvalidDataException>(() => PwsReader.OpenAsync(archive));
    }

    [Fact]
    public async Task Open_NullManifest_ThrowsInvalidData()
    {
        var archive = ZipWithManifest("null");

        await Assert.ThrowsAsync<InvalidDataException>(() => PwsReader.OpenAsync(archive));
    }

    [Fact]
    public async Task Open_SiteWithEmptyToken_ThrowsInvalidData()
    {
        var packed   = await PackValidAsync();
        var tampered = RewriteManifest(packed, manifest => manifest["sites"]![0]!["token"] = "");

        await Assert.ThrowsAsync<InvalidDataException>(() => PwsReader.OpenAsync(tampered));
    }

    // ── Packer ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pack_NoSites_ThrowsArgument_AndCreatesNoFile()
    {
        var output = Path.Combine(_workDir, "empty.pws");

        await Assert.ThrowsAsync<ArgumentException>(
            () => new PwsPacker().PackAsync(new PwsPackOptions { Sites = [] }, output));

        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("docs", "docs")]
    [InlineData("docs", "Docs")]
    public async Task Pack_DuplicateSiteIds_ThrowsArgument_AndCreatesNoFile(string first, string second)
    {
        var output = Path.Combine(_workDir, "dup.pws");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new PwsPacker().PackAsync(
            new PwsPackOptions { Sites = [MakeSite(first), MakeSite(second)] }, output));

        Assert.Contains(second, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("mio sito")]
    [InlineData("")]
    [InlineData("-docs")]
    [InlineData("docs-")]
    [InlineData("docs_1")]
    [InlineData("docs/api")]
    [InlineData("città")]
    public async Task Pack_SiteIdNotValidAsHost_ThrowsArgument_AndCreatesNoFile(string id)
    {
        var output = Path.Combine(_workDir, "invalid.pws");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new PwsPacker().PackAsync(
            new PwsPackOptions { Sites = [MakeSite(id)] }, output));

        Assert.Contains($"'{id}'", ex.Message);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("Docs-2")]
    [InlineData("a")]
    public async Task Pack_SiteIdValidAsHost_Succeeds(string id)
    {
        var output = Path.Combine(_workDir, "valid.pws");

        await new PwsPacker().PackAsync(new PwsPackOptions { Sites = [MakeSite(id)] }, output);

        using var reader = await PwsReader.OpenAsync(output);
        Assert.Equal(id, reader.Sites.Single().SiteId);
    }

    [Fact]
    public async Task Pack_SiteIdLongerThan63Chars_ThrowsArgument()
    {
        var id = new string('a', 64);

        await Assert.ThrowsAsync<ArgumentException>(() => new PwsPacker().PackAsync(
            new PwsPackOptions { Sites = [MakeSite(id)] }, new MemoryStream()));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PwsSiteSource MakeSite(string id)
    {
        var site = new PwsSiteSource { Id = id, Title = id };
        site.AddFile("index.html", () => new MemoryStream(Encoding.UTF8.GetBytes("<html>hello</html>")));
        return site;
    }

    private static async Task<MemoryStream> PackValidAsync()
    {
        var ms = new MemoryStream();
        await new PwsPacker().PackAsync(new PwsPackOptions { Sites = [MakeSite("docs")] }, ms);
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream ZipWithManifest(string manifestJson)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open(), Encoding.UTF8);
            writer.Write(manifestJson);
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>Copies the archive, rewriting <c>manifest.json</c> with <paramref name="edit"/>.</summary>
    private static MemoryStream RewriteManifest(MemoryStream packed, Action<JsonNode> edit)
    {
        var copy = new MemoryStream();
        packed.CopyTo(copy);
        copy.Position = 0;

        using (var zip = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("manifest.json")!;
            JsonNode manifest;
            using (var stream = entry.Open())
                manifest = JsonNode.Parse(stream)!;
            edit(manifest);

            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open(), Encoding.UTF8);
            writer.Write(manifest.ToJsonString());
        }
        copy.Position = 0;
        return copy;
    }
}
