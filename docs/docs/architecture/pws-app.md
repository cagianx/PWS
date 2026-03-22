---
sidebar_position: 3
---

# PWS.App.Linux

`PWS.App.Linux` è l'applicazione MAUI GTK4 dedicata a Linux. È il layer di presentazione: conosce MAUI e GTK4,
ma delega tutta la logica a `PWS.Core` tramite interfacce.

:::info Progetto separato
Il progetto usa [`Platform.Maui.Linux.Gtk4`](https://github.com/Redth/Maui.Gtk) che porta dipendenze
native GTK4 specifiche per Linux. Per questo motivo è mantenuto come **progetto separato** rispetto
a un'eventuale app MAUI multi-piattaforma, evitando che le dipendenze native inquinino la build su
altri OS.
:::

## Struttura

```
PWS.App.Linux/
├── Program.cs          ← entry point GtkMauiApplication
├── MauiProgram.cs      ← DI builder
├── App.xaml/.cs        ← Application root
├── Pages/
│   ├── StartupPage.xaml      ← chooser GTK nativo per aprire file .pws
│   ├── BrowserPage.xaml      ← UI: toolbar + WebView + status bar
│   └── BrowserPage.xaml.cs   ← code-behind
├── Services/
│   ├── IPwsArchivePicker.cs    ← astrazione chooser archivio
│   ├── GtkPwsArchivePicker.cs  ← Gtk.FileDialog nativo Linux
│   ├── PwsFileService.cs       ← mantiene provider + LoopbackContentServer correnti
│   └── LoopbackContentServer.cs← server HTTP su loopback dedicato per sito
└── ViewModels/
    ├── BaseViewModel.cs       ← INotifyPropertyChanged helper
    └── BrowserViewModel.cs    ← stato e comandi del browser
```

## Program.cs — Entry Point

```csharp
public class Program : GtkMauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public static void Main(string[] args)
    {
        var app = new Program();
        app.Run(args);
    }
}
```

## MauiProgram.cs — Dependency Injection

```csharp
builder.Services.AddSingleton<PwsFileService>();
builder.Services.AddSingleton<IPwsArchivePicker, GtkPwsArchivePicker>();
builder.Services.AddTransient<BrowserViewModel>();
```

Il `LoopbackContentServer` **non è più un singleton**: viene creato da `PwsFileService.SetProvider()`
ogni volta che viene aperto un nuovo archivio `.pws`, su una porta TCP casuale dedicata al sito.

## Logging — Serilog su file

Il progetto usa **Serilog** come backend concreto di `Microsoft.Extensions.Logging`.
Tutta la dipendenza da Serilog è confinata in `MauiProgram.cs`; il resto del codice
(inclusi `PWS.Core` e `PWS.Format`) usa solo `ILogger<T>` astratto.

```
ILogger<T>           ← usato da BrowserViewModel, PwsReader, PwsFileService …
    │
    │  Microsoft.Extensions.Logging (astrazione)
    │
    └─► Serilog.Extensions.Logging ─► Serilog ─► Serilog.Sinks.File
                                                       │
                                              ~/.local/share/PWS/logs/
                                              pws-20260322.log
```

### Configurazione (MauiProgram.cs)

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System",    LogEventLevel.Warning)
    .WriteTo.File(
        path:                   Path.Combine(logDir, "pws-.log"),
        rollingInterval:        RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Logging.ClearProviders().AddSerilog(Log.Logger, dispose: true);
```

### File di log

| Percorso | `~/.local/share/PWS/logs/pws-YYYYMMDD.log` |
|----------|--------------------------------------------|
| Rotazione | Giornaliera |
| Retention | 7 file |
| Livello minimo | `Verbose` (ridotto a `Warning` per namespace Microsoft/System) |

### Cosa viene loggato

| Componente | Evento | Livello |
|------------|--------|---------|
| `PwsReader` | JWT verification failed | `Error` |
| `PwsReader` | Content hash mismatch (file alterati) | `Error` |
| `PwsPacker` | Errore durante il packing di un sito | `Error` |
| `StartupPage` | pick file, apertura reader, verifica sito | `Debug` / `Info` / `Error` |
| `PwsFileService` | sostituzione provider, avvio server loopback | `Debug` / `Info` |
| `LoopbackContentServer` | avvio, richieste HTTP, risposta | `Info` / `Trace` / `Debug` |
| `BrowserPage` | lifecycle, attach BindingContext, update WebView | `Debug` / `Trace` |
| `BrowserViewModel` | Navigate, NavigateToCurrentSite, OnWebViewNavigated | `Trace` / `Debug` |

## StartupPage — Apertura archivio .pws

All'avvio l'app mostra una `StartupPage` con un pulsante **Apri file .pws**.

Su Linux/GTK il progetto usa un servizio dedicato `IPwsArchivePicker` implementato da
`GtkPwsArchivePicker`, che apre un dialog nativo `Gtk.FileDialog`.

> Non si usa `Microsoft.Maui.Storage.FilePicker` perché sul backend Linux/GTK può ricadere
> nella versione portable dell'assembly e lanciare `NotImplementedInReferenceAssemblyException`.

Flusso:

```csharp
var path = await archivePicker.PickAsync();
var reader = await PwsReader.OpenAsync(path, new PwsOpenOptions { Logger = logger });
var provider = new PwsContentProvider(reader, defaultSiteId, loggerFactory.CreateLogger<PwsContentProvider>());

// SetProvider() crea automaticamente un LoopbackContentServer su una porta libera
pwsFileService.SetProvider(provider);

await Navigation.PushAsync(new BrowserPage());
// BrowserPage.OnAppearing chiama automaticamente vm.NavigateToCurrentSite()
```

La `StartupPage` **resta nello stack**: non viene rimossa con `Navigation.RemovePage(...)`.
Questo è un workaround per un bug di `Platform.Maui.Linux.Gtk4` v0.6.0: il `LayoutHandler`
aggancia callback di resize alla `GtkWindow` senza de-registrarle correttamente, quindi
distruggere una pagina può lasciare handler stantii che interferiscono con i resize successivi.
Per questo motivo il flusso usa solo `PushAsync()` / `PopAsync()`.

Il `PwsReader` resta aperto in memoria per tutta la sessione e i file vengono letti on-demand.

## Server HTTP Loopback per sito

Ogni archivio `.pws` aperto ottiene un **server HTTP dedicato** (`LoopbackContentServer`)
in ascolto su `http://127.0.0.1:{portaCasuale}/`.

```text
Apertura docs.pws
   ↓
PwsFileService.SetProvider(provider)
   ↓
new LoopbackContentServer(provider, "docs", logger)
   → avvio su http://127.0.0.1:49152/

BrowserPage.OnAppearing
   ↓
vm.NavigateToCurrentSite()
   ↓
WebView.Source = "http://127.0.0.1:49152/"
```

Tutte le richieste HTTP verso quel server vengono soddisfatte leggendo il file
corrispondente dal `PwsContentProvider` tramite l'URI interno `pws://{siteId}/{path}`.
La barra indirizzi dell'app mostra sempre l'URL HTTP loopback.

### Vantaggi rispetto allo schema `pws://`

| | `pws://` (precedente) | `http://loopback` (attuale) |
|---|---|---|
| Asset secondari (JS/CSS/img) | Richiedevano mapping esplicito | Serviti nativamente dalla WebView |
| Back/Forward | Gestiti da `NavigationService` custom | Gestiti dalla WebView nativa (WebKit) |
| Barra indirizzi | `pws://siteId/path` | `http://127.0.0.1:{port}/path` |
| Schema link nei documenti | Solo `pws://` | URL relativi standard (come su un web server reale) |

## BrowserViewModel

Il ViewModel **non dipende da MAUI Controls**: usa solo `ICommand` e `INotifyPropertyChanged`.
Non usa più `INavigationService`: la navigazione è gestita dalla WebView nativa tramite
i server HTTP loopback.

Proprietà esposte alla UI:

| Proprietà | Tipo | Descrizione |
|-----------|------|-------------|
| `AddressText` | `string` | URL nella barra degli indirizzi (`http://127.0.0.1:{port}/...`) |
| `RenderedUrl` | `string` | URL da caricare nella WebView (cambia per navigazione programmatica) |
| `PageTitle` | `string` | Titolo della pagina corrente |
| `StatusMessage` | `string` | Messaggio nella status bar |
| `CanGoBack` | `bool` | Abilita il pulsante Indietro |
| `CanGoForward` | `bool` | Abilita il pulsante Avanti |
| `IsBusy` | `bool` | True durante il caricamento |

Il ViewModel espone anche tre **eventi** che il code-behind usa per comandare la WebView:

```csharp
public event EventHandler? GoBackRequested;
public event EventHandler? GoForwardRequested;
public event EventHandler? ReloadRequested;
```

### Metodi pubblici chiamati da BrowserPage

```csharp
// Chiamato in OnAppearing: naviga al sito correntemente aperto
vm.NavigateToCurrentSite();

// Chiamati dalla WebView per aggiornare lo stato nel VM
vm.OnPageNavigating(url);             // WebView.Navigating
vm.OnWebViewNavigated(url, back, fwd);// WebView.Navigated
```

## BrowserPage.xaml.cs — Code-Behind

Il code-behind è l'**unico** punto dove si tocca la `WebView` MAUI.

**Risoluzione ViewModel da DI (ritardata):**
```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();
    await Task.Delay(100); // lascia completare la widget-realization GTK4

    var vm = IPlatformApplication.Current!.Services
        .GetRequiredService<BrowserViewModel>();
    BindingContext = vm;
    vm.PropertyChanged += OnViewModelPropertyChanged;

    // Collega gli eventi VM → operazioni native della WebView
    vm.GoBackRequested    += (_, _) => _browserWebView?.GoBack();
    vm.GoForwardRequested += (_, _) => _browserWebView?.GoForward();
    vm.ReloadRequested    += (_, _) => _browserWebView?.Reload();

    vm.NavigateToCurrentSite(); // avvia la navigazione al sito aperto
}
```

**Aggiornamento WebView lazy (navigazione programmatica):**

La `WebView` viene creata lazy al primo contenuto. Quando `RenderedUrl` cambia nel VM,
il code-behind imposta `webView.Source`:

```csharp
private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(BrowserViewModel.RenderedUrl))
        Dispatcher.Dispatch(() =>
        {
            EnsureWebView();
            _browserWebView!.Source = new UrlWebViewSource { Url = vm.RenderedUrl };
        });
}
```

**Intercettazione navigazione (`WebView.Navigating`):**

```csharp
private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
{
    // about: e data: sempre consentiti
    if (url.StartsWith("about:") || url.StartsWith("data:")) return;

    var server = pwsFileService.CurrentServer;

    // Permetti TUTTO il loopback del sito corrente → WebKit gestisce storia nativa
    if (server != null && url.StartsWith(server.BaseAddress)) return;

    // Blocca tutto il resto (URL esterni, schemi sconosciuti)
    e.Cancel = true;
}
```

La WebView naviga liberamente tra gli URL del server loopback corrente;
i link verso altri domini vengono bloccati.

**Aggiornamento stato dopo navigazione (`WebView.Navigated`):**

```csharp
private void WebView_Navigated(object? sender, WebNavigatedEventArgs e)
{
    Dispatcher.Dispatch(() =>
        vm.OnWebViewNavigated(e.Url,
            _browserWebView?.CanGoBack ?? false,
            _browserWebView?.CanGoForward ?? false));
}
```

**Resize GTK4 — stato attuale:**

Il problema di resize non è nella `WebView` in sé ma nel backend `Platform.Maui.Linux.Gtk4`
v0.6.0, in particolare in `LayoutHandler`:

- il listener anonimo agganciato a `GtkWindow.OnNotify` non viene rimosso in `DisconnectHandler`;
- durante `notify::default-width` / `notify::default-height` il backend usa
  `GetAllocatedWidth()` / `GetAllocatedHeight()`, che possono ancora riflettere la dimensione
  precedente al momento del callback.

Nel codice applicativo il workaround adottato è conservativo:

- non si usa più la ricreazione della `WebView` al resize;
- non si distruggono le pagine durante la navigazione (`RemovePage`), per evitare handler GTK4
  stantii con `VirtualView = null`;
- il passaggio `StartupPage` ⇄ `BrowserPage` usa solo `PushAsync()` / `PopAsync()`.
