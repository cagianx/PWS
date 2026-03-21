using System.Text.Json.Serialization;

namespace PWS.Format.Manifest;

/// <summary>Manifest entry for a single site within a .pws archive.</summary>
public sealed class SiteManifest
{
    /// <summary>Unique identifier within the archive (used as ZIP path prefix and URI host).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// ZIP path prefix for all files belonging to this site.
    /// Always <c>sites/{id}/</c>.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// JWT (JSON Web Token) containing site metadata and the Merkle content hash.
    /// Signed with the packer's private key; verified via <see cref="PwsManifest.PublicKey"/>.
    /// Algorithm is encoded in the token header (<c>alg</c> claim).
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;
}

