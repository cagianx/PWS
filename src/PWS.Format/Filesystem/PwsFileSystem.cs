using System.IO.Compression;
using PWS.Format.Manifest;

namespace PWS.Format.Filesystem;

/// <summary>
/// <see cref="IPwsFileSystem"/> implementation backed by an open <see cref="ZipArchive"/>.
/// Does not own the archive — lifetime is managed by <see cref="Reading.PwsReader"/>.
/// </summary>
internal sealed class PwsFileSystem : IPwsFileSystem
{
    private readonly ZipArchive                           _zip;
    private readonly IReadOnlyDictionary<string, SiteManifest> _sites;

    internal PwsFileSystem(ZipArchive zip, IEnumerable<SiteManifest> sites)
    {
        _zip   = zip;
        _sites = sites.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    // ── IPwsFileSystem ───────────────────────────────────────────────────────

    public IReadOnlyList<PwsFileEntry> ListFiles(string? siteId = null)
    {
        var result = new List<PwsFileEntry>();

        foreach (var entry in _zip.Entries)
        {
            // Skip directory entries (end with '/')
            if (entry.FullName.EndsWith('/')) continue;

            if (!TryResolveSite(entry.FullName, out var site, out var relative)) continue;
            if (siteId is not null && site!.Id != siteId) continue;

            result.Add(new PwsFileEntry
            {
                ArchivePath  = entry.FullName,
                SiteId       = site!.Id,
                RelativePath = relative!,
                Size         = entry.Length,
                LastModified = entry.LastWriteTime,
            });
        }

        return result;
    }

    public Stream OpenFile(string archivePath)
    {
        var entry = _zip.GetEntry(archivePath)
            ?? throw new FileNotFoundException(
                $"File '{archivePath}' not found in the .pws archive.");
        return entry.Open();
    }

    public Stream OpenSiteFile(string siteId, string relativePath)
    {
        if (!_sites.TryGetValue(siteId, out var site))
            throw new KeyNotFoundException(
                $"Site '{siteId}' is not declared in the manifest.");

        var archivePath = site.Path.TrimEnd('/') + '/' + relativePath.TrimStart('/');
        return OpenFile(archivePath);
    }

    public bool FileExists(string archivePath) =>
        _zip.GetEntry(archivePath) is not null;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private bool TryResolveSite(
        string archivePath,
        out SiteManifest? site,
        out string? relative)
    {
        foreach (var s in _sites.Values)
        {
            if (archivePath.StartsWith(s.Path, StringComparison.Ordinal))
            {
                site     = s;
                relative = archivePath[s.Path.Length..];
                return true;
            }
        }

        site     = null;
        relative = null;
        return false;
    }
}

