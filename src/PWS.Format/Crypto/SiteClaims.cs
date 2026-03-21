namespace PWS.Format.Crypto;

/// <summary>
/// Claims decoded (and optionally verified) from a site's JWT token.
/// </summary>
public sealed record SiteClaims
{
    /// <summary>Site identifier — matches <c>SiteManifest.Id</c>.</summary>
    public required string        SiteId       { get; init; }

    /// <summary>Human-readable title of the site.</summary>
    public required string        Title        { get; init; }

    /// <summary>Relative path to the site entry point (e.g. <c>index.html</c>).</summary>
    public required string        EntryPoint   { get; init; }

    /// <summary>
    /// Deterministic SHA-256 Merkle hash of all site files, prefixed with <c>"sha256:"</c>.
    /// Used to detect any tampering with the archive contents.
    /// </summary>
    public required string        ContentHash  { get; init; }

    /// <summary>Number of files included in the content hash.</summary>
    public int                    FileCount    { get; init; }

    /// <summary>Timestamp when the JWT was issued (packed).</summary>
    public DateTimeOffset         IssuedAt     { get; init; }

    /// <summary>
    /// <see langword="true"/> when the token signature was validated against a real key
    /// (HS256 or ES256). <see langword="false"/> for unsigned (<c>alg:none</c>) tokens.
    /// </summary>
    public bool                   IsVerified   { get; init; }
}

