# GitHub Copilot – Istruzioni di progetto: PWS_MAUI

## Panoramica

**PWS** è un browser .NET MAUI che gira nativamente su **Linux/GTK4** tramite il pacchetto
[`Platform.Maui.Linux.Gtk4`](https://github.com/Redth/Maui.Gtk) (v0.6.0).
Il punto chiave del progetto è che la WebView **non carica mai file dal filesystem**:
il contenuto (HTML, dati, ecc.) viene sempre fornito da un'astrazione chiamata
`IContentProvider`, che può essere implementata in qualunque modo (in-memory, API REST,
database, ecc.).

---

## Struttura della soluzione

```
PWS.slnx
├── src/
│   ├── PWS.Core/           ← libreria portable (net10.0), ZERO dipendenze MAUI
│   │   ├── Abstractions/   ← IContentProvider, INavigationService
│   │   ├── Models/         ← ContentRequest, ContentResponse, NavigationEntry
│   │   ├── Navigation/     ← NavigationHistory, NavigationService
│   │   └── Providers/      ← InMemoryContentProvider, ApiContentProvider,
│   │                           CompositeContentProvider
│   └── PWS.App/            ← app MAUI GTK4 (net10.0)
│       ├── Program.cs      ← entry point (GtkMauiApplication)
│       ├── MauiProgram.cs  ← DI builder (UseMauiAppLinuxGtk4<App>)
│       ├── App.xaml/.cs    ← Application root, imposta MainPage = new AppShell()
│       ├── AppShell.xaml   ← Shell con route "browser" → BrowserPage
│       ├── Pages/
│       │   ├── BrowserPage.xaml      ← UI: toolbar + WebView + status bar
│       │   └── BrowserPage.xaml.cs   ← code-behind: VM da DI, sync WebView
│       ├── ViewModels/
│       │   ├── BaseViewModel.cs      ← INotifyPropertyChanged helper
│       │   └── BrowserViewModel.cs   ← comandi nav, AddressText, HtmlContent
│       └── Resources/Styles/
│           ├── Colors.xaml
│           └── Styles.xaml
└── docs/                   ← documentazione Docusaurus (TypeScript)
```

---

## Regole di architettura

### PWS.Core
- **Nessuna dipendenza MAUI**: solo BCL + `Microsoft.Extensions.*`.
- `IContentProvider.CanHandle(Uri)` + `GetAsync(ContentRequest)` → `ContentResponse`.
- `ContentResponse` usa uno `Stream` per il contenuto (non una stringa), ed è `IDisposable`.
- `NavigationService` coordina `IContentProvider` e `NavigationHistory`;
  **non** sa nulla della UI.

### PWS.App
- `BrowserViewModel` non dipende da MAUI Controls: usa solo `ICommand` e `INotifyPropertyChanged`.
- `BrowserPage.xaml.cs` è l'unico punto in cui si tocca la `WebView` MAUI.
- Il ViewModel risolto tramite `IPlatformApplication.Current!.Services.GetRequiredService<T>()`:
  MAUI crea le pagine via Shell `ContentTemplate` (reflection, non DI-injection),
  quindi si usa il service-locator pattern nel costruttore della pagina.
- Il flusso di navigazione per link custom (`pws://`, `api://`, ecc.):
  `WebView.Navigating` → `e.Cancel = true` → `BrowserViewModel.NavigateCommand`.

---

## Come buildare

```bash
# .NET / C#  (senza il workload MAUI tradizionale, che è rotto su net10.0)
MSBuildEnableWorkloadResolver=false dotnet build src/PWS.App/PWS.App.csproj

# Docusaurus (sviluppo)
cd docs && npm run start

# Docusaurus (produzione)
cd docs && npm run build
```

> **Variabile d'ambiente consigliata** nel proprio shell profile:
> `export MSBuildEnableWorkloadResolver=false`

---

## Dipendenze chiave

| Package | Versione | Scopo |
|---------|----------|-------|
| `Platform.Maui.Linux.Gtk4` | 0.6.0 | Backend GTK4 per MAUI su Linux |
| `Platform.Maui.Linux.Gtk4.Essentials` | 0.6.0 | MAUI Essentials per Linux |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.* | DI in PWS.Core |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.* | Logging in PWS.Core |

### Prerequisiti di sistema (Linux)
```bash
# Debian/Ubuntu
sudo apt install libgtk-4-dev libwebkitgtk-6.0-dev
# Fedora
sudo dnf install gtk4-devel webkitgtk6.0-devel
```

---

## Convenzioni di codice

- **C#**: `latest` LangVersion, `nullable enable`, `implicit usings enable`.
- Classi `sealed` di default dove non serve ereditarietà.
- I provider implementano `IContentProvider`; registrarli nel `CompositeContentProvider`
  in `MauiProgram.cs`.
- **Non** usare `Update` negli item `MauiXaml` del csproj quando
  `EnableDefaultXamlItems=false` — usare sempre `Include`.
- Docusaurus: TypeScript, tema `classic`.

---

## Roadmap / TODO

- [ ] Aggiungere `ApiContentProvider` al `CompositeContentProvider` in `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` attraverso `ApiContentProvider` nella `WebView`
- [ ] Aggiungere la barra di progresso durante il caricamento
- [ ] Completare la documentazione in `/docs`
- [ ] Test unitari per `PWS.Core` (xUnit)

