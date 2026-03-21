# GitHub Copilot – Istruzioni di progetto: PWS_MAUI

## Panoramica

**PWS** sta per **Portable WebSite**: un formato file (simile a uno ZIP) che racchiude
un intero sito web statico (HTML, CSS, JS, asset) in un singolo archivio portabile con
estensione `.pws`.

Il sistema è composto da due parti:

1. **`pws pack`** (TODO) — CLI che prende la cartella `build/` di uno SSG (Docusaurus,
   Hugo, Next.js, …) e la impacchetta in un `.pws` con un `manifest.json` di metadati
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

sco## Regola fondamentale — Prima di ogni commit

**Ogni volta che apporti modifiche, PRIMA del commit devi:**

1. ✅ Verificare che il C# compili: `dotnet build src/PWS.App/PWS.App.csproj` → **0 errori**
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
# .NET / C#  (MSBuildEnableWorkloadResolver=false è in Directory.Build.props)
dotnet build src/PWS.App/PWS.App.csproj

# Docusaurus (sviluppo)
cd docs && npm run start

# Docusaurus (produzione)
cd docs && npm run build
```


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

- [ ] Definire la specifica del formato `.pws` (struttura archivio, manifest)
- [ ] Implementare `PwsFileContentProvider` — legge risorse dall'archivio `.pws`
- [ ] Implementare `pws pack` — CLI che impacchetta `build/` → `.pws`
- [ ] Aggiungere `ApiContentProvider` al `CompositeContentProvider` in `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` attraverso `ApiContentProvider` nella `WebView`
- [ ] Dialog di apertura file `.pws` (via MAUI Essentials `FilePicker`)
- [ ] Aggiungere la barra di progresso durante il caricamento
- [ ] Completare la documentazione in `/docs`
- [ ] Test unitari per `PWS.Core` (xUnit)

