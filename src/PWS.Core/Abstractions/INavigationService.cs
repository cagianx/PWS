using PWS.Core.Models;

namespace PWS.Core.Abstractions;

/// <summary>
/// Gestisce la navigazione del browser: history, back, forward, reload.
/// </summary>
public interface INavigationService
{
    /// <summary>Entry corrente nella history.</summary>
    NavigationEntry? Current { get; }

    /// <summary>True se è possibile tornare indietro.</summary>
    bool CanGoBack { get; }

    /// <summary>True se è possibile andare avanti.</summary>
    bool CanGoForward { get; }

    /// <summary>Naviga verso un nuovo URI.</summary>
    Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>Torna alla pagina precedente nella history.</summary>
    Task GoBackAsync(CancellationToken cancellationToken = default);

    /// <summary>Avanza alla pagina successiva nella history.</summary>
    Task GoForwardAsync(CancellationToken cancellationToken = default);

    /// <summary>Ricarica la pagina corrente.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Evento lanciato prima della navigazione.</summary>
    event EventHandler<NavigationEventArgs>? Navigating;

    /// <summary>Evento lanciato dopo il completamento della navigazione.</summary>
    event EventHandler<NavigationEventArgs>? Navigated;
}

public sealed class NavigationEventArgs(NavigationEntry entry, ContentResponse? response = null) : EventArgs
{
    public NavigationEntry Entry { get; } = entry;
    public ContentResponse? Response { get; } = response;
}

