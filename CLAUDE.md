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
│   └── PWS.App.Linux/   ← app MAUI GTK4 net10.0 (Linux-only)
├── docs/                ← documentazione Docusaurus (TypeScript, pnpm)
└── CLAUDE.md
```

### PWS.Core (nessuna dipendenza MAUI)
- `Abstractions/` → `IContentProvider`, `INavigationService`
- `Models/`       → `ContentRequest`, `ContentResponse` (usa `Stream`, è `IDisposable`), `NavigationEntry`
- `Navigation/`   → `NavigationHistory`, `NavigationService`
- `Providers/`    → `InMemoryContentProvider` (pws://), `ApiContentProvider` (http/https/api://), `CompositeContentProvider`

### PWS.App.Linux (MAUI GTK4 — Linux)
- Progetto **separato** dedicato a Linux: le dipendenze native GTK4 non inquinano altri target
- `Program.cs`        → entry point `GtkMauiApplication`
- `MauiProgram.cs`    → DI builder con `UseMauiAppLinuxGtk4<App>`
- `Pages/BrowserPage` → WebView + toolbar + status bar
- `ViewModels/BrowserViewModel` → comandi nav, `HtmlContent`, `AddressText`

**Flusso link custom**: `WebView.Navigating` → `e.Cancel = true` → `BrowserViewModel.NavigateCommand`
→ `NavigationService` → `IContentProvider` → `HtmlWebViewSource`

---

## Comandi di build

```bash
# C# — (MSBuildEnableWorkloadResolver=false è già in Directory.Build.props)
dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj

# Docs — sviluppo
cd docs && pnpm start

# Docs — produzione (verifica pre-commit)
cd docs && pnpm build
```

---

## Regola fondamentale — Prima di ogni commit

1. ✅ `dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj` → **0 errori**
2. ✅ `cd docs && pnpm build` → **[SUCCESS]**
3. ✅ Documentazione aggiornata con le modifiche apportate
4. ✅ Messaggio di commit in formato **Conventional Commits**

---

## Conventional Commits + SemVer

Ogni commit **deve** seguire il formato [Conventional Commits](https://www.conventionalcommits.org/):

```
<tipo>[scope opzionale][! per breaking]: <descrizione>

[corpo opzionale]

[footer opzionale — es. BREAKING CHANGE: ...]
```

### Tipi ammessi e impatto SemVer

| Tipo | Descrizione | SemVer |
|------|-------------|--------|
| `feat` | Nuova funzionalità | **MINOR** `0.x.0` |
| `fix` | Correzione di un bug | **PATCH** `0.0.x` |
| `feat!` / `fix!` / `BREAKING CHANGE` | Rottura compatibilità API | **MAJOR** `x.0.0` |
| `docs` | Solo documentazione | no bump |
| `refactor` | Refactoring senza nuove feature o fix | no bump |
| `test` | Aggiunta/modifica test | no bump |
| `chore` | Aggiornamenti build, dipendenze, CI | no bump |
| `perf` | Miglioramento prestazioni | no bump |
| `style` | Formattazione, whitespace | no bump |
| `ci` | Modifiche pipeline CI/CD | no bump |
| `build` | Modifiche al sistema di build | no bump |

### Esempi

```bash
feat(providers): aggiunge SqliteContentProvider
fix(navigation): corregge doppio push su GoBack
feat!: ContentResponse.Content diventa required
docs(providers): documenta ApiContentProvider
chore(deps): aggiorna Platform.Maui.Linux.Gtk4 a 0.7.0
refactor(core): estrae interfaccia INavigationHistory
```

### Scope consigliati

| Scope | Riguarda |
|-------|----------|
| `core` | PWS.Core (qualsiasi) |
| `app` | PWS.App.Linux (qualsiasi) |
| `providers` | IContentProvider e implementazioni |
| `navigation` | NavigationService, NavigationHistory |
| `ui` | XAML, stili, layout |
| `vm` | BrowserViewModel, BaseViewModel |
| `docs` | Documentazione Docusaurus |
| `deps` | Dipendenze NuGet o npm |

---

## Convenzioni di codice (C#)

- `LangVersion latest`, `nullable enable`, `implicit usings enable`
- Classi `sealed` di default
- `MauiXaml Include` (mai `Update`) quando `EnableDefaultXamlItems=false`
- Usare **`Dispatcher.Dispatch()`** (dalla pagina/view) per aggiornare la UI da thread diversi.
  `MainThread.BeginInvokeOnMainThread` **non** è implementato da `Platform.Maui.Linux.Gtk4.Essentials`.
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
sudo pacman -S webkitgtk-6.0                          # Arch/EndeavourOS/Manjaro
```

---

## Roadmap / TODO

- [ ] `ApiContentProvider` nel `CompositeContentProvider` di `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` via `ApiContentProvider` nella WebView
- [ ] Barra di progresso durante il caricamento
- [ ] Test unitari per `PWS.Core` (xUnit)
- [ ] Completare la documentazione in `/docs`
