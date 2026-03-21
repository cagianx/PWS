using System.IO.Compression;
using System.Text.Json;
using PWS.Format.Crypto;
using PWS.Format.Manifest;

namespace PWS.Format.Packing;

/// <summary>
/// Assembles one or more sites into a .pws archive (ZIP + manifest.json + JWT tokens).
/// </summary>
public sealed class PwsPacker
{
    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Packs all sites in <paramref name="options"/> into a new file at <paramref name="outputPath"/>.</summary>
    public async Task PackAsync(
        PwsPackOptions options,
        string         outputPath,
        CancellationToken ct = default)
    {
        await using var fs = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, useAsync: true);
        await PackAsync(options, fs, ct);
    }

    /// <summary>Packs into an existing writable <paramref name="output"/> stream.</summary>
    public async Task PackAsync(
        PwsPackOptions options,
        Stream         output,
        CancellationToken ct = default)
    {
        if (options.Sites.Count == 0)
            throw new ArgumentException("At least one site must be specified.", nameof(options));

        using var zip     = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var siteEntries   = new List<SiteManifest>(options.Sites.Count);

        foreach (var site in options.Sites)
        {
            ct.ThrowIfCancellationRequested();
            var entry = await PackSiteAsync(zip, site, options.SigningKey, ct);
            siteEntries.Add(entry);
        }

        // Write manifest.json last (after all site files are in the ZIP)
        var manifest = new PwsManifest
        {
            Created   = DateTimeOffset.UtcNow,
            PublicKey = options.SigningKey.ExportPublicKey(),
            Sites     = siteEntries,
        };

        var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream, manifest,
            new JsonSerializerOptions { WriteIndented = true },
            ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static async Task<SiteManifest> PackSiteAsync(
        ZipArchive     zip,
        PwsSiteSource  site,
        IPwsSigningKey key,
        CancellationToken ct)
    {
        var prefix  = $"sites/{site.Id}/";
        var buffer  = new List<(string relative, byte[] content)>();

        // Read all files into memory — needed to compute the Merkle hash before writing
        foreach (var (relPath, openStream) in site.GetFiles())
        {
            ct.ThrowIfCancellationRequested();
            await using var src = openStream();
            using  var mem      = new MemoryStream();
            await src.CopyToAsync(mem, ct);
            buffer.Add((relPath, mem.ToArray()));
        }

        // Compute the deterministic Merkle content hash
        var contentHash = MerkleHasher.Compute(buffer);

        // Build and sign the site JWT
        var claims = new Dictionary<string, object>
        {
            ["sub"]       = site.Id,
            ["pws:title"] = site.Title,
            ["pws:entry"] = site.EntryPoint,
            ["pws:hash"]  = contentHash,
            ["pws:files"] = (long)buffer.Count,
            ["iat"]       = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        var token = key.Sign(claims);

        // Write site files into the ZIP
        foreach (var (relPath, content) in buffer)
        {
            ct.ThrowIfCancellationRequested();
            var zipEntry = zip.CreateEntry(prefix + relPath, CompressionLevel.Optimal);
            await using var es = zipEntry.Open();
            await es.WriteAsync(content, ct);
        }

        return new SiteManifest { Id = site.Id, Path = prefix, Token = token };
    }
}

