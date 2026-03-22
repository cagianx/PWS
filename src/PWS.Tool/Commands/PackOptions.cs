using CommandLine;

namespace PWS.Tool.Commands;

/// <summary>
/// Opzioni per il verbo <c>pack</c>.
/// Uso: pwstool pack &lt;source&gt; -o &lt;output.pws&gt; [opzioni]
/// </summary>
[Verb("pack", HelpText = "Crea un archivio .pws da una directory sorgente o da un file .zip.")]
public sealed class PackOptions
{
    /// <summary>Directory sorgente oppure file .zip da impacchettare.</summary>
    [Value(0,
        MetaName = "source",
        Required = true,
        HelpText = "Directory sorgente o file .zip da impacchettare.")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Percorso del file .pws di output.</summary>
    [Option('o', "output",
        Required = true,
        HelpText = "Percorso del file .pws di output.")]
    public string Output { get; set; } = string.Empty;

    // ── Metadati del sito ─────────────────────────────────────────────────────

    /// <summary>Identificatore del sito (alfanumerico + trattini). Default: 'site'.</summary>
    [Option('i', "id",
        Default = "site",
        HelpText = "Identificatore del sito (alfanumerico + trattini). Default: 'site'.")]
    public string SiteId { get; set; } = "site";

    /// <summary>Titolo del sito (default: uguale a --id).</summary>
    [Option('t', "title",
        HelpText = "Titolo del sito. Default: uguale a --id.")]
    public string? Title { get; set; }

    /// <summary>File di entry-point relativo alla radice del sito. Default: 'index.html'.</summary>
    [Option('e', "entry",
        Default = "index.html",
        HelpText = "File di entry-point relativo alla radice del sito. Default: 'index.html'.")]
    public string EntryPoint { get; set; } = "index.html";

    // ── Firma ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Algoritmo di firma:
    /// <list type="bullet">
    ///   <item><c>none</c> — nessuna firma (sviluppo, default)</item>
    ///   <item><c>ecdsa</c> — genera una nuova coppia di chiavi ECDSA P-256 (ES256)</item>
    ///   <item><c>hmac:&lt;segreto&gt;</c> — HMAC-SHA256 con il segreto fornito</item>
    /// </list>
    /// </summary>
    [Option('s', "sign",
        Default = "none",
        HelpText = "Algoritmo di firma: none | ecdsa | hmac:<segreto>. Default: 'none'.")]
    public string Sign { get; set; } = "none";

    /// <summary>
    /// Quando <c>--sign ecdsa</c>, scrive la chiave pubblica ES256 esportata su questo file.
    /// Se omesso la chiave viene solo stampata a console.
    /// </summary>
    [Option("key-out",
        HelpText = "Salva la chiave pubblica ES256 su file (solo con --sign ecdsa).")]
    public string? KeyOut { get; set; }
}

