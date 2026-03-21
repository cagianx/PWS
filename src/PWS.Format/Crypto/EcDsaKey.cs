using System.Security.Cryptography;

namespace PWS.Format.Crypto;

/// <summary>
/// ECDSA P-256 / SHA-256 asymmetric key (JWT ES256).
/// <para>
/// Can be full (private + public, for signing) or public-only (for verification).
/// Readers reconstruct a public-only instance from <see cref="PwsManifest.PublicKey"/>.
/// </para>
/// </summary>
internal sealed class EcDsaKey : IPwsSigningKey
{
    private readonly ECDsa  _ecdsa;
    private readonly bool   _publicOnly;
    private readonly string _export;   // "ES256:base64publickey"

    private EcDsaKey(ECDsa ecdsa, bool publicOnly, string export)
    {
        _ecdsa      = ecdsa;
        _publicOnly = publicOnly;
        _export     = export;
    }

    public JwtAlgorithm Algorithm => JwtAlgorithm.ES256;

    public string Sign(IReadOnlyDictionary<string, object> claims)
    {
        if (_publicOnly)
            throw new InvalidOperationException(
                "Cannot sign with a public-only ECDSA key. " +
                "Use the full key returned by PwsSigningKey.GenerateEcDsa().");

        return JwtHelper.Create(claims, JwtAlgorithm.ES256, input =>
            // JWT ES256 requires IEEE P1363 (raw R||S), not DER
            _ecdsa.SignData(input, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    public bool Verify(string token, out IReadOnlyDictionary<string, object>? claims)
    {
        claims = JwtHelper.TryParse(token, JwtAlgorithm.ES256, (input, sig) =>
            _ecdsa.VerifyData(input, sig, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        return claims is not null;
    }

    public string? ExportPublicKey() => _export;

    // ── Factories ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a fresh P-256 key pair.
    /// <list type="bullet">
    ///   <item><c>Full</c> — holds private key; pass to <see cref="PwsPackOptions.SigningKey"/>.</item>
    ///   <item><c>PublicOnly</c> — holds only the public key; can be used to verify.</item>
    ///   <item><c>PublicKeyExport</c> — embed in <see cref="PwsManifest.PublicKey"/>.</item>
    /// </list>
    /// </summary>
    public static (IPwsSigningKey Full, IPwsSigningKey PublicOnly, string PublicKeyExport)
        Generate()
    {
        var ecdsa  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubDer = ecdsa.ExportSubjectPublicKeyInfo();
        var export = "ES256:" + Convert.ToBase64String(pubDer);

        var pubEcdsa = ECDsa.Create();
        pubEcdsa.ImportSubjectPublicKeyInfo(pubDer, out _);

        return (
            new EcDsaKey(ecdsa,    publicOnly: false, export),
            new EcDsaKey(pubEcdsa, publicOnly: true,  export),
            export
        );
    }

    /// <summary>
    /// Reconstructs a public-only verification key from the <c>"ES256:base64"</c>
    /// string stored in <see cref="PwsManifest.PublicKey"/>.
    /// </summary>
    public static IPwsSigningKey FromExport(string exported)
    {
        var sep = exported.IndexOf(':');
        if (sep < 0 || !exported[..sep].Equals("ES256", StringComparison.OrdinalIgnoreCase))
            throw new FormatException(
                $"Unsupported key export format: '{exported}'. Expected 'ES256:base64'.");

        var pubBytes = Convert.FromBase64String(exported[(sep + 1)..]);
        var ecdsa    = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(pubBytes, out _);
        return new EcDsaKey(ecdsa, publicOnly: true, exported);
    }
}

