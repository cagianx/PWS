using System.Diagnostics;
using PWS.IntegrationTests.Common;

namespace PWS.IntegrationTests.Tool;

/// <summary>
/// Entry point di <c>pwstool</c>: il processo viene lanciato davvero (<c>dotnet PWS.Tool.dll</c>),
/// così si verificano parsing degli argomenti, output e codice di uscita come li vede l'utente.
/// </summary>
[TestFixture]
public sealed class CommandLineTests : ArchiveTestBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Test]
    public async Task Senza_argomenti_stampa_l_aiuto_e_termina_con_1()
    {
        var result = await RunToolAsync();

        result.ExitCode.Should().Be(1);
        result.StdOut.Should().Contain("Uso:  pwstool <verbo> [opzioni]");
    }

    [Test]
    public async Task Un_verbo_sconosciuto_viene_segnalato_con_aiuto_e_codice_1()
    {
        var result = await RunToolAsync("unpack");

        result.ExitCode.Should().Be(1);
        result.StdErr.Should().Contain("Verbo sconosciuto: 'unpack'");
        result.StdOut.Should().Contain("Verbi disponibili:");
    }

    [Test]
    public async Task Pack_senza_sorgente_termina_con_errore_senza_creare_l_archivio()
    {
        var output = PathInWorkDir("sito.pws");

        var result = await RunToolAsync("pack", "-o", output);

        result.ExitCode.Should().NotBe(0);
        File.Exists(output).Should().BeFalse();
    }

    [Test]
    public async Task Pack_senza_output_termina_con_errore()
    {
        var result = await RunToolAsync("pack", CreateSiteDirectory("docs"));

        result.ExitCode.Should().NotBe(0);
        Directory.EnumerateFiles(WorkDir, "*.pws", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Test]
    public async Task Pack_e_validate_da_riga_di_comando_terminano_con_0()
    {
        var output = PathInWorkDir("sito.pws");

        var pack     = await RunToolAsync("pack", CreateSiteDirectory("docs"), "-o", output, "--id", "docs");
        var validate = await RunToolAsync("validate", output);

        pack.ExitCode.Should().Be(0, pack.StdErr);
        validate.ExitCode.Should().Be(0, validate.StdErr);
    }

    private sealed record ToolResult(int ExitCode, string StdOut, string StdErr);

    private async Task<ToolResult> RunToolAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            WorkingDirectory       = WorkDir,
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "PWS.Tool.dll"));
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossibile avviare pwstool.");

        var stdOut = process.StandardOutput.ReadToEndAsync();
        var stdErr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ToolResult(process.ExitCode, await stdOut, await stdErr);
    }
}
