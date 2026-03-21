namespace PWS.Format.Crypto;

/// <summary>Unsigned key — tokens have <c>alg:none</c>. For development / internal packages.</summary>
internal sealed class NoneKey : IPwsSigningKey
{
    public JwtAlgorithm Algorithm => JwtAlgorithm.None;

    public string Sign(IReadOnlyDictionary<string, object> claims) =>
        JwtHelper.Create(claims, JwtAlgorithm.None, signer: null);

    public bool Verify(string token, out IReadOnlyDictionary<string, object>? claims)
    {
        claims = JwtHelper.TryParse(token, JwtAlgorithm.None, verifier: null);
        return claims is not null;
    }

    public string? ExportPublicKey() => null;
}

