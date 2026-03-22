using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using PWS.App.Linux.Services;
using PWS.App.Linux.ViewModels;

namespace PWS.App.Linux.Pages;

/// <summary>
/// Code-behind di BrowserPage.
/// La navigazione avviene interamente su HTTP loopback: tutti gli URL del sito corrente
/// vengono serviti dal <see cref="LoopbackContentServer"/> dedicato.
/// Il codice-behind è il solo punto in cui si tocca la WebView MAUI.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BrowserPage : ContentPage
{
    private bool _bindingDone;
    private WebView? _browserWebView;
    private readonly ILogger<BrowserPage> _logger;
    private CancellationTokenSource? _resizeWorkaroundCts;
    private double _lastResizeWidth;
    private double _lastResizeHeight;

    public BrowserPage()
    {
        InitializeComponent();
        _logger = IPlatformApplication.Current!.Services.GetRequiredService<ILogger<BrowserPage>>();
        _logger.LogDebug("BrowserPage ctor: InitializeComponent completato.");

        SizeChanged += OnPageSizeChanged;

        // Quando il container del WebView cambia dimensione, aggiorniamo subito
        // WidthRequest/HeightRequest sul widget nativo GTK4 (set_size_request).
        BrowserHost.SizeChanged += (_, _) => SynchronizeWebViewSize();
    }

    // ── Ciclo di vita ────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogDebug("BrowserPage.OnAppearing: bindingDone={Done}", _bindingDone);

        if (_bindingDone) return;
        _bindingDone = true;

        // GTK4 realizza i widget nativi in modo asincrono rispetto al ciclo di vita MAUI.
        _logger.LogTrace("BrowserPage.OnAppearing: attendo 100ms prima di assegnare il BindingContext.");
        await Task.Delay(100);

        var vm = IPlatformApplication.Current!.Services.GetRequiredService<BrowserViewModel>();
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Collega i comandi del VM alle operazioni native della WebView
        vm.GoBackRequested    += (_, _) => _browserWebView?.GoBack();
        vm.GoForwardRequested += (_, _) => _browserWebView?.GoForward();
        vm.ReloadRequested    += (_, _) => _browserWebView?.Reload();

        _logger.LogDebug("BrowserPage.OnAppearing: BindingContext assegnato a BrowserViewModel.");

        // Avvia subito la navigazione al sito aperto (se presente)
        vm.NavigateToCurrentSite();
    }

    // ── Sincronizzazione ViewModel → WebView ─────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _logger.LogTrace("BrowserPage.OnViewModelPropertyChanged: {Property}", e.PropertyName);
        if (sender is not BrowserViewModel vm) return;

        if (e.PropertyName != nameof(BrowserViewModel.RenderedUrl))
            return;

        if (string.IsNullOrWhiteSpace(vm.RenderedUrl))
            return;

        Dispatcher.Dispatch(() =>
        {
            EnsureWebView();
            _logger.LogDebug("BrowserPage: carico RenderedUrl nella WebView: {Url}", vm.RenderedUrl);
            _browserWebView!.Source = new UrlWebViewSource { Url = vm.RenderedUrl };
        });
    }

    private void EnsureWebView()
    {
        if (_browserWebView is not null)
            return;

        _logger.LogDebug("BrowserPage.EnsureWebView: creo WebView lazy.");

        _browserWebView = new WebView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions   = LayoutOptions.Fill,
        };
        _browserWebView.Navigating += WebView_Navigating;
        _browserWebView.Navigated  += WebView_Navigated;
        BrowserHost.Content = _browserWebView;

        // Applica subito la dimensione esplicita per GTK4/WebKit
        SynchronizeWebViewSize();
    }

    /// <summary>
    /// Imposta esplicitamente <see cref="VisualElement.WidthRequest"/> e
    /// <see cref="VisualElement.HeightRequest"/> della WebView in base alle dimensioni
    /// correnti di <see cref="BrowserHost"/>. Su GTK4/WebKit queste proprietà si
    /// traducono in <c>gtk_widget_set_size_request()</c> e sono necessarie per far
    /// ridimensionare il widget nativo durante i resize della finestra.
    /// </summary>
    private void SynchronizeWebViewSize()
    {
        if (_browserWebView is null) return;

        var w = BrowserHost.Width;
        var h = BrowserHost.Height;

        if (w <= 0 || h <= 0)
        {
            _logger.LogTrace("SynchronizeWebViewSize: dimensioni BrowserHost non ancora disponibili ({W}x{H}), skip.", w, h);
            return;
        }

        _logger.LogTrace("SynchronizeWebViewSize: {W}x{H}", w, h);
        _browserWebView.WidthRequest  = w;
        _browserWebView.HeightRequest = h;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0)
            return;

        if (Math.Abs(Width - _lastResizeWidth) < 1 && Math.Abs(Height - _lastResizeHeight) < 1)
            return;

        _lastResizeWidth  = Width;
        _lastResizeHeight = Height;

        _logger.LogDebug("BrowserPage.SizeChanged: {Width}x{Height}", Width, Height);

        _resizeWorkaroundCts?.Cancel();
        _resizeWorkaroundCts = new CancellationTokenSource();
        _ = ApplyResizeWorkaroundAsync(_resizeWorkaroundCts.Token);
    }

    private async Task ApplyResizeWorkaroundAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(150, cancellationToken);

            await Dispatcher.DispatchAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (_browserWebView?.Source is not UrlWebViewSource currentSource ||
                    string.IsNullOrWhiteSpace(currentSource.Url))
                {
                    RootGrid.InvalidateMeasure();
                    BrowserHost.InvalidateMeasure();
                    _logger.LogTrace("BrowserPage.ApplyResizeWorkaround: nessuna Url corrente, solo invalidate layout.");
                    return;
                }

                var currentUrl = currentSource.Url;
                _logger.LogDebug(
                    "BrowserPage.ApplyResizeWorkaround: ricreo WebView. Url={Url}", currentUrl);

                var oldWebView = _browserWebView;

                _browserWebView = new WebView
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions   = LayoutOptions.Fill,
                    Source            = new UrlWebViewSource { Url = currentUrl },
                };
                _browserWebView.Navigating += WebView_Navigating;
                _browserWebView.Navigated  += WebView_Navigated;
                BrowserHost.Content = _browserWebView;

                if (oldWebView is not null)
                {
                    oldWebView.Navigating -= WebView_Navigating;
                    oldWebView.Navigated  -= WebView_Navigated;
                }

                // Forza le dimensioni esplicite sul widget GTK4 appena creato,
                // poi invalida il layout per il passo di arrange successivo.
                SynchronizeWebViewSize();
                RootGrid.InvalidateMeasure();
                BrowserHost.InvalidateMeasure();
            });
        }
        catch (OperationCanceledException)
        {
            // debounce cancellato da un resize successivo
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BrowserPage.ApplyResizeWorkaround: errore nel workaround resize.");
        }
    }

    // ── Gestione navigazione WebView ──────────────────────────────

    /// <summary>
    /// Permette tutte le navigazioni verso il server loopback del sito corrente;
    /// blocca qualsiasi altro URL (http/https esterno, schemi sconosciuti, ecc.).
    /// </summary>
    private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;
        _logger.LogTrace("BrowserPage.WebView_Navigating: url='{Url}'", url);

        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
        if (url.StartsWith("data:",  StringComparison.OrdinalIgnoreCase)) return;

        var pwsFileService = IPlatformApplication.Current!.Services.GetRequiredService<PwsFileService>();
        var server         = pwsFileService.CurrentServer;

        // Permetti TUTTA la navigazione verso il server loopback del sito corrente
        if (server is not null &&
            url.StartsWith(server.BaseAddress, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("BrowserPage.WebView_Navigating: loopback OK → '{Url}'", url);

            // Aggiorna subito la barra indirizzi
            if (BindingContext is BrowserViewModel vm)
                Dispatcher.Dispatch(() => vm.OnPageNavigating(url));

            return; // lascia navigare la WebView
        }

        // Tutto il resto viene bloccato (URL esterni, schemi non-http, ecc.)
        e.Cancel = true;
        _logger.LogDebug("BrowserPage.WebView_Navigating: bloccata → '{Url}'", url);
    }

    /// <summary>
    /// Aggiorna CanGoBack/CanGoForward nel ViewModel dopo il completamento della navigazione.
    /// </summary>
    private void WebView_Navigated(object? sender, WebNavigatedEventArgs e)
    {
        _logger.LogDebug(
            "BrowserPage.WebView_Navigated: url='{Url}' result={Result}", e.Url, e.Result);

        Dispatcher.Dispatch(() =>
        {
            if (BindingContext is BrowserViewModel vm)
                vm.OnWebViewNavigated(
                    e.Url,
                    _browserWebView?.CanGoBack    ?? false,
                    _browserWebView?.CanGoForward ?? false);
        });
    }

    // ── Pulsante "Apri file" ──────────────────────────────────────

    /// <summary>
    /// Sostituisce l'intera pagina radice con la <see cref="StartupPage"/> per consentire
    /// l'apertura di un nuovo archivio .pws.
    /// </summary>
    private void OnOpenFileClicked(object? sender, EventArgs e)
    {
        _logger.LogDebug("BrowserPage.OnOpenFileClicked: torno a StartupPage.");
        Application.Current!.Windows[0].Page = new NavigationPage(new StartupPage());
    }
}
