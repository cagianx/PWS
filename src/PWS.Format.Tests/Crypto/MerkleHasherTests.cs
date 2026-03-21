using System.Text;
using PWS.Format.Crypto;
using Xunit;

namespace PWS.Format.Tests.Crypto;

public sealed class MerkleHasherTests
{
    [Fact]
    public void Compute_EmptySet_ReturnsValidHash()
    {
        var hash = MerkleHasher.Compute([]);
        Assert.StartsWith("sha256:", hash);
        Assert.Equal(7 + 64, hash.Length); // "sha256:" + 64 hex chars
    }

    [Fact]
    public void Compute_SameInput_SameOutput()
    {
        var files = TestFiles();
        Assert.Equal(MerkleHasher.Compute(files), MerkleHasher.Compute(files));
    }

    [Fact]
    public void Compute_OrderIndependent_SameHash()
    {
        // Files in different order should produce the same hash (sort by path internally)
        var files1 = new[]
        {
            ("a/index.html", "content-a"u8.ToArray()),
            ("b/index.html", "content-b"u8.ToArray()),
        };
        var files2 = new[]
        {
            ("b/index.html", "content-b"u8.ToArray()),
            ("a/index.html", "content-a"u8.ToArray()),
        };

        Assert.Equal(MerkleHasher.Compute(files1), MerkleHasher.Compute(files2));
    }

    [Fact]
    public void Compute_DifferentContent_DifferentHash()
    {
        var v1 = new[] { ("index.html", "version 1"u8.ToArray()) };
        var v2 = new[] { ("index.html", "version 2"u8.ToArray()) };
        Assert.NotEqual(MerkleHasher.Compute(v1), MerkleHasher.Compute(v2));
    }

    [Fact]
    public void Compute_DifferentPath_DifferentHash()
    {
        // Same content, different path → different hash (prevents path substitution)
        var f1 = new[] { ("page.html",  "hello"u8.ToArray()) };
        var f2 = new[] { ("other.html", "hello"u8.ToArray()) };
        Assert.NotEqual(MerkleHasher.Compute(f1), MerkleHasher.Compute(f2));
    }

    [Fact]
    public void Compute_PathPrefixAttack_DifferentHash()
    {
        // "ab" + "cd" must not equal "a" + "bcd" (length-prefix prevents this)
        var f1 = new[] { ("ab", "X"u8.ToArray()), ("cd", "Y"u8.ToArray()) };
        var f2 = new[] { ("a",  "X"u8.ToArray()), ("bcd","Y"u8.ToArray()) };
        Assert.NotEqual(MerkleHasher.Compute(f1), MerkleHasher.Compute(f2));
    }

    [Fact]
    public void Compute_SingleFile_DeterministicHash()
    {
        var content = Encoding.UTF8.GetBytes("<html>hello</html>");
        var hash1   = MerkleHasher.Compute([("index.html", content)]);
        var hash2   = MerkleHasher.Compute([("index.html", content)]);
        Assert.Equal(hash1, hash2);
        Assert.StartsWith("sha256:", hash1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IEnumerable<(string, byte[])> TestFiles() =>
    [
        ("index.html",     Encoding.UTF8.GetBytes("<html>test</html>")),
        ("assets/main.css", Encoding.UTF8.GetBytes("body { color: red; }")),
        ("assets/app.js",  Encoding.UTF8.GetBytes("console.log('hi');")),
    ];
}

