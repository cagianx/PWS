namespace PWS.Format.Crypto;

/// <summary>Algorithm used to sign (and verify) site JWT tokens in a .pws manifest.</summary>
public enum JwtAlgorithm
{
    /// <summary>No signature — for development or internal-use packages (token is still parseable).</summary>
    None,

    /// <summary>HMAC-SHA256 symmetric shared secret.</summary>
    HS256,

    /// <summary>ECDSA P-256 / SHA-256 asymmetric signature. Public key embedded in manifest.</summary>
    ES256,
}

