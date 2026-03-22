using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PWS.App.Linux.Services;

namespace PWS.App.Linux.ViewModels;

/// <summary>
/// ViewModel principale del browser.
/// La navigazione avviene interamente su HTTP loopback: il browser punta direttamente
/// al <see cref="LoopbackContentServer"/> dedicato al sito aperto.
/// </summary>
public sealed class BrowserViewModel : BaseViewModel
{
    private readonly PwsFileService _pwsFileService;
    private readonly ILogger<BrowserViewModel> _logger;

    // ── Stato barra indirizzi ────────────────────────────────────────
    private string _addressText = string.Empty;
    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    // ── Pulsanti navigazione ─────────────────────────────────────────
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

    // ── Titolo pagina ────────────────────────────────────────────────
    private string _pageTitle = "PWS Browser";
    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    // ── URL da caricare nella WebView ────────────────────────────────
    private string _renderedUrl = string.Empty;
    /// <summary>
    /// URL HTTP loopback che la WebView deve caricare.
    /// Cambia solo per navigazione programmatica (apertura sito, digitazione in barra indirizzi).
    /// La navigazione tra link interni avviene nativamente nella WebView.
    /// </summary>
    public string RenderedUrl
    {
        get => _renderedUrl;
        private set => SetProperty(ref _renderedUrl, value);
    }

    // ── Barra di stato ───────────────────────────────────────────────
    private string _statusMessage = "Apri un file .pws per iniziare";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Comandi ──────────────────────────────────────────────────────
    public ICommand NavigateCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand StopCommand { get; }

    // ── Eventi delegati alla View ─────────────────────────────────────
    /// <summary>Richiesta di navigare indietro nella WebView.</summary>
    public event EventHandler? GoBackRequested;
    /// <summary>Richiesta di navigare avanti nella WebView.</summary>
    public event EventHandler? GoForwardRequested;
    /// <summary>Richiesta di ricaricare la pagina corrente nella WebView.</summary>
    public event EventHandler? ReloadRequested;

    public BrowserViewModel(
        PwsFileService           pwsFileService,
        ILogger<BrowserViewModel> logger)
    {
        _pwsFileService = pwsFileService;
        _logger         = logger;

        NavigateCommand  = new Command<string?>(url => NavigateTo(url));
        GoBackCommand    = new Command(
            () => GoBackRequested?.Invoke(this, EventArgs.Empty),
            () => CanGoBack);
        GoForwardCommand = new Command(
            () => GoForwardRequested?.Invoke(this, EventArgs.Empty),
            () => CanGoForward);
        RefreshCommand   = new Command(
            () => ReloadRequested?.Invoke(this, EventArgs.Empty),
            () => !IsBusy);
        StopCommand      = new Command(Stop, () => IsBusy);

        _logger.LogDebug("BrowserViewModel creato.");
    }

    // ── API pubblica chiamata da BrowserPage ──────────────────────────

    /// <summary>
    /// Naviga al sito correntemente aperto in <see cref="PwsFileService"/>.
    /// Chiamato da <see cref="Pages.BrowserPage"/> al momento dell'OnAppearing.
    /// </summary>
    public void NavigateToCurrentSite()
    {
        var server = _pwsFileService.CurrentServer;
        if (server is null)
        {
            StatusMessage = "Apri un file .pws per iniziare";
            return;
        }

        _logger.LogDebug("BrowserViewModel.NavigateToCurrentSite: {Url}", server.BaseAddress);
        NavigateTo(server.BaseAddress);
    }

    /// <summary>
    /// Chiamato da <see cref="Pages.BrowserPage"/> quando WebView inizia a navigare.
    /// Aggiorna barra indirizzi e stato subito (prima del completamento).
    /// </summary>
    public void OnPageNavigating(string url)
    {
        AddressText   = url;
        IsBusy        = true;
        StatusMessage = "Caricamento…";
        InvalidateCommands();
    }

    /// <summary>
    /// Chiamato da <see cref="Pages.BrowserPage"/> dopo il completamento della navigazione
    /// (<c>WebView.Navigated</c>). Aggiorna stato, CanGoBack/Forward.
    /// </summary>
    public void OnWebViewNavigated(string url, bool canGoBack, bool canGoForward)
    {
        AddressText   = url;
        CanGoBack     = canGoBack;
        CanGoForward  = canGoForward;
        IsBusy        = false;
        StatusMessage = "Completato";

        _logger.LogDebug(
            "OnWebViewNavigated: url={Url} back={Back} fwd={Fwd}", url, canGoBack, canGoForward);
        InvalidateCommands();
    }

    // ── Implementazioni comandi ───────────────────────────────────────

    private void NavigateTo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "URL non valido";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!uri.Scheme.Equals("http",  StringComparison.OrdinalIgnoreCase) &&
             !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("BrowserViewModel.NavigateTo: schema non supportato '{Url}'", url);
            StatusMessage = $"Schema non supportato. Usa http:// — {url}";
            return;
        }

        _logger.LogDebug("BrowserViewModel.NavigateTo → {Url}", url);

        AddressText   = url;
        IsBusy        = true;
        StatusMessage = "Caricamento…";
        RenderedUrl   = url;        // scatena PropertyChanged → BrowserPage carica la WebView
    }

    private void Stop()
    {
        _logger.LogDebug("Stop richiesto.");
        IsBusy        = false;
        StatusMessage = "Interrotto";
        InvalidateCommands();
    }

    private void InvalidateCommands()
    {
        (GoBackCommand    as Command)?.ChangeCanExecute();
        (GoForwardCommand as Command)?.ChangeCanExecute();
        (RefreshCommand   as Command)?.ChangeCanExecute();
        (StopCommand      as Command)?.ChangeCanExecute();
    }
}
