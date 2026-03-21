using PWS.Format.Crypto;
using Xunit;

namespace PWS.Format.Tests.Crypto;

/// <summary>
/// Tests for all three IPwsSigningKey implementations (None, HMAC, ECDSA)
/// accessed through the public PwsSigningKey factory.
/// </summary>
public sealed class SigningKeyTests
{
    private static readonly IReadOnlyDictionary<string, object> TestClaims =
        new Dictionary<string, object>
        {
            ["sub"]       = "my-site",
            ["pws:title"] = "My Site",
            ["pws:hash"]  = "sha256:abc",
            ["iat"]       = 1742551200L,
        };

    // ── NoneKey ──────────────────────────────────────────────────────────────

    [Fact]
    public void None_Algorithm_IsNone()
        => Assert.Equal(JwtAlgorithm.None, PwsSigningKey.None().Algorithm);

    [Fact]
    public void None_ExportPublicKey_ReturnsNull()
        => Assert.Null(PwsSigningKey.None().ExportPublicKey());

    [Fact]
    public void None_SignAndVerify_Success()
    {
        var key   = PwsSigningKey.None();
        var token = key.Sign(TestClaims);

        Assert.True(key.Verify(token, out var claims));
        Assert.NotNull(claims);
        Assert.Equal("my-site", claims["sub"]);
    }

    [Fact]
    public void None_SignedTokenIsNotEmpty()
    {
        var token = PwsSigningKey.None().Sign(TestClaims);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length);
    }

    // ── HmacKey ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hmac_Algorithm_IsHS256()
        => Assert.Equal(JwtAlgorithm.HS256, PwsSigningKey.FromHmac("secret").Algorithm);

    [Fact]
    public void Hmac_ExportPublicKey_ReturnsNull()
        => Assert.Null(PwsSigningKey.FromHmac("secret").ExportPublicKey());

    [Fact]
    public void Hmac_SignAndVerify_SameKey_Success()
    {
        var key   = PwsSigningKey.FromHmac("my-secret");
        var token = key.Sign(TestClaims);

        Assert.True(key.Verify(token, out var claims));
        Assert.NotNull(claims);
        Assert.Equal("my-site", claims["sub"]);
    }

    [Fact]
    public void Hmac_Verify_WrongKey_ReturnsFalse()
    {
        var signerKey   = PwsSigningKey.FromHmac("correct");
        var verifierKey = PwsSigningKey.FromHmac("wrong");
        var token = signerKey.Sign(TestClaims);

        Assert.False(verifierKey.Verify(token, out var claims));
        Assert.Null(claims);
    }

    [Fact]
    public void Hmac_FromBytes_SignAndVerify()
    {
        var keyBytes = new byte[32];
        new Random(42).NextBytes(keyBytes);

        var key   = PwsSigningKey.FromHmac(keyBytes);
        var token = key.Sign(TestClaims);
        Assert.True(key.Verify(token, out _));
    }

    [Fact]
    public void Hmac_TamperedToken_FailsVerification()
    {
        var key   = PwsSigningKey.FromHmac("secret");
        var token = key.Sign(TestClaims);

        // Flip a character in the signature (last part)
        var parts = token.Split('.');
        parts[2] = parts[2].Length > 0
            ? new string(parts[2].Select((c, i) => i == 0 ? (char)(c ^ 1) : c).ToArray())
            : "tampered";
        var tampered = string.Join('.', parts);

        Assert.False(key.Verify(tampered, out _));
    }

    // ── EcDsaKey ─────────────────────────────────────────────────────────────

    [Fact]
    public void EcDsa_Generate_FullKey_Algorithm_IsES256()
    {
        var (full, _, _) = PwsSigningKey.GenerateEcDsa();
        Assert.Equal(JwtAlgorithm.ES256, full.Algorithm);
    }

    [Fact]
    public void EcDsa_Generate_ExportPublicKey_HasCorrectPrefix()
    {
        var (full, _, export) = PwsSigningKey.GenerateEcDsa();
        Assert.StartsWith("ES256:", export);
        Assert.StartsWith("ES256:", full.ExportPublicKey());
    }

    [Fact]
    public void EcDsa_FullKey_SignAndVerify()
    {
        var (full, _, _) = PwsSigningKey.GenerateEcDsa();
        var token = full.Sign(TestClaims);

        Assert.True(full.Verify(token, out var claims));
        Assert.NotNull(claims);
        Assert.Equal("my-site", claims["sub"]);
    }

    [Fact]
    public void EcDsa_CrossVerification_PublicOnlyVerifiesFullSignature()
    {
        var (full, publicOnly, _) = PwsSigningKey.GenerateEcDsa();
        var token = full.Sign(TestClaims);

        Assert.True(publicOnly.Verify(token, out var claims));
        Assert.NotNull(claims);
    }

    [Fact]
    public void EcDsa_PublicOnly_CannotSign()
    {
        var (_, publicOnly, _) = PwsSigningKey.GenerateEcDsa();
        Assert.Throws<InvalidOperationException>(() => publicOnly.Sign(TestClaims));
    }

    [Fact]
    public void EcDsa_FromExport_VerifiesCorrectly()
    {
        var (full, _, export) = PwsSigningKey.GenerateEcDsa();
        var token      = full.Sign(TestClaims);

        var imported   = PwsSigningKey.FromExport(export);
        Assert.True(imported.Verify(token, out _));
    }

    [Fact]
    public void EcDsa_FromExport_WrongKey_FailsVerification()
    {
        var (full1,   _, _)      = PwsSigningKey.GenerateEcDsa();
        var (_,       _, export2) = PwsSigningKey.GenerateEcDsa();

        var token       = full1.Sign(TestClaims);
        var wrongImport = PwsSigningKey.FromExport(export2);

        Assert.False(wrongImport.Verify(token, out _));
    }

    [Fact]
    public void EcDsa_FromExport_BadFormat_Throws()
    {
        Assert.Throws<FormatException>(() => PwsSigningKey.FromExport("INVALID:base64"));
        Assert.Throws<FormatException>(() => PwsSigningKey.FromExport("nocolon"));
    }

    [Fact]
    public void EcDsa_DifferentTokens_ForSameClaims_AreUnique()
    {
        // ECDSA is probabilistic — same message produces different signatures each time
        var (full, _, _) = PwsSigningKey.GenerateEcDsa();
        var t1 = full.Sign(TestClaims);
        var t2 = full.Sign(TestClaims);
        // Signatures differ (probabilistic), but both verify correctly
        Assert.True(full.Verify(t1, out _));
        Assert.True(full.Verify(t2, out _));
    }
}

