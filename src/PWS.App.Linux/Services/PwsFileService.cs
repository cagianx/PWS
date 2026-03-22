using Microsoft.Extensions.Logging;
using PWS.Core.Providers;

namespace PWS.App.Linux.Services;

/// <summary>
/// Servizio singleton che mantiene il riferimento al <see cref="PwsContentProvider"/>
/// corrente e al corrispondente <see cref="LoopbackContentServer"/> dedicato al sito.
/// </summary>
public sealed class PwsFileService : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PwsFileService> _logger;
    private PwsContentProvider? _currentProvider;
    private LoopbackContentServer? _currentServer;

    public PwsFileService(ILoggerFactory loggerFactory, ILogger<PwsFileService> logger)
    {
        _loggerFactory = loggerFactory;
        _logger        = logger;
        _logger.LogDebug("PwsFileService creato.");
    }

    /// <summary>Provider attualmente aperto, o <see langword="null"/> se nessun file è aperto.</summary>
    public PwsContentProvider? CurrentProvider => _currentProvider;

    /// <summary>
    /// Server loopback dedicato al sito corrente, o <see langword="null"/> se nessun file è aperto.
    /// Il browser punta direttamente a <see cref="LoopbackContentServer.BaseAddress"/>.
    /// </summary>
    public LoopbackContentServer? CurrentServer => _currentServer;

    /// <summary>Event sollevato quando un nuovo file .pws viene aperto.</summary>
    public event EventHandler<PwsContentProvider>? FileOpened;

    /// <summary>
    /// Imposta un nuovo provider e avvia un server loopback dedicato su una porta libera.
    /// Dispone automaticamente il provider e il server precedenti.
    /// </summary>
    public void SetProvider(PwsContentProvider provider)
    {
        _logger.LogDebug(
            "PwsFileService.SetProvider: sostituisco provider e server. HadPrevious={Had}",
            _currentProvider is not null);

        _currentServer?.Dispose();
        _currentProvider?.Dispose();

        _currentProvider = provider;

        var siteId = provider.DefaultSiteId ?? "site";
        _currentServer = new LoopbackContentServer(
            provider,
            siteId,
            _loggerFactory.CreateLogger<LoopbackContentServer>());

        _logger.LogInformation(
            "PwsFileService.SetProvider: sito '{SiteId}' disponibile su {Url}",
            siteId, _currentServer.BaseAddress);

        FileOpened?.Invoke(this, provider);
    }

    public void Dispose()
    {
        _logger.LogDebug("PwsFileService.Dispose");
        _currentServer?.Dispose();
        _currentProvider?.Dispose();
        _currentServer   = null;
        _currentProvider = null;
    }
}
