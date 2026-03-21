using System.Text;

namespace PWS.Format.Crypto;

/// <summary>Factory for creating signing / verification keys used in .pws archives.</summary>
public static class PwsSigningKey
{
    /// <summary>
    /// Returns an unsigned key — tokens carry <c>alg:none</c>.
    /// Any reader can open the package without a key; no integrity guarantee.
    /// Suitable only for development or internal pipelines.
    /// </summary>
    public static IPwsSigningKey None() => new NoneKey();

    /// <summary>Creates an HMAC-SHA256 key from a UTF-8 secret string.</summary>
    public static IPwsSigningKey FromHmac(string secret) =>
        FromHmac(Encoding.UTF8.GetBytes(secret));

    /// <summary>Creates an HMAC-SHA256 key from raw key bytes (minimum 32 bytes recommended).</summary>
    public static IPwsSigningKey FromHmac(byte[] key) => new HmacKey(key);

    /// <summary>
    /// Generates a fresh ECDSA P-256 key pair.
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>Full</c> — private + public key; pass to <see cref="Packing.PwsPackOptions.SigningKey"/>.
    ///   </description></item>
    ///   <item><description>
    ///     <c>PublicOnly</c> — public key only; for readers or secondary verifiers.
    ///   </description></item>
    ///   <item><description>
    ///     <c>PublicKeyExport</c> — <c>"ES256:base64"</c> string; embed in
    ///     <see cref="Manifest.PwsManifest.PublicKey"/> so any reader can verify.
    ///   </description></item>
    /// </list>
    /// </summary>
    public static (IPwsSigningKey Full, IPwsSigningKey PublicOnly, string PublicKeyExport)
        GenerateEcDsa() => EcDsaKey.Generate();

    /// <summary>
    /// Reconstructs a public-only verification key from the <c>"ALG:base64"</c> string
    /// stored in <see cref="Manifest.PwsManifest.PublicKey"/>.
    /// Currently supports ES256.
    /// </summary>
    public static IPwsSigningKey FromExport(string exported) => EcDsaKey.FromExport(exported);
}

