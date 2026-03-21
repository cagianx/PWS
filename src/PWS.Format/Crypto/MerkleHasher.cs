using System.Security.Cryptography;
using System.Text;

namespace PWS.Format.Crypto;

/// <summary>
/// Computes a deterministic SHA-256 Merkle-style hash over all files of a site.
/// <para>
/// Algorithm:
/// <list type="number">
///   <item>Sort files by path (ordinal ascending).</item>
///   <item>For each file: SHA-256(content) → 32-byte file hash.</item>
///   <item>Concatenate: <c>len(path_utf8) || path_utf8 || file_hash</c> for every file.</item>
///   <item>Final hash: SHA-256 of the concatenated buffer.</item>
/// </list>
/// The result is the hex digest prefixed with <c>"sha256:"</c>.
/// </para>
/// </summary>
internal static class MerkleHasher
{
    public static string Compute(IEnumerable<(string path, byte[] content)> files)
    {
        using var combined = new MemoryStream();

        foreach (var (path, content) in files.OrderBy(f => f.path, StringComparer.Ordinal))
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var fileHash  = SHA256.HashData(content);

            // Length-prefix the path so paths can't be forged by concatenation
            combined.Write(BitConverter.GetBytes(pathBytes.Length));
            combined.Write(pathBytes);
            combined.Write(fileHash);
        }

        combined.Position = 0;
        return "sha256:" + Convert.ToHexString(SHA256.HashData(combined)).ToLowerInvariant();
    }
}

