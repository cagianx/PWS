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
│   └── PWS.App.Linux/      ← app MAUI GTK4 (Linux-only, net10.0)
│       ├── Program.cs      ← entry point (GtkMauiApplication)
│       ├── MauiProgram.cs  ← DI builder (UseMauiAppLinuxGtk4<App>)
│       ├── App.xaml/.cs    ← Application root, imposta MainPage = new NavigationPage(new StartupPage())
│       ├── Pages/
│       │   ├── StartupPage.xaml      ← UI: chooser GTK nativo per aprire `.pws`
│       │   ├── BrowserPage.xaml      ← UI: toolbar + WebView + status bar
│       │   └── BrowserPage.xaml.cs   ← code-behind: VM da DI, sync WebView
│       ├── Services/
│       │   ├── IPwsArchivePicker.cs  ← astrazione chooser archivio
│       │   ├── GtkPwsArchivePicker.cs← `Gtk.FileDialog` nativo Linux
│       │   └── PwsFileService.cs     ← mantiene il provider `.pws` corrente
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

### PWS.App.Linux
- Progetto **separato** dedicato a Linux: `Platform.Maui.Linux.Gtk4` porta dipendenze
  native GTK4 che non devono inquinare build su altri OS.
- Per scegliere un archivio `.pws`, usare un servizio GTK nativo (`Gtk.FileDialog`) invece di
  `Microsoft.Maui.Storage.FilePicker`, che sul backend Linux/GTK può non essere implementato.
- `BrowserViewModel` non dipende da MAUI Controls: usa solo `ICommand` e `INotifyPropertyChanged`.
- `BrowserPage.xaml.cs` è l'unico punto in cui si tocca la `WebView` MAUI.
- Il ViewModel risolto tramite `IPlatformApplication.Current!.Services.GetRequiredService<T>()`:
  MAUI crea le pagine via Shell `ContentTemplate` (reflection, non DI-injection),
  quindi si usa il service-locator pattern nel costruttore della pagina.
- Il flusso di navigazione per link custom (`pws://`, `api://`, ecc.):
  `WebView.Navigating` → `e.Cancel = true` → `BrowserViewModel.NavigateCommand`.

---

## Regola fondamentale — Prima di ogni commit

**Ogni volta che apporti modifiche, PRIMA del commit devi:**

1. ✅ Verificare che il C# compili: `dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj` → **0 errori**
2. ✅ Verificare che Docusaurus compili: `cd docs && pnpm build` → **[SUCCESS]**
3. ✅ Aggiornare la documentazione in `docs/` riflettendo le modifiche apportate
4. ✅ Usare il formato **Conventional Commits** per il messaggio

> Questa regola vale sia per modifiche al codice C# sia per modifiche alla documentazione.
> La documentazione è parte del progetto al pari del codice.

---

## Conventional Commits + SemVer

Ogni commit **deve** seguire il formato [Conventional Commits](https://www.conventionalcommits.org/):

```
<tipo>[scope opzionale][! per breaking]: <descrizione>
```

### Tipi e impatto SemVer

| Tipo | SemVer | Quando usarlo |
|------|--------|---------------|
| `feat` | MINOR | Nuova funzionalità |
| `fix` | PATCH | Correzione bug |
| `feat!` / `BREAKING CHANGE` | MAJOR | Rottura compatibilità |
| `docs` | — | Solo documentazione |
| `refactor` | — | Refactoring puro |
| `test` | — | Test |
| `chore` | — | Build, dipendenze, CI |
| `perf` | — | Ottimizzazioni |
| `style` | — | Formattazione |
| `ci` | — | Pipeline CI/CD |

### Scope consigliati

`core` · `app` · `providers` · `navigation` · `ui` · `vm` · `docs` · `deps`

### Esempi

```bash
feat(providers): aggiunge SqliteContentProvider
fix(navigation): corregge doppio push su GoBack
feat!: ContentResponse.Content diventa required
docs(providers): documenta ApiContentProvider
chore(deps): aggiorna Platform.Maui.Linux.Gtk4 a 0.7.0
```

---

## Come buildare

```bash
# .NET / C#  (senza il workload MAUI tradizionale, che è rotto su net10.0)
dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj

# Docusaurus (sviluppo)
cd docs && pnpm start

# Docusaurus (produzione)
cd docs && pnpm build
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
# Arch / EndeavourOS / Manjaro
sudo pacman -S webkitgtk-6.0
```

---

## Convenzioni di codice

- **C#**: `latest` LangVersion, `nullable enable`, `implicit usings enable`.
- Classi `sealed` di default dove non serve ereditarietà.
- I provider implementano `IContentProvider`; registrarli nel `CompositeContentProvider`
  in `MauiProgram.cs`.
- **Non** usare `Update` negli item `MauiXaml` del csproj quando
  `EnableDefaultXamlItems=false` — usare sempre `Include`.
- Usare **`Dispatcher.Dispatch()`** (dalla pagina/view) per aggiornare la UI da thread diversi.
  `MainThread.BeginInvokeOnMainThread` **non** è implementato da `Platform.Maui.Linux.Gtk4.Essentials`.
- Docusaurus: TypeScript, tema `classic`.

---

## Roadmap / TODO

- [ ] Aggiungere `ApiContentProvider` al `CompositeContentProvider` in `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` attraverso `ApiContentProvider` nella `WebView`
- [ ] Aggiungere la barra di progresso durante il caricamento
- [ ] Completare la documentazione in `/docs`
- [ ] Test unitari per `PWS.Core` (xUnit)
