# CLAUDE.md — Istruzioni per Claude AI

Questo file descrive il progetto per l'assistente AI Claude.
Viene letto automaticamente da Claude all'inizio di ogni sessione.

## Progetto: PWS Browser

**PWS** è un browser .NET MAUI nativo per **Linux/GTK4** (`Platform.Maui.Linux.Gtk4` v0.6.0).
La caratteristica chiave è che la WebView **non carica mai contenuti dal filesystem**:
tutto passa attraverso l'astrazione `IContentProvider`.

---

## Struttura

```
PWS_MAUI/
├── src/
│   ├── PWS.Core/        ← libreria portable net10.0, ZERO dipendenze MAUI
│   └── PWS.App/         ← app MAUI GTK4 net10.0
├── docs/                ← documentazione Docusaurus (TypeScript, pnpm)
└── CLAUDE.md
```

### PWS.Core (nessuna dipendenza MAUI)
- `Abstractions/` → `IContentProvider`, `INavigationService`
- `Models/`       → `ContentRequest`, `ContentResponse` (usa `Stream`, è `IDisposable`), `NavigationEntry`
- `Navigation/`   → `NavigationHistory`, `NavigationService`
- `Providers/`    → `InMemoryContentProvider` (pws://), `ApiContentProvider` (http/https/api://), `CompositeContentProvider`

### PWS.App (MAUI GTK4)
- `Program.cs`        → entry point `GtkMauiApplication`
- `MauiProgram.cs`    → DI builder con `UseMauiAppLinuxGtk4<App>`
- `Pages/BrowserPage` → WebView + toolbar + status bar
- `ViewModels/BrowserViewModel` → comandi nav, `HtmlContent`, `AddressText`

**Flusso link custom**: `WebView.Navigating` → `e.Cancel = true` → `BrowserViewModel.NavigateCommand`
→ `NavigationService` → `IContentProvider` → `HtmlWebViewSource`

---

## Comandi di build

```bash
# C# — usa SEMPRE questa variabile (workload MAUI rotto su net10.0)
MSBuildEnableWorkloadResolver=false dotnet build src/PWS.App/PWS.App.csproj

# Docs — sviluppo
cd docs && pnpm start

# Docs — produzione (verifica pre-commit)
cd docs && pnpm build
```

> Aggiungi `export MSBuildEnableWorkloadResolver=false` al tuo shell profile.

---

## Regola fondamentale — Prima di ogni commit

1. ✅ `MSBuildEnableWorkloadResolver=false dotnet build src/PWS.App/PWS.App.csproj` → **0 errori**
2. ✅ `cd docs && pnpm build` → **[SUCCESS]**
3. ✅ Documentazione aggiornata con le modifiche apportate

---

## Convenzioni di codice (C#)

- `LangVersion latest`, `nullable enable`, `implicit usings enable`
- Classi `sealed` di default
- `MauiXaml Include` (mai `Update`) quando `EnableDefaultXamlItems=false`
- Provider → implementano `IContentProvider` → registrati in `CompositeContentProvider` in `MauiProgram.cs`
- Il ViewModel NON dipende da MAUI Controls (solo `ICommand`, `INotifyPropertyChanged`)
- `BrowserPage.xaml.cs` è l'**unico** punto in cui si tocca la `WebView`
- DI nelle pagine via `IPlatformApplication.Current!.Services.GetRequiredService<T>()`
  (Shell crea le pagine via reflection, non constructor-injection)

## Convenzioni di codice (TypeScript/Docusaurus)

- TypeScript strict, tema `classic`
- Nessun contenuto di esempio (tutorial Docusaurus, blog placeholder, ecc.)
- Ogni nuova feature deve avere la propria pagina doc in `docs/docs/`

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
sudo apt install libgtk-4-dev libwebkitgtk-6.0-dev   # Debian/Ubuntu
sudo dnf install gtk4-devel webkitgtk6.0-devel        # Fedora
```

---

## Roadmap / TODO

- [ ] `ApiContentProvider` nel `CompositeContentProvider` di `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` via `ApiContentProvider` nella WebView
- [ ] Barra di progresso durante il caricamento
- [ ] Test unitari per `PWS.Core` (xUnit)
- [ ] Completare la documentazione in `/docs`

