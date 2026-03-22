using System.IO.Compression;
using Microsoft.Extensions.Logging;
using PWS.Format.Crypto;
using PWS.Format.Packing;

namespace PWS.Tool.Commands;

/// <summary>
/// Implementazione del verbo <c>pack</c>:
/// impacchetta una directory sorgente o un file .zip in un archivio .pws.
/// </summary>
public static class PackCommand
{
    /// <summary>Esegue il packing e restituisce l'exit code (0 = OK, 1 = errore).</summary>
    public static async Task<int> RunAsync(PackOptions opts, ILogger logger)
    {
        // ── 1. Risolvi il percorso sorgente ───────────────────────────────────
        var sourcePath = Path.GetFullPath(opts.Source, Directory.GetCurrentDirectory());
        var isZip      = File.Exists(sourcePath) &&
                         sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var isDir      = Directory.Exists(sourcePath);

        if (!isZip && !isDir)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ Sorgente non trovata: {sourcePath}");
            Console.Error.WriteLine("   Atteso: una directory oppure un file .zip");
            Console.ResetColor();
            return 1;
        }

        // ── 2. Risolvi la chiave di firma ─────────────────────────────────────
        IPwsSigningKey signingKey;
        string?        publicKeyExport = null;

        var signSpec = opts.Sign.Trim();
        var signLow  = signSpec.ToLowerInvariant();

        if (signLow == "none")
        {
            signingKey = PwsSigningKey.None();
        }
        else if (signLow == "ecdsa")
        {
            var (full, _, pubExport) = PwsSigningKey.GenerateEcDsa();
            signingKey      = full;
            publicKeyExport = pubExport;
        }
        else if (signLow.StartsWith("hmac:"))
        {
            var secret = signSpec[5..]; // preserva maiuscole/minuscole del segreto
            if (string.IsNullOrEmpty(secret))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("✗ Segreto HMAC mancante. Formato atteso: --sign hmac:<segreto>");
                Console.ResetColor();
                return 1;
            }
            signingKey = PwsSigningKey.FromHmac(secret);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ Algoritmo di firma non riconosciuto: '{opts.Sign}'");
            Console.Error.WriteLine("   Valori validi: none | ecdsa | hmac:<segreto>");
            Console.ResetColor();
            return 1;
        }

        // ── 3. Costruisci PwsSiteSource ───────────────────────────────────────
        var siteId = opts.SiteId;
        var title  = opts.Title ?? siteId;

        PwsSiteSource site;

        if (isZip)
        {
            // Legge tutte le entry dello ZIP in memoria prima di chiuderlo,
            // così le Func<Stream> restano valide durante il packing.
            site = new PwsSiteSource
            {
                Id         = siteId,
                Title      = title,
                EntryPoint = opts.EntryPoint,
            };

            Console.WriteLine($"🗜️  Lettura ZIP: {sourcePath}");

            using var zip        = ZipFile.OpenRead(sourcePath);
            var       fileCount  = 0;

            foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                // Salta le entry di tipo "directory" (Name vuoto)
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var rel = entry.FullName.Replace('\\', '/');

                // Leggi subito in memoria (il ZIP verrà chiuso dopo questo foreach)
                using var ms  = new MemoryStream((int)entry.Length);
                using var src = entry.Open();
                src.CopyTo(ms);
                var bytes = ms.ToArray();

                site.AddFile(rel, () => new MemoryStream(bytes));
                fileCount++;
            }

            Console.WriteLine($"   {fileCount} file trovati nello ZIP.");
        }
        else
        {
            // Directory sorgente — PwsSiteSource legge i file lazily
            site = new PwsSiteSource
            {
                Id              = siteId,
                Title           = title,
                EntryPoint      = opts.EntryPoint,
                SourceDirectory = sourcePath,
            };
        }

        // ── 4. Risolvi output e crea le directory necessarie ──────────────────
        var outputPath = Path.GetFullPath(opts.Output, Directory.GetCurrentDirectory());
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // ── 5. Stampa riepilogo pre-pack ──────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine($"📦 Sorgente : {sourcePath} ({(isZip ? "ZIP" : "directory")})");
        Console.WriteLine($"   Output   : {outputPath}");
        Console.WriteLine($"   Sito     : id={siteId}  title=\"{title}\"  entry={opts.EntryPoint}");
        Console.WriteLine($"   Firma    : {(signLow == "none" ? "nessuna (alg:none)" : opts.Sign)}");
        Console.WriteLine();

        // ── 6. Packing ────────────────────────────────────────────────────────
        try
        {
            var packer = new PwsPacker();
            await packer.PackAsync(
                new PwsPackOptions
                {
                    Sites      = [site],
                    SigningKey  = signingKey,
                },
                outputPath);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ Errore durante il packing: {ex.Message}");
            Console.ResetColor();
            logger.LogError(ex, "Errore durante il packing di {Source} → {Output}", sourcePath, outputPath);
            return 1;
        }

        // ── 7. Risultato ──────────────────────────────────────────────────────
        var fileSize = new FileInfo(outputPath).Length;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Archivio creato: {outputPath}");
        Console.ResetColor();
        Console.WriteLine($"   Dimensione: {fileSize / 1024.0:F1} KB");

        if (publicKeyExport is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"   Chiave pubblica (ES256):");
            Console.WriteLine($"   {publicKeyExport}");

            if (opts.KeyOut is { } keyOutPath)
            {
                keyOutPath = Path.GetFullPath(keyOutPath, Directory.GetCurrentDirectory());
                await File.WriteAllTextAsync(keyOutPath, publicKeyExport);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   Salvata in: {keyOutPath}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("   ⚠️  Usa --key-out <file> per salvare la chiave pubblica su disco.");
                Console.ResetColor();
            }
        }

        return 0;
    }
}

