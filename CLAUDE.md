# CLAUDE.md — Istruzioni per Claude AI

Questo file descrive il progetto per l'assistente AI Claude.
Viene letto automaticamente da Claude all'inizio di ogni sessione.

## Progetto: PWS Browser

**PWS** sta per **Portable WebSite**: un formato file (simile a uno ZIP) che
incapsula l'**output di build di un sito statico** (HTML/CSS/JS/asset) — prodotto
da Docusaurus, Hugo, Next.js, ecc. — in un **singolo archivio portabile** con
estensione `.pws`.

Il sistema è composto da due parti:

1. **`pws pack`** (TODO) — CLI/tool che prende la cartella `build/` di uno SSG
   e la impacchetta in un file `.pws` (con un `manifest.json` di metadati)
2. **PWS Browser** (questa app) — lettore nativo GTK4 che apre i file `.pws`
   e li renderizza tramite WebView **senza mai estrarre file su disco**

### Flusso completo

```
Docusaurus/Hugo/...
  └─ pnpm build  →  build/           (cartella con centinaia di file)
  └─ pws pack    →  docs.pws         (un solo file archivio ZIP)

PWS Browser
  └─ FilePicker  →  apre docs.pws
  └─ PwsFileContentProvider          (legge ZIP in-memory)
  └─ NavigationService
  └─ WebView GTK4                    (renderizza, zero server)
```

### Analogia
| Formato | Contenuto |
|---------|-----------|
| `.epub` | libro elettronico (ZIP + HTML/CSS) |
| `.docx` | documento Word (ZIP + XML) |
| **`.pws`** | sito web portabile (ZIP + HTML/CSS/JS/asset) |

### Perché IContentProvider?
La WebView non sa nulla del formato `.pws`. L'archivio viene aperto una volta sola,
e ogni richiesta di risorsa (pagina, immagine, script) viene intercettata e servita
dal provider senza mai passare per il filesystem o per HTTP.

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
# C# — MSBuildEnableWorkloadResolver=false è in Directory.Build.props (automatico)
dotnet build src/PWS.App/PWS.App.csproj

# Docs — sviluppo
cd docs && pnpm start

# Docs — produzione (verifica pre-commit)
cd docs && pnpm build
```


---

## Regola fondamentale — Prima di ogni commit

1. ✅ `dotnet build src/PWS.App/PWS.App.csproj` → **0 errori**
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
| `app` | PWS.App (qualsiasi) |
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

- [ ] Definire la specifica del formato `.pws` (struttura archivio, `manifest.json`)
- [ ] Implementare `PwsFileContentProvider` — legge risorse dall'archivio `.pws` via `ZipArchive`
- [ ] Implementare `pws pack` — CLI che impacchetta `build/` → `.pws`
- [ ] `ApiContentProvider` nel `CompositeContentProvider` di `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` via `ApiContentProvider` nella WebView
- [ ] Dialog di apertura file `.pws` (via MAUI Essentials `FilePicker`)
- [ ] Barra di progresso durante il caricamento
- [ ] Test unitari per `PWS.Core` (xUnit)
- [ ] Completare la documentazione in `/docs`

