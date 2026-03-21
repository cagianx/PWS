using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PWS.Format.Crypto;

/// <summary>
/// BCL-only JWT encode / decode / verify.
/// Implements compact serialisation (RFC 7519) with HS256 and ES256 — zero external packages.
/// </summary>
internal static class JwtHelper
{
    // ── Base64Url ────────────────────────────────────────────────────────────

    public static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }

    // ── Token creation ───────────────────────────────────────────────────────

    /// <param name="claims">Payload claims (string, long, int, bool values).</param>
    /// <param name="algorithm">Algorithm for the <c>alg</c> header field.</param>
    /// <param name="signer">
    /// Delegate that takes the UTF-8 signing input (<c>header.payload</c>) and returns the
    /// raw signature bytes. Pass <see langword="null"/> for unsigned tokens.
    /// </param>
    public static string Create(
        IReadOnlyDictionary<string, object> claims,
        JwtAlgorithm algorithm,
        Func<byte[], byte[]>? signer)
    {
        var algName = AlgName(algorithm);

        // Header
        var headerJson  = JsonSerializer.Serialize(new { alg = algName, typ = "JWT" });
        var payloadJson = JsonSerializer.Serialize(claims);

        var h = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var p = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var signingInput = $"{h}.{p}";

        var sig = signer is not null
            ? Base64UrlEncode(signer(Encoding.UTF8.GetBytes(signingInput)))
            : string.Empty;

        return $"{signingInput}.{sig}";
    }

    // ── Token parsing & verification ─────────────────────────────────────────

    /// <summary>
    /// Parses a compact JWT and optionally verifies its signature.
    /// </summary>
    /// <param name="token">Compact JWT string.</param>
    /// <param name="expectedAlgorithm">
    /// Asserts the <c>alg</c> header matches (prevents algorithm-switching attacks).
    /// </param>
    /// <param name="verifier">
    /// Delegate that takes (signingInput, signature) and returns <see langword="true"/> if valid.
    /// Pass <see langword="null"/> to skip signature verification (unsigned tokens).
    /// </param>
    /// <returns>Decoded payload dictionary, or <see langword="null"/> on failure.</returns>
    public static IReadOnlyDictionary<string, object>? TryParse(
        string token,
        JwtAlgorithm expectedAlgorithm,
        Func<byte[], byte[], bool>? verifier)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            // Decode and check algorithm in header
            var headerBytes = Base64UrlDecode(parts[0]);
            var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerBytes);
            if (header is null) return null;

            var alg = header.TryGetValue("alg", out var algEl)
                ? algEl.GetString() ?? "none"
                : "none";

            if (!string.Equals(alg, AlgName(expectedAlgorithm), StringComparison.OrdinalIgnoreCase))
                return null;

            // Verify signature when a verifier is provided
            if (verifier is not null)
            {
                var signingInput  = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
                var signatureBytes = Base64UrlDecode(parts[2]);
                if (!verifier(signingInput, signatureBytes)) return null;
            }

            // Decode payload and flatten JsonElement → primitive objects
            var payloadBytes = Base64UrlDecode(parts[1]);
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadBytes);
            if (raw is null) return null;

            var result = new Dictionary<string, object>(raw.Count, StringComparer.Ordinal);
            foreach (var (key, val) in raw)
            {
                result[key] = val.ValueKind switch
                {
                    JsonValueKind.String => (object)(val.GetString() ?? string.Empty),
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    JsonValueKind.Number => val.TryGetInt64(out var l) ? l : val.GetDouble(),
                    _                   => val.ToString(),
                };
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string AlgName(JwtAlgorithm alg) => alg switch
    {
        JwtAlgorithm.HS256 => "HS256",
        JwtAlgorithm.ES256 => "ES256",
        _                  => "none",
    };
}

