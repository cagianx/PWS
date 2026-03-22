using CommandLine;

namespace PWS.Tool.Commands;

/// <summary>
/// Opzioni per il verbo <c>validate</c>.
/// Uso: pwstool validate --file &lt;path.pws&gt; [--require-signed]
/// </summary>
[Verb("validate", HelpText = "Apre un archivio .pws e ne verifica l'integrità (manifest, JWT, hash Merkle).")]
public sealed class ValidateOptions
{
    [Value(0,
        MetaName = "file",
        Required = true,
        HelpText = "Percorso del file .pws da validare.")]
    public string FilePath { get; set; } = string.Empty;

    [Option('s', "require-signed",
        Default = false,
        HelpText = "Rifiuta archivi con token non firmati (alg:none).")]
    public bool RequireSigned { get; set; }

    [Option('v', "verbose",
        Default = false,
        HelpText = "Mostra dettagli aggiuntivi (hash, file count, iat…).")]
    public bool Verbose { get; set; }
}

