using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PWS.Core.Abstractions;

namespace PWS.App.Linux.ViewModels;

/// <summary>
/// ViewModel principale del browser.
/// Espone tutto ciò che serve alla UI: barra indirizzi, pulsanti, contenuto.
/// </summary>
public sealed class BrowserViewModel : BaseViewModel
{
    private readonly INavigationService      _navigation;
    private readonly ILogger<BrowserViewModel> _logger;

    // ── Stato barra indirizzi ────────────────────────────────────────
    private string _addressText = string.Empty;
    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    // ── Pulsanti navigazione ────────────────────────────────────────
    private bool _canGoBack;
    public bool CanGoBack
    {
        get => _canGoBack;
        private set => SetProperty(ref _canGoBack, value);
    }

    private bool _canGoForward;
    public bool CanGoForward
    {
        get => _canGoForward;
        private set => SetProperty(ref _canGoForward, value);
    }

    // ── Contenuto caricato ─────────────────────────────────────────
    private string _pageTitle = string.Empty;
    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    private string _htmlContent = string.Empty;
    /// <summary>HTML da mostrare nella WebView.</summary>
    public string HtmlContent
    {
        get => _htmlContent;
        private set => SetProperty(ref _htmlContent, value);
    }

    private string _statusMessage = "Inserisci un URI pws://<siteId>/index.html e premi Vai";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Comandi ────────────────────────────────────────────────────
    public ICommand NavigateCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand StopCommand { get; }

    // ── Cancellation per stop ──────────────────────────────────────
    private CancellationTokenSource? _cts;

    public BrowserViewModel(
        INavigationService          navigation,
        ILogger<BrowserViewModel>   logger)
    {
        _navigation = navigation;
        _logger     = logger;

        NavigateCommand  = new Command<string?>(url => _ = NavigateTo(url));
        GoBackCommand    = new Command(() => _ = GoBack(),    () => CanGoBack);
        GoForwardCommand = new Command(() => _ = GoForward(), () => CanGoForward);
        RefreshCommand   = new Command(() => _ = Refresh(),   () => !IsBusy);
        StopCommand      = new Command(Stop, () => IsBusy);

        _navigation.Navigating += OnNavigating;
        _navigation.Navigated  += OnNavigated;

        _logger.LogDebug("BrowserViewModel creato. Startup neutro: nessuna navigazione automatica.");
    }

    // ── Entry point all'avvio ──────────────────────────────────────
    public Task InitializeAsync() => NavigateTo("pws://home");

    /// <summary>Naviga ad un URI specifico (usato da codice esterno).</summary>
    public Task NavigateToUri(string uri) => NavigateTo(uri);


    // ── Implementazioni comandi ────────────────────────────────────
    private async Task NavigateTo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogTrace("NavigateTo ignorato: URL vuoto.");
            StatusMessage = "Inserisci un URI pws:// valido";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("URI non valido: '{Url}'", url);
            StatusMessage = $"URI non valido: {url}";
            return;
        }

        _logger.LogDebug("NavigateTo → {Uri}", uri);

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await _navigation.NavigateAsync(uri, _cts.Token);
            _logger.LogDebug("NavigateTo completato: {Uri}", uri);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("NavigateTo cancellato: {Uri}", uri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error navigating to '{Uri}'.", uri);
            StatusMessage = $"Errore: {ex.Message}";
        }
    }

    private async Task GoBack()
    {
        _logger.LogDebug("GoBack richiesto.");
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await _navigation.GoBackAsync(_cts.Token);
        }
        catch (OperationCanceledException) { /* ignorato */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error on GoBack.");
            StatusMessage = $"Errore: {ex.Message}";
        }
    }

    private async Task GoForward()
    {
        _logger.LogDebug("GoForward richiesto.");
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await _navigation.GoForwardAsync(_cts.Token);
        }
        catch (OperationCanceledException) { /* ignorato */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error on GoForward.");
            StatusMessage = $"Errore: {ex.Message}";
        }
    }

    private async Task Refresh()
    {
        _logger.LogDebug("Refresh richiesto.");
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await _navigation.RefreshAsync(_cts.Token);
        }
        catch (OperationCanceledException) { /* ignorato */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error on Refresh.");
            StatusMessage = $"Errore: {ex.Message}";
        }
    }

    private void Stop()
    {
        _logger.LogDebug("Stop richiesto.");
        _cts?.Cancel();
        IsBusy = false;
        StatusMessage = "Interrotto";
    }

    // ── Gestione eventi di navigazione ────────────────────────────
    private void OnNavigating(object? sender, Core.Abstractions.NavigationEventArgs e)
    {
        _logger.LogDebug("OnNavigating → {Uri}", e.Entry.Uri);
        IsBusy = true;
        AddressText = e.Entry.Uri.ToString();
        StatusMessage = $"Caricamento {e.Entry.Uri}…";
        RefreshNavButtons();
        InvalidateCommands();
    }

    private void OnNavigated(object? sender, Core.Abstractions.NavigationEventArgs e)
    {
        IsBusy = false;
        AddressText = e.Entry.Uri.ToString();
        PageTitle   = e.Entry.Title ?? e.Entry.Uri.Host;

        var ok = e.Response?.IsSuccess == true;
        StatusMessage = ok ? "Completato" : $"Errore {e.Response?.StatusCode}";

        _logger.LogDebug("OnNavigated ← {Uri}  status={Status}  hasResponse={HasResp}",
            e.Entry.Uri, e.Response?.StatusCode, e.Response is not null);

        if (e.Response is { } resp)
        {
            _logger.LogTrace("OnNavigated: inizio lettura stream per {Uri}", e.Entry.Uri);
            using var reader = new StreamReader(resp.Content,
                System.Text.Encoding.UTF8, leaveOpen: true);
            HtmlContent = reader.ReadToEnd();

            _logger.LogDebug("HtmlContent set: {Len} chars  (ok={Ok})",
                HtmlContent.Length, ok);
        }
        else
        {
            _logger.LogWarning("OnNavigated: Response è null per {Uri}", e.Entry.Uri);
        }

        RefreshNavButtons();
        InvalidateCommands();
    }

    private void RefreshNavButtons()
    {
        CanGoBack    = _navigation.CanGoBack;
        CanGoForward = _navigation.CanGoForward;
    }

    private void InvalidateCommands()
    {
        (GoBackCommand    as Command)?.ChangeCanExecute();
        (GoForwardCommand as Command)?.ChangeCanExecute();
        (RefreshCommand   as Command)?.ChangeCanExecute();
        (StopCommand      as Command)?.ChangeCanExecute();
    }
}
