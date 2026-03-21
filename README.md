# PWS Browser

> **PWS** = **Portable WebSite** — un formato file (stile ZIP) che racchiude un intero
> sito web statico in un singolo archivio portabile con estensione `.pws`.

Il browser PWS apre questi archivi e li renderizza tramite **WebView nativa GTK4**,
senza mai estrarre file su disco.

---

## Cos'è un file `.pws`?

Un `.pws` è un archivio ZIP rinominato con una struttura interna definita:

```
archivio.pws
├── manifest.json   ← entry point, titolo, versione
├── index.html
├── css/
├── js/
└── img/
```

| Formato | Contenuto |
|---------|-----------|
| `.epub` | libro elettronico (ZIP + HTML/CSS) |
| `.docx` | documento Word (ZIP + XML) |
| **`.pws`** | sito web portabile (ZIP + HTML/CSS/JS/asset) |

Un sito → un file. Portabile come un `.epub`, nessun server necessario.

---

## Stack

| Componente | Tecnologia |
|-----------|-----------|
| Framework | .NET MAUI 10 — target `net10.0` |
| Backend UI | [`Platform.Maui.Linux.Gtk4`](https://github.com/Redth/Maui.Gtk) v0.6.0 |
| WebView | WebKitGTK (via GTK4) |
| OS target | Linux nativo |
| Docs | Docusaurus 3 · TypeScript · pnpm |

---

## Struttura del progetto

```
PWS_MAUI/
├── src/
│   ├── PWS.Core/          ← logica pura (net10.0, zero dipendenze MAUI)
│   │   ├── Abstractions/  ← IContentProvider, INavigationService
│   │   ├── Models/        ← ContentRequest, ContentResponse, NavigationEntry
│   │   ├── Navigation/    ← NavigationHistory, NavigationService
│   │   └── Providers/     ← InMemoryContentProvider, ApiContentProvider,
│   │                          CompositeContentProvider
│   └── PWS.App/           ← app MAUI GTK4
│       ├── Program.cs     ← entry point (GtkMauiApplication)
│       ├── MauiProgram.cs ← DI builder
│       ├── Pages/         ← BrowserPage (WebView + toolbar + status bar)
│       └── ViewModels/    ← BrowserViewModel
└── docs/                  ← documentazione Docusaurus
```

---

## Build

```bash
# Prerequisiti di sistema (Debian/Ubuntu)
sudo apt install libgtk-4-dev libwebkitgtk-6.0-dev

# C# — build
MSBuildEnableWorkloadResolver=false dotnet build src/PWS.App/PWS.App.csproj

# C# — avvio
MSBuildEnableWorkloadResolver=false dotnet run --project src/PWS.App/PWS.App.csproj

# Documentazione
cd docs && pnpm install && pnpm build
```

> Aggiungere `export MSBuildEnableWorkloadResolver=false` al proprio shell profile
> per non doverlo specificare ogni volta.

---

## Come funziona

```
file.pws  →  PwsFileContentProvider  →  NavigationService  →  WebView GTK4
              (legge dall'archivio        (gestisce history,     (renderizza HTML
               ZIP in-memory)             back/forward)          senza toccare disco)
```

I link all'interno del sito vengono intercettati dalla `WebView.Navigating` e
instradati al `NavigationService` — la WebView non naviga mai autonomamente verso
URI custom (`pws://`).

---

## Stato attuale

| Componente | Stato |
|-----------|-------|
| `PWS.Core` — astrazioni e navigazione | ✅ Implementato |
| `PWS.App` — UI browser GTK4 | ✅ Implementato |
| `InMemoryContentProvider` (demo) | ✅ Implementato |
| `ApiContentProvider` (http/api) | ✅ Implementato |
| **`PwsFileContentProvider`** (archivio `.pws`) | 🔲 TODO |
| Specifica formato `.pws` / manifest | 🔲 TODO |
| Dialog apertura file (FilePicker) | 🔲 TODO |
| Barra di progresso caricamento | 🔲 TODO |
| Test unitari (`PWS.Core`) | 🔲 TODO |

---

## Documentazione

La documentazione completa è in [`docs/`](./docs) e include:

- **Getting Started** — prerequisiti, build e avvio
- **Architettura** — panoramica, PWS.Core, PWS.App
- **Content Providers** — `IContentProvider`, InMemory, Api, Composite

---

## Convenzioni

- Commit: [Conventional Commits](https://www.conventionalcommits.org/) + SemVer
  (`feat`→MINOR · `fix`→PATCH · `feat!`/`BREAKING CHANGE`→MAJOR)
- Pre-commit: `dotnet build` → 0 errori **e** `pnpm build` → SUCCESS
- C#: `nullable enable`, `implicit usings`, classi `sealed` di default
- `MauiXaml Include` (non `Update`) quando `EnableDefaultXamlItems=false`

