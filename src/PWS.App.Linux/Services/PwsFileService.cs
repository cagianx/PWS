using Microsoft.Extensions.Logging;
using PWS.Core.Providers;

namespace PWS.App.Linux.Services;

/// <summary>
/// Servizio singleton che mantiene il riferimento al <see cref="PwsContentProvider"/>
/// corrente. Permette di sostituire dinamicamente il provider quando si apre un nuovo file.
/// </summary>
public sealed class PwsFileService : IDisposable
{
    private readonly ILogger<PwsFileService> _logger;
    private PwsContentProvider? _currentProvider;

    public PwsFileService(ILogger<PwsFileService> logger)
    {
        _logger = logger;
        _logger.LogDebug("PwsFileService creato.");
    }

    /// <summary>Provider attualmente aperto, o <see langword="null"/> se nessun file è aperto.</summary>
    public PwsContentProvider? CurrentProvider => _currentProvider;

    /// <summary>Event sollevato quando un nuovo file .pws viene aperto.</summary>
    public event EventHandler<PwsContentProvider>? FileOpened;

    /// <summary>
    /// Imposta un nuovo provider. Dispone automaticamente il provider precedente.
    /// </summary>
    public void SetProvider(PwsContentProvider provider)
    {
        _logger.LogDebug("PwsFileService.SetProvider: sostituisco provider corrente. HadPrevious={HadPrevious}", _currentProvider is not null);
        _currentProvider?.Dispose();
        _currentProvider = provider;
        _logger.LogInformation("PwsFileService.SetProvider: provider impostato. DefaultSiteId={SiteId}", provider.DefaultSiteId);
        FileOpened?.Invoke(this, provider);
    }

    public void Dispose()
    {
        _logger.LogDebug("PwsFileService.Dispose: dispose provider corrente. HasProvider={HasProvider}", _currentProvider is not null);
        _currentProvider?.Dispose();
        _currentProvider = null;
    }
}

