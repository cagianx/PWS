using Microsoft.Extensions.Logging;
using Platform.Maui.Linux.Gtk4.Hosting;
using PWS.App.Linux.Services;
using PWS.App.Linux.ViewModels;
using PWS.Core.Abstractions;
using PWS.Core.Models;
using PWS.Core.Navigation;
using PWS.Core.Providers;
using Serilog;
using Serilog.Events;

namespace PWS.App.Linux;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // ── Serilog — log su file rotante in ~/.local/share/PWS/logs/ ────────
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PWS", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System",    LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path:                 Path.Combine(logDir, "pws-.log"),
                rollingInterval:      RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate:       "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("PWS Browser starting. Log directory: {LogDir}", logDir);

        var builder = MauiApp
            .CreateBuilder()
            .UseMauiAppLinuxGtk4<App>();

        // ── Logging: Serilog come provider Microsoft.Extensions.Logging ─────
        builder.Logging
            .ClearProviders()
            .AddSerilog(Log.Logger, dispose: true);

        // ── Services ────────────────────────────────────────────────
        // Servizio che mantiene il PwsContentProvider corrente
        builder.Services.AddSingleton<PwsFileService>();
        builder.Services.AddSingleton<IPwsArchivePicker, GtkPwsArchivePicker>();
        builder.Services.AddSingleton<ErrorDialogService>();

        // ── Core (PWS.Core) ─────────────────────────────────────────
        // Provider in-memory per schema pws://
        builder.Services.AddSingleton<InMemoryContentProvider>(_ =>
            new InMemoryContentProvider("pws"));

        // Provider composito dinamico: include PwsContentProvider quando disponibile
        builder.Services.AddSingleton<IContentProvider>(sp =>
        {
            var inMemoryProvider = sp.GetRequiredService<InMemoryContentProvider>();
            var pwsFileService   = sp.GetRequiredService<PwsFileService>();
            var logger           = sp.GetRequiredService<ILogger<DynamicCompositeContentProvider>>();

            // DynamicCompositeContentProvider che delega a pwsFileService.CurrentProvider
            return new DynamicCompositeContentProvider(inMemoryProvider, pwsFileService, logger);
        });

        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // ── UI (PWS.App) ────────────────────────────────────────────
        builder.Services.AddTransient<BrowserViewModel>();

        return builder.Build();
    }

    /// <summary>
    /// CompositeContentProvider che include dinamicamente il PwsContentProvider
    /// dal PwsFileService quando disponibile.
    /// </summary>
    private sealed class DynamicCompositeContentProvider : IContentProvider
    {
        private readonly InMemoryContentProvider _inMemory;
        private readonly PwsFileService _pwsFileService;
        private readonly ILogger<DynamicCompositeContentProvider> _logger;

        public DynamicCompositeContentProvider(
            InMemoryContentProvider inMemory,
            PwsFileService pwsFileService,
            ILogger<DynamicCompositeContentProvider> logger)
        {
            _inMemory = inMemory;
            _pwsFileService = pwsFileService;
            _logger = logger;
            _logger.LogDebug("DynamicCompositeContentProvider creato.");
        }

        public bool CanHandle(Uri uri)
        {
            var inMemory = _inMemory.CanHandle(uri);
            var current  = _pwsFileService.CurrentProvider?.CanHandle(uri) ?? false;
            var result   = inMemory || current;

            _logger.LogTrace("DynamicComposite.CanHandle({Uri}) => {Result} [pwsCurrent={Current}, inMemory={InMemory}]",
                uri, result, current, inMemory);

            return result;
        }

        public Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
        {
            // PwsContentProvider ha CanHandle più specifico (verifica il site ID dell'archivio),
            // quindi viene controllato prima. InMemoryContentProvider è il fallback per
            // le rotte built-in (pws://home, pws://about, ecc.).
            if (_pwsFileService.CurrentProvider?.CanHandle(request.Uri) == true)
            {
                _logger.LogDebug("DynamicComposite.GetAsync: uso PwsContentProvider per {Uri}", request.Uri);
                return _pwsFileService.CurrentProvider.GetAsync(request, cancellationToken);
            }

            if (_inMemory.CanHandle(request.Uri))
            {
                _logger.LogDebug("DynamicComposite.GetAsync: uso InMemoryContentProvider per {Uri}", request.Uri);
                return _inMemory.GetAsync(request, cancellationToken);
            }

            _logger.LogWarning("DynamicComposite.GetAsync: nessun provider per {Uri}", request.Uri);

            return Task.FromResult(ContentResponse.Error(404,
                $"Nessun provider registrato per '{request.Uri}'."));
        }
    }
}
