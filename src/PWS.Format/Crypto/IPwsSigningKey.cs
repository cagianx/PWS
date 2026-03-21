namespace PWS.Format.Crypto;

/// <summary>
/// Abstracts JWT signing (packer) and verification (reader) for .pws site tokens.
/// </summary>
public interface IPwsSigningKey
{
    /// <summary>Cryptographic algorithm used by this key.</summary>
    JwtAlgorithm Algorithm { get; }

    /// <summary>
    /// Creates a signed (or unsigned) JWT token encoding the given claims.
    /// </summary>
    /// <param name="claims">Claims to embed in the token payload.</param>
    /// <returns>Compact JWT string (<c>header.payload.signature</c>).</returns>
    string Sign(IReadOnlyDictionary<string, object> claims);

    /// <summary>
    /// Verifies the token signature (if applicable) and decodes its payload.
    /// </summary>
    /// <param name="token">Compact JWT string to verify.</param>
    /// <param name="claims">Decoded payload on success; <see langword="null"/> on failure.</param>
    /// <returns><see langword="true"/> if the token is valid and claims were decoded.</returns>
    bool Verify(string token, out IReadOnlyDictionary<string, object>? claims);

    /// <summary>
    /// Exports the public key for embedding in <c>manifest.json</c> (<c>"ALG:base64"</c> format).
    /// Returns <see langword="null"/> for symmetric (HS256) and unsigned (None) keys —
    /// readers cannot reconstruct a verifier from those.
    /// </summary>
    string? ExportPublicKey();
}

