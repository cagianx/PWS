using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
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
    private bool    _initialLoadDone;
    private string? _initialUri;   // impostato da StartupPage, consumato in OnAppearing

    /// <param name="initialUri">
    /// URI da aprire subito dopo che la pagina è montata nel widget tree GTK.
    /// Se <see langword="null"/> viene usata la home <c>pws://home</c>.
    /// </param>
    public BrowserPage(string? initialUri = null)
    {
        InitializeComponent();
        _initialUri = initialUri;

        var vm = IPlatformApplication.Current!.Services.GetRequiredService<BrowserViewModel>();
        BindingContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    // ── Ciclo di vita ────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_initialLoadDone) return;
        _initialLoadDone = true;

        if (BindingContext is not BrowserViewModel vm) return;

        try
        {
            if (_initialUri is not null)
            {
                // Naviga all'URI fornito da StartupPage.
                // A questo punto la pagina è nel widget tree GTK → la WebView è pronta.
                var uri = _initialUri;
                _initialUri = null;
                await vm.NavigateToUri(uri);
            }
            else if (string.IsNullOrWhiteSpace(vm.HtmlContent)
                     && string.Equals(vm.AddressText, "pws://home",
                                      StringComparison.OrdinalIgnoreCase))
            {
                await vm.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            var svc = IPlatformApplication.Current?.Services.GetService<ErrorDialogService>();
            if (svc is not null)
                await svc.ShowAsync(ex, "Inizializzazione browser");
        }
    }

    // ── Sincronizzazione ViewModel → WebView ─────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BrowserViewModel.HtmlContent)) return;
        if (sender is not BrowserViewModel vm) return;

        // Usa il Dispatcher della pagina — MainThread.BeginInvokeOnMainThread
        // non è implementato da Platform.Maui.Linux.Gtk4.Essentials
        Dispatcher.Dispatch(() =>
        {
            BrowserWebView.Source = new HtmlWebViewSource { Html = vm.HtmlContent };
        });
    }

    // ── Intercettazione navigazione nella WebView ─────────────────

    private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;

        // Lascia passare le navigazioni interne di WebKit
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;

        // HTTP/HTTPS: al momento li lasciamo passare (in futuro → ApiContentProvider)
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return;

        // Qualsiasi altro schema (pws://, pack://, api://, ecc.) viene gestito
        // dal NavigationService anziché direttamente dalla WebView
        e.Cancel = true;

        if (BindingContext is BrowserViewModel vm)
            vm.NavigateCommand.Execute(url);
    }
}

