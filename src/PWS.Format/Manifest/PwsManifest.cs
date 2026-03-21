using System.Text.Json.Serialization;

namespace PWS.Format.Manifest;

/// <summary>Root object of the <c>manifest.json</c> inside a .pws archive.</summary>
public sealed class PwsManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1";

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Public key embedded by the packer so readers can verify site tokens without
    /// a separate key exchange. Format: <c>"ALG:base64"</c> — e.g. <c>"ES256:MFkwE…"</c>.
    /// <see langword="null"/> for unsigned (development) packages.
    /// </summary>
    [JsonPropertyName("publicKey")]
    public string? PublicKey { get; init; }

    /// <summary>One entry per site contained in this archive.</summary>
    [JsonPropertyName("sites")]
    public List<SiteManifest> Sites { get; init; } = [];
}

