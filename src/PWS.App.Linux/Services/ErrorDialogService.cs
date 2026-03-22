using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using PWS.App.Linux.Pages;
using Serilog;

namespace PWS.App.Linux.Services;

/// <summary>
/// Mostra gli errori in una pagina modale con stack trace completo,
/// li registra su file tramite ILogger e li stampa su stderr per il debug da terminale.
/// </summary>
public sealed class ErrorDialogService
{
    private readonly ILogger<ErrorDialogService> _logger;

    public ErrorDialogService(ILogger<ErrorDialogService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logga l'eccezione e apre la pagina di errore modale.
    /// Può essere chiamato da qualsiasi thread: il dispatch sul main thread è gestito internamente.
    /// </summary>
    /// <param name="ex">Eccezione da mostrare.</param>
    /// <param name="context">Descrizione del contesto (es. "Apertura file .pws").</param>
    public async Task ShowAsync(Exception ex, string? context = null)
    {
        // 1. Log su file (Serilog)
        _logger.LogError(ex, "Unhandled error{Context}",
            context is null ? string.Empty : $" [{context}]");

        // 2. Output su console/stderr — visibile quando si lancia da terminale
        var separator = new string('─', 60);
        Console.Error.WriteLine(separator);
        Console.Error.WriteLine($"[ERROR] {(context is null ? string.Empty : $"[{context}] ")}{ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine(ex.ToString());
        Console.Error.WriteLine(separator);

        // 3. Pagina modale (deve girare sul thread UI)
        var mainPage = Application.Current?.MainPage;
        if (mainPage is null) return;

        await mainPage.Dispatcher.DispatchAsync(async () =>
            await mainPage.Navigation.PushModalAsync(new ErrorPage(ex, context)));
    }

    /// <summary>
    /// Variante sincrona usabile negli handler di eccezioni non osservate
    /// (<see cref="AppDomain.UnhandledException"/>,
    ///  <see cref="TaskScheduler.UnobservedTaskException"/>).
    /// Logga e stampa a console senza tentare di mostrare la UI (che potrebbe non essere disponibile).
    /// </summary>
    public void LogFatal(Exception? ex, string? context = null)
    {
        _logger.LogCritical(ex, "Fatal error{Context}",
            context is null ? string.Empty : $" [{context}]");

        var separator = new string('═', 60);
        Console.Error.WriteLine(separator);
        Console.Error.WriteLine($"[FATAL] {(context is null ? string.Empty : $"[{context}] ")}{ex?.GetType().Name}: {ex?.Message}");
        Console.Error.WriteLine(ex?.ToString());
        Console.Error.WriteLine(separator);
    }
}

