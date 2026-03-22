using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PWS.Core.Abstractions;
using PWS.Core.Models;

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
    private string _addressText = "pws://home";
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

    private string _statusMessage = "Pronto";
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

        NavigateCommand  = new Command<string?>(async url => await NavigateTo(url));
        GoBackCommand    = new Command(async () => await GoBack(),    () => CanGoBack);
        GoForwardCommand = new Command(async () => await GoForward(), () => CanGoForward);
        RefreshCommand   = new Command(async () => await Refresh(),   () => !IsBusy);
        StopCommand      = new Command(Stop, () => IsBusy);

        _navigation.Navigating += OnNavigating;
        _navigation.Navigated  += OnNavigated;
    }

    // ── Entry point all'avvio ──────────────────────────────────────
    public Task InitializeAsync() => NavigateTo("pws://home");

    /// <summary>Naviga ad un URI specifico (usato da codice esterno).</summary>
    public Task NavigateToUri(string uri) => NavigateTo(uri);

    // ── Implementazioni comandi ────────────────────────────────────
    private async Task NavigateTo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await _navigation.NavigateAsync(uri, _cts.Token);
        }
        catch (OperationCanceledException) { /* ignorato */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error navigating to '{Uri}'.", uri);
            StatusMessage = $"Errore: {ex.Message}";
        }
    }

    private async Task GoBack()
    {
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
        _cts?.Cancel();
        IsBusy = false;
        StatusMessage = "Interrotto";
    }

    // ── Gestione eventi di navigazione ────────────────────────────
    private void OnNavigating(object? sender, Core.Abstractions.NavigationEventArgs e)
    {
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
        PageTitle = e.Entry.Title ?? e.Entry.Uri.Host;
        StatusMessage = e.Response?.IsSuccess == true ? "Completato" : $"Errore {e.Response?.StatusCode}";

        if (e.Response is { } resp)
        {
            // leaveOpen: true — NavigationService dispone ContentResponse (e il suo stream)
            // dopo che tutti i listener hanno finito di leggerlo.
            using var reader = new StreamReader(resp.Content,
                System.Text.Encoding.UTF8, leaveOpen: true);
            HtmlContent = reader.ReadToEnd();
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
