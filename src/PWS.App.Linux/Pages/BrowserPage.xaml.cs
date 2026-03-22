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
/// Gestisce la sincronizzazione tra BrowserViewModel e la WebView GTK4:
///  – aggiorna la Source della WebView quando HtmlContent cambia
///  – intercetta i click sui link e li reindirizza al NavigationService
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BrowserPage : ContentPage
{
    private bool _bindingDone;
    private WebView? _browserWebView;
    private readonly ILogger<BrowserPage> _logger;
    private bool _allowOneLoopbackNavigation;
    private CancellationTokenSource? _resizeWorkaroundCts;
    private double _lastResizeWidth;
    private double _lastResizeHeight;

    public BrowserPage()
    {
        // Costruttore minimo: solo InitializeComponent.
        // BindingContext e sottoscrizione agli eventi del VM vengono assegnati
        // in OnAppearing, dopo che GTK4 ha realizzato tutti i widget nativi.
        InitializeComponent();
        _logger = IPlatformApplication.Current!.Services.GetRequiredService<ILogger<BrowserPage>>();
        _logger.LogDebug("BrowserPage ctor: InitializeComponent completato.");

        SizeChanged += OnPageSizeChanged;
    }

    // ── Ciclo di vita ────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogDebug("BrowserPage.OnAppearing: bindingDone={Done}", _bindingDone);

        if (_bindingDone) return;
        _bindingDone = true;

        // GTK4 realizza i widget nativi (GtkEntry, WebKitWebView…) in modo
        // asincrono rispetto al ciclo di vita MAUI. Task.Delay(100) cede il
        // controllo al GLib main loop per almeno un frame GTK (~16 ms) più
        // un margine sufficiente a completare la widget-realization prima di
        // toccare qualsiasi widget nativo via binding.
        _logger.LogTrace("BrowserPage.OnAppearing: attendo 100ms prima di assegnare il BindingContext.");
        await Task.Delay(100);

        var vm = IPlatformApplication.Current!.Services.GetRequiredService<BrowserViewModel>();
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        _logger.LogDebug("BrowserPage.OnAppearing: BindingContext assegnato a BrowserViewModel.");
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
            _allowOneLoopbackNavigation = true;
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
            VerticalOptions = LayoutOptions.Fill,
        };
        _browserWebView.Navigating += WebView_Navigating;
        BrowserHost.Content = _browserWebView;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0)
            return;

        if (Math.Abs(Width - _lastResizeWidth) < 1 && Math.Abs(Height - _lastResizeHeight) < 1)
            return;

        _lastResizeWidth = Width;
        _lastResizeHeight = Height;

        _logger.LogDebug("BrowserPage.SizeChanged: {Width}x{Height}", Width, Height);

        // Workaround GTK4/WebKit: su alcune build il widget nativo non segue bene i
        // resize successivi della finestra. Debounce e ricreazione della WebView
        // preservando l'URL corrente.
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
                _logger.LogDebug("BrowserPage.ApplyResizeWorkaround: ricreo WebView per adattarla al resize. Url={Url}", currentUrl);

                var oldWebView = _browserWebView;

                _browserWebView = new WebView
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Source = new UrlWebViewSource { Url = currentUrl },
                };

                _allowOneLoopbackNavigation = true;
                _browserWebView.Navigating += WebView_Navigating;
                BrowserHost.Content = _browserWebView;

                if (oldWebView is not null)
                    oldWebView.Navigating -= WebView_Navigating;

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

    // ── Intercettazione navigazione nella WebView ─────────────────

    private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;
        _logger.LogTrace("BrowserPage.WebView_Navigating: url='{Url}'", url);

        var loopbackServer = IPlatformApplication.Current!.Services.GetRequiredService<LoopbackContentServer>();

        // Permette una singola navigazione loopback quando la Source viene impostata dal codice.
        if (_allowOneLoopbackNavigation && url.StartsWith(loopbackServer.BaseAddress, StringComparison.OrdinalIgnoreCase))
        {
            _allowOneLoopbackNavigation = false;
            _logger.LogDebug("BrowserPage.WebView_Navigating: permetto la navigazione loopback iniziale '{Url}'.", url);
            return;
        }

        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
        if (url.StartsWith("data:",  StringComparison.OrdinalIgnoreCase)) return;

        if (loopbackServer.TryMapLoopbackUrlToPwsUri(url, out var pwsUri))
        {
            e.Cancel = true;
            _logger.LogDebug("BrowserPage.WebView_Navigating: loopback '{Url}' -> '{PwsUri}'", url, pwsUri);

            if (BindingContext is BrowserViewModel loopbackVm)
                loopbackVm.NavigateCommand.Execute(pwsUri);

            return;
        }

        if (url.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return;

        // Qualsiasi schema non-web (pws://, api://, ecc.) → NavigationService
        e.Cancel = true;
        _logger.LogDebug("BrowserPage.WebView_Navigating: cancello navigazione WebView e delego al VM per '{Url}'.", url);

        if (BindingContext is BrowserViewModel vm)
            vm.NavigateCommand.Execute(url);
    }
}
