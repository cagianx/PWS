using System.Text;
using PWS.Format.Crypto;
using Xunit;

namespace PWS.Format.Tests.Crypto;

public sealed class JwtHelperTests
{
    // ── Base64Url ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0xFD })]           // produces + and / in standard Base64
    [InlineData(new byte[] { 0xFB, 0xFF, 0xFE, 0x00, 0x01 })]
    public void Base64UrlEncode_Decode_RoundTrip(byte[] data)
    {
        var encoded = JwtHelper.Base64UrlEncode(data);
        var decoded = JwtHelper.Base64UrlDecode(encoded);
        Assert.Equal(data, decoded);
    }

    [Theory]
    [InlineData(new byte[] { 0x00 })]           // 1 byte  → padding "=="
    [InlineData(new byte[] { 0x00, 0x00 })]     // 2 bytes → padding "="
    [InlineData(new byte[] { 0x00, 0x00, 0x00 })] // 3 bytes → no padding
    public void Base64UrlEncode_ContainsNoPadding(byte[] data)
    {
        var encoded = JwtHelper.Base64UrlEncode(data);
        Assert.DoesNotContain('=', encoded);
    }

    [Fact]
    public void Base64UrlEncode_UsesUrlSafeChars()
    {
        // Exhaustively check that no standard-Base64-only chars appear
        var encoded = JwtHelper.Base64UrlEncode(new byte[] { 0xFB, 0xFF, 0xFE });
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
    }

    // ── Token creation — None ────────────────────────────────────────────────

    [Fact]
    public void Create_None_ProducesThreePartToken()
    {
        var token = JwtHelper.Create(Claims("sub", "test"), JwtAlgorithm.None, signer: null);
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void Create_None_HasEmptySignaturePart()
    {
        var token = JwtHelper.Create(Claims("sub", "test"), JwtAlgorithm.None, signer: null);
        Assert.Equal(string.Empty, token.Split('.')[2]);
    }

    // ── TryParse — None ──────────────────────────────────────────────────────

    [Fact]
    public void TryParse_None_ReturnsClaims()
    {
        var original = new Dictionary<string, object>
        {
            ["sub"]   = "site1",
            ["count"] = 42L,
            ["flag"]  = true,
        };
        var token  = JwtHelper.Create(original, JwtAlgorithm.None, null);
        var result = JwtHelper.TryParse(token, JwtAlgorithm.None, null);

        Assert.NotNull(result);
        Assert.Equal("site1", result["sub"]);
        // JSON deserializza 42L come double se il numero è piccolo
        Assert.Equal(42.0, (double)result["count"]);
        Assert.Equal(true, result["flag"]);
    }

    // ── Algorithm mismatch ───────────────────────────────────────────────────

    [Fact]
    public void TryParse_AlgorithmMismatch_ReturnsNull()
    {
        var token = JwtHelper.Create(Claims("sub", "test"), JwtAlgorithm.None, null);
        Assert.Null(JwtHelper.TryParse(token, JwtAlgorithm.HS256, null));
    }

    [Fact]
    public void TryParse_InvalidTokenFormat_ReturnsNull()
    {
        Assert.Null(JwtHelper.TryParse("notajwt",   JwtAlgorithm.None, null));
        Assert.Null(JwtHelper.TryParse("only.two",  JwtAlgorithm.None, null));
        Assert.Null(JwtHelper.TryParse("",          JwtAlgorithm.None, null));
    }

    [Fact]
    public void TryParse_VerifierReturningFalse_ReturnsNull()
    {
        var token = JwtHelper.Create(Claims("sub", "test"), JwtAlgorithm.HS256,
            _ => new byte[32]);   // dummy 32-byte signature

        // Verifier that always rejects
        var result = JwtHelper.TryParse(token, JwtAlgorithm.HS256,
            (_, _) => false);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_VerifierReturningTrue_ReturnsClaims()
    {
        var token = JwtHelper.Create(Claims("sub", "test"), JwtAlgorithm.HS256,
            _ => new byte[32]);

        var result = JwtHelper.TryParse(token, JwtAlgorithm.HS256,
            (_, _) => true);
        Assert.NotNull(result);
        Assert.Equal("test", result["sub"]);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object> Claims(string key, object value) =>
        new Dictionary<string, object> { [key] = value };
}

