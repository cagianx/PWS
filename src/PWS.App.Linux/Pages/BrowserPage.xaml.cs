using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using Platform.Maui.Linux.Gtk4.Platform;
using PWS.App.Linux.Services;
using PWS.App.Linux.ViewModels;

namespace PWS.App.Linux.Pages;

/// <summary>
/// Code-behind di BrowserPage.
/// La navigazione avviene interamente su HTTP loopback: tutti gli URL del sito corrente
/// vengono serviti dal <see cref="LoopbackContentServer"/> dedicato.
/// Il codice-behind è il solo punto in cui si tocca la WebView MAUI.
/// </summary>
/// <remarks>
/// <para><b>Bug GTK4 resize (Platform.Maui.Linux.Gtk4 ≤ 0.6.0):</b></para>
/// <para>
/// <c>LayoutHandler.ConnectHandler</c> aggancia una lambda anonima a
/// <c>GtkWindow.OnNotify</c> per gestire il resize, ma presenta due difetti:
/// </para>
/// <list type="number">
///   <item>La lambda non viene mai de-registrata in <c>DisconnectHandler</c>.
///   Se la pagina viene distrutta, <c>VirtualView</c> diventa null → la lambda
///   lancia <c>InvalidOperationException</c>, abortendo il dispatch del segnale
///   e impedendo il resize delle pagine successive.</item>
///   <item>Nella lambda, <c>window.GetAllocatedWidth/Height()</c> restituisce
///   la dimensione <b>vecchia</b> perché al momento del <c>notify::default-width</c>
///   la nuova allocazione GTK4 non è ancora avvenuta. <c>DoLayout()</c> quindi
///   ricalcola il layout con le vecchie dimensioni → nessun cambiamento visivo.</item>
/// </list>
/// <para><b>Workaround:</b></para>
/// <list type="bullet">
///   <item>Bug 1: non distruggere mai le pagine (solo <c>PushAsync</c>/<c>PopAsync</c>).</item>
///   <item>Bug 2: agganciare direttamente <c>GtkWindow.OnNotify</c>, leggere la
///   dimensione corretta da <c>GetDefaultSize()</c>, e chiamare
///   <c>CrossPlatformMeasure</c>/<c>CrossPlatformArrange</c> sulla
///   <see cref="GtkLayoutPanel"/> del Grid.</item>
/// </list>
/// </remarks>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BrowserPage : ContentPage
{
    private bool _bindingDone;
    private bool _resizeHooked;
    private WebView? _browserWebView;
    private readonly ILogger<BrowserPage> _logger;
    private Gtk.Window? _gtkWindow;

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

        // Installa il workaround resize dopo che il widget tree è stabile
        InstallResizeWorkaround();

        vm.NavigateToCurrentSite();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_gtkWindow is not null)
        {
            _gtkWindow.OnNotify -= OnGtkWindowNotify;
            _gtkWindow = null;
            _resizeHooked = false;
            _logger.LogDebug("BrowserPage.OnDisappearing: sganciato GtkWindow.OnNotify.");
        }
    }

    // ── Workaround resize GTK4 ───────────────────────────────────

    /// <summary>
    /// Aggancia il workaround per il bug resize del <c>LayoutHandler</c>.
    /// Cerca la <see cref="Gtk.Window"/> risalendo il widget tree nativo
    /// dal <see cref="RootGrid"/> e si iscrive a <c>OnNotify</c>.
    /// </summary>
    private void InstallResizeWorkaround()
    {
        if (_resizeHooked) return;

        // Risali il widget tree nativo per trovare la GtkWindow
        if (RootGrid.Handler?.PlatformView is not Gtk.Widget nativeGrid)
        {
            _logger.LogWarning("BrowserPage: RootGrid non ha un PlatformView nativo, resize workaround non installato.");
            return;
        }

        Gtk.Widget? cur = nativeGrid;
        while (cur is not null && cur is not Gtk.Window)
            cur = cur.GetParent();

        if (cur is not Gtk.Window window)
        {
            _logger.LogWarning("BrowserPage: GtkWindow non trovata risalendo il widget tree, resize workaround non installato.");
            return;
        }

        _gtkWindow = window;
        _gtkWindow.OnNotify += OnGtkWindowNotify;
        _resizeHooked = true;
        _logger.LogDebug("BrowserPage: resize workaround installato (GtkWindow trovata via widget tree).");
    }

    /// <summary>
    /// Intercetta <c>notify::default-width</c> / <c>notify::default-height</c> sulla
    /// <see cref="Gtk.Window"/> e forza il re-layout della <see cref="GtkLayoutPanel"/>
    /// del Grid con le dimensioni corrette da <c>GetDefaultSize()</c>.
    /// </summary>
    /// <remarks>
    /// Il <c>LayoutHandler</c> del backend usa <c>GetAllocatedWidth/Height()</c> che a
    /// questo punto restituiscono ancora i valori VECCHI (la nuova allocazione GTK4 non
    /// è ancora avvenuta). Noi usiamo <c>GetDefaultSize()</c> che ha già il valore nuovo.
    /// </remarks>
    private void OnGtkWindowNotify(GObject.Object sender, GObject.Object.NotifySignalArgs args)
    {
        var prop = args.Pspec.GetName();
        if (prop is not ("default-width" or "default-height"))
            return;

        if (_gtkWindow is null) return;

        // GetDefaultSize() ha già il valore NUOVO al momento del notify,
        // a differenza di GetAllocatedWidth/Height() che è ancora VECCHIO.
        _gtkWindow.GetDefaultSize(out var w, out var h);
        if (w < 1 || h < 1) return;

        _logger.LogDebug("BrowserPage.OnGtkWindowNotify: resize {W}x{H} (da GetDefaultSize)", w, h);

        // Accede direttamente alla GtkLayoutPanel del Grid e forza
        // CrossPlatformMeasure/Arrange con le dimensioni corrette,
        // bypassando il LayoutHandler che usa valori stale.
        if (RootGrid.Handler?.PlatformView is GtkLayoutPanel layoutPanel)
        {
            (RootGrid as VisualElement)?.InvalidateMeasure();
            layoutPanel.CrossPlatformMeasure(w, h);
            layoutPanel.CrossPlatformArrange(new Rect(0, 0, w, h));
        }
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
    /// Torna alla <see cref="StartupPage"/> tramite <c>PopAsync</c>.
    /// NON usare <c>RemovePage</c>: vedi remarks della classe per il bug GTK4.
    /// </summary>
    private async void OnOpenFileClicked(object? sender, EventArgs e)
    {
        _logger.LogDebug("BrowserPage.OnOpenFileClicked: torno a StartupPage.");
        await Navigation.PopAsync(animated: false);
    }
}
