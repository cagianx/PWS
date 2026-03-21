using PWS.Format.Crypto;
using PWS.Format.Packing;

// Usage:
//   dotnet run --project src/CreateTestPws/CreateTestPws.csproj -- [inputDir] [outputPath]
// Defaults:
//   inputDir   = <repo>/docs/build
//   outputPath = <repo>/artifacts/docs.pws

var repoRoot = FindRepoRoot();
var docsDir = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "docs", "build");

if (!Directory.Exists(docsDir))
{
    Console.WriteLine($"❌ Directory not found: {docsDir}");
    return 1;
}

var outputPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? Path.GetFullPath(args[1], Directory.GetCurrentDirectory())
    : Path.Combine(repoRoot, "artifacts", "docs.pws");

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var packer = new PwsPacker();
var (fullKey, _, publicKeyExport) = PwsSigningKey.GenerateEcDsa();

Console.WriteLine($"📦 Packing {docsDir}...");
await packer.PackAsync(
    new PwsPackOptions
    {
        Sites =
        [
            new PwsSiteSource
            {
                Id              = "docs",
                Title           = "PWS Browser Documentation",
                EntryPoint      = "index.html",
                SourceDirectory = docsDir,
            },
        ],
        SigningKey = fullKey,
    },
    outputPath);

var fileSize = new FileInfo(outputPath).Length;
Console.WriteLine($"✓ Created {outputPath}");
Console.WriteLine($"  Size: {fileSize / 1024.0:F1} KB");
Console.WriteLine($"  PublicKey: {publicKeyExport[..50]}...");
return 0;

static string FindRepoRoot()
{
    var current = Directory.GetCurrentDirectory();
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current, "PWS.slnx")))
            return current;
        current = Directory.GetParent(current)?.FullName;
    }
    throw new Exception("Cannot find repo root");
}

