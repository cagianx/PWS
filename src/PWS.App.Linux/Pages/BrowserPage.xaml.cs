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

    public BrowserPage()
    {
        // Costruttore minimo: solo InitializeComponent.
        // BindingContext e sottoscrizione agli eventi del VM vengono assegnati
        // in OnAppearing, dopo che GTK4 ha realizzato tutti i widget nativi.
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
        if (e.PropertyName != nameof(BrowserViewModel.HtmlContent)) return;
        if (sender is not BrowserViewModel vm) return;

        Dispatcher.Dispatch(() =>
        {
            EnsureWebView();
            _logger.LogDebug("BrowserPage: aggiorno WebView.Source con HtmlContent di {Len} caratteri.", vm.HtmlContent.Length);
            _browserWebView!.Source = new HtmlWebViewSource { Html = vm.HtmlContent };
        });
    }

    private void EnsureWebView()
    {
        if (_browserWebView is not null)
            return;

        _logger.LogDebug("BrowserPage.EnsureWebView: creo WebView lazy.");

        _browserWebView = new WebView();
        _browserWebView.Navigating += WebView_Navigating;
        BrowserHost.Content = _browserWebView;
    }

    // ── Intercettazione navigazione nella WebView ─────────────────

    private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;
        _logger.LogTrace("BrowserPage.WebView_Navigating: url='{Url}'", url);

        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
        if (url.StartsWith("data:",  StringComparison.OrdinalIgnoreCase)) return;

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
