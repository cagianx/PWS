using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
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
    private bool _initialLoadDone;

    public BrowserPage()
    {
        InitializeComponent();

        // Risolve il ViewModel dal container DI senza richiedere
        // la constructor-injection (non supportata via ContentTemplate in Shell)
        var vm = IPlatformApplication.Current!.Services.GetRequiredService<BrowserViewModel>();
        BindingContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    // ── Ciclo di vita ────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Naviga alla home solo al primo avvio
        if (!_initialLoadDone)
        {
            _initialLoadDone = true;
            if (BindingContext is BrowserViewModel vm)
                await vm.InitializeAsync();
        }
    }

    // ── Sincronizzazione ViewModel → WebView ─────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BrowserViewModel.HtmlContent)) return;
        if (sender is not BrowserViewModel vm) return;

        // Forza l'aggiornamento sul thread UI
        MainThread.BeginInvokeOnMainThread(() =>
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

        // Qualsiasi altro schema (pws://, api://, ecc.) viene gestito
        // dal NavigationService anziché direttamente dalla WebView
        e.Cancel = true;

        if (BindingContext is BrowserViewModel vm)
            vm.NavigateCommand.Execute(url);
    }
}

