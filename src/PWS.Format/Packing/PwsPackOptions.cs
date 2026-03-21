using PWS.Format.Crypto;

namespace PWS.Format.Packing;

/// <summary>Options controlling how <see cref="PwsPacker"/> assembles a .pws archive.</summary>
public sealed class PwsPackOptions
{
    /// <summary>
    /// Sites to include in the archive. At least one site is required.
    /// Each site maps to a <c>sites/{id}/</c> subtree inside the ZIP.
    /// </summary>
    public required IReadOnlyList<PwsSiteSource> Sites { get; init; }

    /// <summary>
    /// Key used to sign site JWT tokens.
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="PwsSigningKey.None()"/> — unsigned, for development. (default)
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="PwsSigningKey.FromHmac(string)"/> — HMAC-SHA256 symmetric key.
    ///   </description></item>
    ///   <item><description>
    ///     <c>PwsSigningKey.GenerateEcDsa().Full</c> — ECDSA P-256 private key for production.
    ///   </description></item>
    /// </list>
    /// </summary>
    public IPwsSigningKey SigningKey { get; init; } = PwsSigningKey.None();
}

