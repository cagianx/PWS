using System.IO.Compression;
using System.Text.Json;
using PWS.Format.Crypto;
using PWS.Format.Filesystem;
using PWS.Format.Manifest;

namespace PWS.Format.Reading;

/// <summary>
/// Opens a .pws archive, verifies site JWT tokens, and exposes a virtual filesystem.
/// <para>
/// Dispose the reader to release the underlying ZIP stream.
/// Any <see cref="IPwsFileSystem"/> reference obtained from <see cref="FileSystem"/>
/// becomes invalid after disposal.
/// </para>
/// </summary>
public sealed class PwsReader : IDisposable
{
    private readonly ZipArchive    _zip;
    private readonly PwsFileSystem _fs;
    private          bool          _disposed;

    /// <summary>Deserialized manifest from <c>manifest.json</c>.</summary>
    public PwsManifest Manifest { get; }

    /// <summary>Verified (or parsed) claims for each site declared in the manifest.</summary>
    public IReadOnlyList<SiteClaims> Sites { get; }

    /// <summary>
    /// Virtual filesystem over the archive contents.
    /// Valid only while this reader is not disposed.
    /// </summary>
    public IPwsFileSystem FileSystem => _fs;

    // ── Construction ─────────────────────────────────────────────────────────

    private PwsReader(ZipArchive zip, PwsManifest manifest, IReadOnlyList<SiteClaims> sites)
    {
        _zip     = zip;
        Manifest = manifest;
        Sites    = sites;
        _fs      = new PwsFileSystem(zip, manifest.Sites);
    }

    // ── Open ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a .pws file from disk, verifies all site tokens and returns a reader.
    /// </summary>
    /// <param name="path">Full path to the <c>.pws</c> file.</param>
    /// <param name="options">Verification options; uses manifest-embedded key when <see langword="null"/>.</param>
    public static async Task<PwsReader> OpenAsync(
        string path,
        PwsOpenOptions? options = null,
        CancellationToken ct = default)
    {
        var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        try
        {
            return await OpenAsync(stream, options, ct);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Opens a .pws archive from an existing stream.
    /// The reader takes ownership of the stream and disposes it on <see cref="Dispose"/>.
    /// </summary>
    public static async Task<PwsReader> OpenAsync(
        Stream stream,
        PwsOpenOptions? options = null,
        CancellationToken ct = default)
    {
        var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        // ── Read manifest.json ────────────────────────────────────────────────
        var manifestEntry = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException(
                "The .pws archive does not contain a manifest.json file.");

        await using var raw = manifestEntry.Open();
        using  var buf = new MemoryStream();
        await raw.CopyToAsync(buf, ct);
        buf.Position = 0;

        var manifest = await JsonSerializer.DeserializeAsync<PwsManifest>(
                           buf,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                           ct)
                       ?? throw new InvalidDataException("manifest.json is invalid or empty.");

        // ── Resolve verification key ──────────────────────────────────────────
        var verKey = options?.VerificationKey;
        if (verKey is null && manifest.PublicKey is not null)
            verKey = PwsSigningKey.FromExport(manifest.PublicKey);

        // ── Verify / parse all site tokens ────────────────────────────────────
        var opt    = options ?? new PwsOpenOptions();
        var claims = new List<SiteClaims>(manifest.Sites.Count);

        foreach (var site in manifest.Sites)
            claims.Add(VerifySiteToken(site, verKey, opt));

        // ── Verify content hashes against actual ZIP entries ──────────────────
        // The JWT (signed or not) embeds pws:hash = Merkle hash of all site files.
        // We recompute the hash from the actual bytes in the archive to detect any
        // file-level tampering that would not be caught by the JWT signature alone.
        foreach (var (site, claim) in manifest.Sites.Zip(claims))
        {
            var actualHash = ComputeSiteHash(zip, site.Path);
            if (actualHash != claim.ContentHash)
                throw new InvalidDataException(
                    $"Site '{site.Id}': content hash mismatch. " +
                    "The archive files do not match the signed hash — " +
                    "the archive may have been tampered with.");
        }

        return new PwsReader(zip, manifest, claims);
    }

    // ── Convenience ──────────────────────────────────────────────────────────

    /// <summary>Returns the claims for a specific site, or <see langword="null"/> if not found.</summary>
    public SiteClaims? GetSite(string siteId) =>
        Sites.FirstOrDefault(s => s.SiteId == siteId);

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _zip.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static SiteClaims VerifySiteToken(
        SiteManifest   site,
        IPwsSigningKey? verKey,
        PwsOpenOptions  opt)
    {
        var effectiveKey = verKey ?? PwsSigningKey.None();

        if (opt.RequireSignedTokens && effectiveKey.Algorithm == JwtAlgorithm.None)
            throw new InvalidDataException(
                $"Site '{site.Id}': unsigned token rejected (RequireSignedTokens = true).");

        if (!effectiveKey.Verify(site.Token, out var raw) || raw is null)
            throw new InvalidDataException(
                $"Site '{site.Id}': JWT token verification failed. " +
                "The archive may have been tampered with.");

        return new SiteClaims
        {
            SiteId      = site.Id,
            Title       = GetStr(raw, "pws:title"),
            EntryPoint  = GetStr(raw, "pws:entry"),
            ContentHash = GetStr(raw, "pws:hash"),
            FileCount   = raw.TryGetValue("pws:files", out var fc)
                              ? Convert.ToInt32(fc) : 0,
            IssuedAt    = raw.TryGetValue("iat", out var iat)
                              ? DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(iat))
                              : DateTimeOffset.UtcNow,
            IsVerified  = effectiveKey.Algorithm != JwtAlgorithm.None,
        };
    }

    private static string GetStr(IReadOnlyDictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) ? v.ToString() ?? string.Empty : string.Empty;

    /// <summary>
    /// Re-computes the Merkle hash of all files stored under <paramref name="prefix"/>
    /// in the ZIP archive and returns the result in the same <c>"sha256:…"</c> format
    /// used by <see cref="MerkleHasher"/>.
    /// </summary>
    private static string ComputeSiteHash(ZipArchive zip, string prefix)
    {
        var files = new List<(string path, byte[] content)>();

        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var rel = entry.FullName[prefix.Length..];
            if (string.IsNullOrEmpty(rel)) continue; // skip directory sentinel entries

            using var ms  = new MemoryStream();
            using var src = entry.Open();
            src.CopyTo(ms);
            files.Add((rel, ms.ToArray()));
        }

        return MerkleHasher.Compute(files);
    }
}

