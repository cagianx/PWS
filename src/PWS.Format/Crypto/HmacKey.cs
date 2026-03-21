using System.Security.Cryptography;

namespace PWS.Format.Crypto;

/// <summary>HMAC-SHA256 symmetric key. Same key for signing and verification.</summary>
internal sealed class HmacKey : IPwsSigningKey
{
    private readonly byte[] _key;

    internal HmacKey(byte[] key) => _key = key;

    public JwtAlgorithm Algorithm => JwtAlgorithm.HS256;

    public string Sign(IReadOnlyDictionary<string, object> claims) =>
        JwtHelper.Create(claims, JwtAlgorithm.HS256, input =>
        {
            using var hmac = new HMACSHA256(_key);
            return hmac.ComputeHash(input);
        });

    public bool Verify(string token, out IReadOnlyDictionary<string, object>? claims)
    {
        claims = JwtHelper.TryParse(token, JwtAlgorithm.HS256, (input, sig) =>
        {
            using var hmac = new HMACSHA256(_key);
            // Constant-time comparison prevents timing attacks
            return CryptographicOperations.FixedTimeEquals(hmac.ComputeHash(input), sig);
        });
        return claims is not null;
    }

    /// <summary>HMAC is symmetric — there is no separate public key to export.</summary>
    public string? ExportPublicKey() => null;
}

