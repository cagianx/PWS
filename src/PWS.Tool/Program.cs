using CommandLine;
using Microsoft.Extensions.Logging;
using PWS.Tool.Commands;

// Crea un logger semplice su console (colori) per i messaggi di diagnostica
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddSimpleConsole(o =>
    {
        o.SingleLine      = true;
        o.ColorBehavior   = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
        o.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger("pwstool");

// ── Verb routing ─────────────────────────────────────────────────────────────
// CommandLineParser con singolo tipo non consuma il nome del verb automaticamente;
// lo gestiamo manualmente così da supportare futuri comandi senza riscrivere il main.

if (args.Length == 0 || args[0].StartsWith('-'))
{
    PrintHelp();
    return 1;
}

var verb     = args[0];
var verbArgs = args[1..];

return verb.ToLowerInvariant() switch
{
    "validate" => await RunVerb<ValidateOptions>(
        verbArgs,
        async opts => await ValidateCommand.RunAsync(opts, logger)),

    "pack" => await RunVerb<PackOptions>(
        verbArgs,
        async opts => await PackCommand.RunAsync(opts, logger)),

    _ => UnknownVerb(verb),
};

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task<int> RunVerb<T>(string[] verbArgs, Func<T, Task<int>> handler)
{
    int code = 1;
    await Parser.Default
        .ParseArguments<T>(verbArgs)
        .WithParsedAsync(async opts => code = await handler(opts));
    return code;
}

static int UnknownVerb(string verb)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"✗ Verbo sconosciuto: '{verb}'");
    Console.ResetColor();
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("pwstool — strumento a riga di comando per archivi .pws");
    Console.WriteLine();
    Console.WriteLine("Uso:  pwstool <verbo> [opzioni]");
    Console.WriteLine();
    Console.WriteLine("Verbi disponibili:");
    Console.WriteLine("  validate <file.pws>            Verifica l'integrità di un archivio .pws");
    Console.WriteLine("  pack     <source> -o <out.pws> Crea un archivio .pws da directory o .zip");
    Console.WriteLine();
    Console.WriteLine("Esempi:");
    Console.WriteLine("  pwstool validate mysite.pws");
    Console.WriteLine("  pwstool validate mysite.pws --require-signed --verbose");
    Console.WriteLine("  pwstool pack ./docs/build -o mysite.pws --id docs --title \"My Docs\"");
    Console.WriteLine("  pwstool pack ./docs/build -o mysite.pws --sign ecdsa --key-out pubkey.txt");
    Console.WriteLine("  pwstool pack ./dist.zip    -o mysite.pws --sign hmac:mysecret");
}
