namespace PWS.Format.Filesystem;

/// <summary>Metadata for a single file entry within a .pws archive.</summary>
public sealed class PwsFileEntry
{
    /// <summary>Full path inside the ZIP (e.g. <c>sites/docs/index.html</c>).</summary>
    public required string        ArchivePath  { get; init; }

    /// <summary>Site identifier this file belongs to.</summary>
    public required string        SiteId       { get; init; }

    /// <summary>Path relative to the site root (e.g. <c>index.html</c>, <c>assets/main.css</c>).</summary>
    public required string        RelativePath { get; init; }

    /// <summary>Uncompressed file size in bytes.</summary>
    public long                   Size         { get; init; }

    public DateTimeOffset         LastModified { get; init; }
}

