---
sidebar_position: 3
---

# PWS.App

`PWS.App` è l'applicazione MAUI GTK4. È il layer di presentazione: conosce MAUI e GTK4,
ma delega tutta la logica a `PWS.Core` tramite interfacce.

## Struttura

```
PWS.App/
├── Program.cs          ← entry point GtkMauiApplication
├── MauiProgram.cs      ← DI builder
├── App.xaml/.cs        ← Application root
├── AppShell.xaml/.cs   ← Shell con route "browser"
├── Pages/
│   ├── BrowserPage.xaml      ← UI: toolbar + WebView + status bar
│   └── BrowserPage.xaml.cs   ← code-behind
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
builder.Services.AddSingleton<InMemoryContentProvider>(_ =>
    new InMemoryContentProvider("pws"));

builder.Services.AddSingleton<IContentProvider>(sp =>
    new CompositeContentProvider([
        sp.GetRequiredService<InMemoryContentProvider>()
    ]));

builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddTransient<BrowserViewModel>();
```

Per aggiungere un nuovo provider, registrarlo e aggiungerlo al `CompositeContentProvider`.

## BrowserViewModel

Il ViewModel **non dipende da MAUI Controls**: usa solo `ICommand` e `INotifyPropertyChanged`.

Proprietà esposte alla UI:

| Proprietà | Tipo | Descrizione |
|-----------|------|-------------|
| `AddressText` | `string` | URL nella barra degli indirizzi |
| `HtmlContent` | `string` | HTML da mostrare nella WebView |
| `PageTitle` | `string` | Titolo della pagina corrente |
| `StatusMessage` | `string` | Messaggio nella status bar |
| `CanGoBack` | `bool` | Abilita il pulsante Indietro |
| `CanGoForward` | `bool` | Abilita il pulsante Avanti |
| `IsBusy` | `bool` | True durante il caricamento |

## BrowserPage.xaml.cs — Code-Behind

Il code-behind è l'**unico** punto dove si tocca la `WebView` MAUI.

**Risoluzione ViewModel da DI:**
```csharp
public BrowserPage()
{
    InitializeComponent();
    var vm = IPlatformApplication.Current!.Services
        .GetRequiredService<BrowserViewModel>();
    BindingContext = vm;
    vm.PropertyChanged += OnViewModelPropertyChanged;
}
```

Shell crea le pagine via reflection (non constructor-injection), quindi si usa
il service-locator pattern.

**Aggiornamento WebView:**
```csharp
private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(BrowserViewModel.HtmlContent))
        MainThread.BeginInvokeOnMainThread(() =>
            BrowserWebView.Source = new HtmlWebViewSource { Html = vm.HtmlContent });
}
```

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

