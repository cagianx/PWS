using Microsoft.Extensions.Logging;
using PWS.Format.Crypto;
using PWS.Format.Reading;

namespace PWS.Tool.Commands;

/// <summary>
/// Implementazione del verbo <c>validate</c>:
/// apre il file .pws, verifica il manifest, i JWT e gli hash Merkle,
/// poi stampa un riepilogo di tutti i siti trovati.
/// </summary>
public static class ValidateCommand
{
    /// <summary>
    /// Esegue la validazione e restituisce l'exit code (0 = OK, 1 = errore).
    /// </summary>
    public static async Task<int> RunAsync(ValidateOptions opts, ILogger logger)
    {
        var path = opts.FilePath;

        // ── Controllo esistenza ───────────────────────────────────────────────
        if (!File.Exists(path))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ File non trovato: {path}");
            Console.ResetColor();
            return 1;
        }

        var fileInfo = new FileInfo(path);
        Console.WriteLine($"📂 File    : {fileInfo.FullName}");
        Console.WriteLine($"   Dimensione: {fileInfo.Length / 1024.0:F1} KB");
        Console.WriteLine();

        // ── Apertura e verifica ───────────────────────────────────────────────
        PwsReader reader;
        try
        {
            // Risolvi chiave di verifica esterna (HMAC o ES256 da file/stringa)
            IPwsSigningKey? verKey = null;
            if (opts.Key is { } keySpec)
            {
                if (keySpec.StartsWith("hmac:", StringComparison.OrdinalIgnoreCase))
                {
                    verKey = PwsSigningKey.FromHmac(keySpec[5..]);
                }
                else if (File.Exists(keySpec))
                {
                    var export = (await File.ReadAllTextAsync(keySpec)).Trim();
                    verKey = PwsSigningKey.FromExport(export);
                }
                else
                {
                    // Assume raw export string ("ES256:base64…")
                    verKey = PwsSigningKey.FromExport(keySpec);
                }
            }

            var options = new PwsOpenOptions
            {
                RequireSignedTokens = opts.RequireSigned,
                Logger              = logger,
                VerificationKey     = verKey,
            };

            reader = await PwsReader.OpenAsync(path, options);
        }
        catch (InvalidDataException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ Validazione fallita: {ex.Message}");
            Console.ResetColor();
            logger.LogError(ex, "Validazione fallita per {Path}", path);
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ Errore inaspettato: {ex.Message}");
            Console.ResetColor();
            logger.LogError(ex, "Errore inaspettato durante l'apertura di {Path}", path);
            return 1;
        }

        using (reader)
        {
            // ── Riepilogo manifest ────────────────────────────────────────────
            var manifest = reader.Manifest;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Manifest valido");
            Console.ResetColor();
            Console.WriteLine($"   Versione : {manifest.Version}");
            Console.WriteLine($"   Creato   : {manifest.Created:u}");
            Console.WriteLine($"   Chiave   : {(manifest.PublicKey is null ? "(nessuna — non firmato)" : manifest.PublicKey.Split(':')[0])}");
            Console.WriteLine();

            // ── Riepilogo siti ────────────────────────────────────────────────
            Console.WriteLine($"   Siti trovati: {reader.Sites.Count}");
            Console.WriteLine();

            foreach (var site in reader.Sites)
            {
                var signed = site.IsVerified
                    ? "🔒 firmato"
                    : "⚠️  non firmato";

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ▸ {site.SiteId}  [{signed}]");
                Console.ResetColor();
                Console.WriteLine($"    Titolo    : {site.Title}");
                Console.WriteLine($"    EntryPoint: {site.EntryPoint}");

                if (opts.Verbose)
                {
                    Console.WriteLine($"    Hash      : {site.ContentHash}");
                    Console.WriteLine($"    File      : {site.FileCount}");
                    Console.WriteLine($"    IssuedAt  : {site.IssuedAt:u}");

                    // ── Lista file del sito ───────────────────────────────────
                    var siteFiles = reader.FileSystem.ListFiles(site.SiteId).ToList();
                    if (siteFiles.Count > 0)
                    {
                        Console.WriteLine($"    Contenuto ({siteFiles.Count} entry):");
                        foreach (var f in siteFiles.OrderBy(f => f.RelativePath))
                            Console.WriteLine($"      · {f.RelativePath}  ({f.Size} B)");
                    }
                }

                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Archivio .pws valido.");
            Console.ResetColor();
        }

        return 0;
    }
}





