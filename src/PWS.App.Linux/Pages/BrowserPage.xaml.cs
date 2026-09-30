using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using PWS.App.Linux.Services;
using PWS.Core.Hosting;
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

    public BrowserPage()
    {
        InitializeComponent();
        _logger = IPlatformApplication.Current!.Services.GetRequiredService<ILogger<BrowserPage>>();
        _logger.LogDebug("BrowserPage ctor: InitializeComponent completato.");
    }

    // ── Ciclo di vita ────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogDebug("BrowserPage.OnAppearing: bindingDone={Done}", _bindingDone);

        if (_bindingDone) return;
        _bindingDone = true;

        // GTK4 realizza i widget nativi in modo asincrono rispetto al ciclo di vita MAUI.
        await Task.Delay(100);

        var vm = IPlatformApplication.Current!.Services.GetRequiredService<BrowserViewModel>();
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        vm.GoBackRequested    += (_, _) => _browserWebView?.GoBack();
        vm.GoForwardRequested += (_, _) => _browserWebView?.GoForward();
        vm.ReloadRequested    += (_, _) => _browserWebView?.Reload();

        _logger.LogDebug("BrowserPage.OnAppearing: BindingContext assegnato a BrowserViewModel.");

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
    }

    // ── Gestione navigazione WebView ──────────────────────────────

    private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;
        _logger.LogTrace("BrowserPage.WebView_Navigating: url='{Url}'", url);

        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
        if (url.StartsWith("data:",  StringComparison.OrdinalIgnoreCase)) return;

        var pwsFileService = IPlatformApplication.Current!.Services.GetRequiredService<PwsFileService>();
        var server         = pwsFileService.CurrentServer;

        if (server is not null &&
            url.StartsWith(server.BaseAddress, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("BrowserPage.WebView_Navigating: loopback OK → '{Url}'", url);
            if (BindingContext is BrowserViewModel vm)
                Dispatcher.Dispatch(() => vm.OnPageNavigating(url));
            return;
        }

        e.Cancel = true;
        _logger.LogDebug("BrowserPage.WebView_Navigating: bloccata → '{Url}'", url);
    }

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
    /// Torna alla <see cref="StartupPage"/>, che resta alla radice dello stack, tramite <c>PopAsync</c>.
    /// </summary>
    private async void OnOpenFileClicked(object? sender, EventArgs e)
    {
        _logger.LogDebug("BrowserPage.OnOpenFileClicked: torno a StartupPage.");
        await Navigation.PopAsync(animated: false);
    }
}
