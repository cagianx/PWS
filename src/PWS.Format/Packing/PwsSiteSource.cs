namespace PWS.Format.Packing;

/// <summary>Describes a single site to include in a .pws archive.</summary>
public sealed class PwsSiteSource
{
    /// <summary>
    /// Unique site identifier (alphanumeric + hyphen).
    /// Used as the ZIP path prefix (<c>sites/{id}/</c>) and as the JWT <c>sub</c> claim.
    /// </summary>
    public required string Id         { get; init; }

    /// <summary>Human-readable title embedded in the site's JWT claims.</summary>
    public required string Title      { get; init; }

    /// <summary>Entry-point path relative to the site root. Default: <c>index.html</c>.</summary>
    public string          EntryPoint { get; init; } = "index.html";

    // ── Source: directory OR explicit file list ──────────────────────────────

    /// <summary>
    /// When set, all files under this directory are packed recursively.
    /// Mutually exclusive with <see cref="AddFile"/>.
    /// </summary>
    public string? SourceDirectory { get; init; }

    private readonly List<(string RelativePath, Func<Stream> Open)> _files = [];

    /// <summary>
    /// Adds a single file explicitly.
    /// Used when <see cref="SourceDirectory"/> is not set.
    /// </summary>
    /// <param name="relativePath">Path inside the site (forward slashes, no leading slash).</param>
    /// <param name="openStream">Factory that opens the file content stream.</param>
    public void AddFile(string relativePath, Func<Stream> openStream) =>
        _files.Add((relativePath.Replace('\\', '/'), openStream));

    // ── Internal ─────────────────────────────────────────────────────────────

    internal IEnumerable<(string RelativePath, Func<Stream> Open)> GetFiles()
    {
        if (SourceDirectory is { } dir)
        {
            foreach (var file in Directory
                         .EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                yield return (rel, () => File.OpenRead(file));
            }
        }
        else
        {
            foreach (var f in _files)
                yield return f;
        }
    }
}

