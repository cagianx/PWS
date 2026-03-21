using PWS.Core.Providers;

namespace PWS.App.Linux.Services;

/// <summary>
/// Servizio singleton che mantiene il riferimento al <see cref="PwsContentProvider"/>
/// corrente. Permette di sostituire dinamicamente il provider quando si apre un nuovo file.
/// </summary>
public sealed class PwsFileService : IDisposable
{
    private PwsContentProvider? _currentProvider;

    /// <summary>Provider attualmente aperto, o <see langword="null"/> se nessun file è aperto.</summary>
    public PwsContentProvider? CurrentProvider => _currentProvider;

    /// <summary>Event sollevato quando un nuovo file .pws viene aperto.</summary>
    public event EventHandler<PwsContentProvider>? FileOpened;

    /// <summary>
    /// Imposta un nuovo provider. Dispone automaticamente il provider precedente.
    /// </summary>
    public void SetProvider(PwsContentProvider provider)
    {
        _currentProvider?.Dispose();
        _currentProvider = provider;
        FileOpened?.Invoke(this, provider);
    }

    public void Dispose()
    {
        _currentProvider?.Dispose();
        _currentProvider = null;
    }
}

