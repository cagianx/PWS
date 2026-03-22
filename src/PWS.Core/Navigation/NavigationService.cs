using Microsoft.Extensions.Logging;
using PWS.Core.Abstractions;
using PWS.Core.Models;

namespace PWS.Core.Navigation;

/// <summary>
/// Implementazione di INavigationService che coordina la navigazione
/// tra IContentProvider e NavigationHistory.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IContentProvider          _contentProvider;
    private readonly NavigationHistory         _history = new();
    private readonly ILogger<NavigationService>? _logger;

    public NavigationService(
        IContentProvider              contentProvider,
        ILogger<NavigationService>?   logger = null)
    {
        _contentProvider = contentProvider;
        _logger          = logger;
    }

    public NavigationEntry? Current => _history.Current;
    public bool CanGoBack => _history.CanGoBack;
    public bool CanGoForward => _history.CanGoForward;

    public event EventHandler<NavigationEventArgs>? Navigating;
    public event EventHandler<NavigationEventArgs>? Navigated;

    public async Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var entry = new NavigationEntry { Uri = uri };
        Navigating?.Invoke(this, new NavigationEventArgs(entry));

        var response = await FetchAsync(uri, cancellationToken);

        // Aggiorna il titolo dalla risposta
        if (response.Title is { } title)
            entry.Title = title;
        if (response.FinalUri is { } finalUri)
        {
            entry = new NavigationEntry
            {
                Uri = new Uri(finalUri),
                Title = entry.Title,
                Timestamp = entry.Timestamp
            };
        }

        _history.Push(entry);
        // Gli handler di Navigated devono leggere Content prima del dispose.
        Navigated?.Invoke(this, new NavigationEventArgs(entry, response));
        response.Dispose();
    }

    public async Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (_history.GoBack() is { } entry)
            await NavigateToEntryAsync(entry, cancellationToken);
    }

    public async Task GoForwardAsync(CancellationToken cancellationToken = default)
    {
        if (_history.GoForward() is { } entry)
            await NavigateToEntryAsync(entry, cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Current is { } entry)
            await NavigateToEntryAsync(entry, cancellationToken);
    }

    // ──────────────────────────────────────────────
    private async Task NavigateToEntryAsync(NavigationEntry entry, CancellationToken cancellationToken)
    {
        Navigating?.Invoke(this, new NavigationEventArgs(entry));
        var response = await FetchAsync(entry.Uri, cancellationToken);
        Navigated?.Invoke(this, new NavigationEventArgs(entry, response));
        response.Dispose();
    }

    private async Task<ContentResponse> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!_contentProvider.CanHandle(uri))
        {
            _logger?.LogWarning("No provider available for URI '{Uri}'.", uri);
            return ContentResponse.Error(404, $"Nessun provider disponibile per '{uri}'");
        }

        try
        {
            return await _contentProvider.GetAsync(ContentRequest.Get(uri), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Error fetching '{Uri}'.", uri);
            return ContentResponse.Error(500, ex.Message);
        }
    }
}
