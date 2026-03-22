using Microsoft.Extensions.Logging;
using PWS.Format.Crypto;

namespace PWS.Format.Reading;

/// <summary>Options for <see cref="PwsReader.OpenAsync"/>.</summary>
public sealed class PwsOpenOptions
{
    /// <summary>
    /// Override the verification key derived from <see cref="Manifest.PwsManifest.PublicKey"/>.
    /// When <see langword="null"/> the key embedded in the manifest is used (or no verification
    /// for unsigned packages).
    /// </summary>
    public IPwsSigningKey? VerificationKey { get; init; }

    /// <summary>
    /// When <see langword="true"/>, archives with unsigned (<c>alg:none</c>) site tokens are
    /// rejected with an <see cref="InvalidDataException"/>.
    /// Default: <see langword="false"/> — unsigned packages are accepted.
    /// </summary>
    public bool RequireSignedTokens { get; init; }

    /// <summary>
    /// Optional logger used by <see cref="PwsReader"/> to record warnings and errors
    /// (e.g. JWT verification failures, content hash mismatches) before throwing.
    /// When <see langword="null"/> no logging is performed.
    /// </summary>
    public ILogger? Logger { get; init; }
}

