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
│   ├── IPwsArchivePicker.cs   ← astrazione chooser archivio
│   ├── GtkPwsArchivePicker.cs ← Gtk.FileDialog nativo Linux
│   └── PwsFileService.cs      ← mantiene il PwsContentProvider corrente
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

builder.Services.AddSingleton<InMemoryContentProvider>(_ =>
    new InMemoryContentProvider("pws"));

builder.Services.AddSingleton<IContentProvider>(sp =>
    new DynamicCompositeContentProvider(
        sp.GetRequiredService<InMemoryContentProvider>(),
        sp.GetRequiredService<PwsFileService>()));

builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddTransient<BrowserViewModel>();
```

Il provider composito include sempre `pws://` in-memory e, quando l'utente apre un archivio,
delegata anche al `PwsContentProvider` corrente esposto da `PwsFileService`.

## Logging — Serilog su file

Il progetto usa **Serilog** come backend concreto di `Microsoft.Extensions.Logging`.
Tutta la dipendenza da Serilog è confinata in `MauiProgram.cs`; il resto del codice
(inclusi `PWS.Core` e `PWS.Format`) usa solo `ILogger<T>` astratto.

```
ILogger<T>           ← usato da NavigationService, BrowserViewModel, PwsReader …
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
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PWS", "logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System",    LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.File(
        path:                   Path.Combine(logDir, "pws-.log"),
        rollingInterval:        RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate:         "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] " +
                                "{SourceContext} {Message:lj}{NewLine}{Exception}")
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
| `PwsReader` | Token unsigned con RequireSignedTokens | `Warning` |
| `PwsPacker` | Errore durante il packing di un sito | `Error` |
| `PwsPacker` | Inizio/fine packing | `Info` / `Debug` |
| `StartupPage` | pick file, apertura reader, verifica sito, apertura browser | `Debug` / `Info` / `Error` |
| `PwsFileService` | sostituzione provider corrente | `Debug` / `Info` |
| `DynamicCompositeContentProvider` | scelta provider (`PwsContentProvider` vs `InMemory`) | `Trace` / `Debug` |
| `NavigationService` | sequenza NavigateAsync / FetchAsync / eventi | `Trace` / `Debug` |
| `NavigationService` | Eccezione in FetchAsync | `Error` |
| `PwsContentProvider` | `CanHandle`, `GetAsync`, `Dispose` | `Trace` / `Debug` / `Warning` / `Error` |
| `BrowserPage` | lifecycle, attach BindingContext, update WebView | `Debug` / `Trace` |
| `BrowserViewModel` | richieste Navigate/Back/Forward/Refresh, lettura HTML | `Trace` / `Debug` / `Error` |

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
pwsFileService.SetProvider(provider);
await Navigation.PushAsync(new BrowserPage());
```

Il `PwsReader` resta aperto in memoria per tutta la sessione e i file vengono letti on-demand.

La `BrowserPage` si apre volutamente **senza navigare**: mostra toolbar + status bar +
un placeholder centrale. La `WebView` viene creata **lazy** solo quando arriva il primo
contenuto, così l'apertura della pagina resta il più neutra possibile su GTK4.

L'utente digita esplicitamente un URI del tipo `pws://<siteId>/index.html` nella barra indirizzi.

## BrowserViewModel

Il ViewModel **non dipende da MAUI Controls**: usa solo `ICommand` e `INotifyPropertyChanged`.

Proprietà esposte alla UI:

| Proprietà | Tipo | Descrizione |
|-----------|------|-------------|
| `AddressText` | `string` | URL nella barra degli indirizzi |
| `HtmlContent` | `string` | HTML della risposta corrente |
| `RenderedUrl` | `string` | URL loopback HTTP usato realmente dalla WebView |
| `PageTitle` | `string` | Titolo della pagina corrente |
| `StatusMessage` | `string` | Messaggio nella status bar |
| `CanGoBack` | `bool` | Abilita il pulsante Indietro |
| `CanGoForward` | `bool` | Abilita il pulsante Avanti |
| `IsBusy` | `bool` | True durante il caricamento |

## BrowserPage.xaml.cs — Code-Behind

Il code-behind è l'**unico** punto dove si tocca la `WebView` MAUI.

**Risoluzione ViewModel da DI (ritardata):**
```csharp
public BrowserPage()
{
    InitializeComponent();
}

protected override async void OnAppearing()
{
    base.OnAppearing();
    await Task.Delay(100); // lascia completare la widget-realization GTK4

    var vm = IPlatformApplication.Current!.Services
        .GetRequiredService<BrowserViewModel>();
    BindingContext = vm;
    vm.PropertyChanged += OnViewModelPropertyChanged;
}
```

Shell crea le pagine via reflection (non constructor-injection), quindi si usa
il service-locator pattern. Il binding viene assegnato in `OnAppearing()` per
evitare di toccare i widget GTK troppo presto.

**Aggiornamento WebView lazy:**
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

La `WebView` non è più istanziata direttamente in XAML. Al suo posto c'è un `ContentView`
placeholder (`BrowserHost`) che viene sostituito dalla `WebView` solo al primo contenuto.

Per siti statici complessi come Docusaurus, l'HTML inline non basta perché la pagina deve
caricare anche JS/CSS/immagini secondari. Per questo la app usa un piccolo server HTTP locale
(`LoopbackContentServer`) che serve i file del `.pws` dal `PwsContentProvider` corrente:

```text
pws://docs/index.html
   ↓ mappatura interna
http://127.0.0.1:<porta>/index.html
```

La `WebView` carica quindi `RenderedUrl` via loopback HTTP, e tutte le richieste successive
agli asset (`/assets/js/...`, `/assets/css/...`, ecc.) vengono servite dallo stesso bridge.

Su GTK4 il layout della `BrowserPage` deve rimanere **layout-driven**: la `WebView` usa solo
`HorizontalOptions/VerticalOptions = Fill` e non deve fissare `WidthRequest` / `HeightRequest`,
altrimenti i resize successivi della finestra possono restare "bloccati" sulla misura iniziale.

**Intercettazione link:**
```csharp
private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
{
    // Schemi custom → NavigationService (non la WebView)
    if (!e.Url.StartsWith("http") && !e.Url.StartsWith("about") && ...)
    {
        e.Cancel = true;
        vm.NavigateCommand.Execute(e.Url);
    }
}
```

