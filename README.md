# PWS Browser

> **PWS** = **Portable WebSite** — incapsula l'output di build di un sito statico
> (HTML/CSS/JS) in un **singolo file archivio** con estensione `.pws`, e lo rende
> apribile con un lettore nativo desktop.

---

## Il problema che risolve

Un generatore di siti statici come **Docusaurus** produce una cartella `build/` con
centinaia di file HTML/CSS/JS. Distribuire quella cartella è scomodo: bisogna zippare,
estrarre, gestire path relativi, aprire un server locale.

Con PWS:

```
pnpm build          →   docs/build/        (cartella con centinaia di file)
pws pack build/     →   docs.pws           (un solo file, portabile)
PWS Browser         →   apre docs.pws      (rendering nativo, zero server)
```

Un sito → un file. Come un `.epub` per i libri, ma per i siti statici.

---

## Cos'è un file `.pws`?

Un `.pws` è un archivio ZIP con una struttura interna definita:

```
docs.pws  (= ZIP)
├── manifest.json      ← entry point, titolo, versione del sito
├── index.html
├── assets/
│   ├── css/main.css
│   └── js/main.js
└── docs/
    ├── intro/index.html
    └── ...
```

Il `manifest.json` descrive il sito e indica la risorsa di ingresso:

```json
{
  "title": "PWS Browser Docs",
  "version": "1.0.0",
  "entryPoint": "index.html"
}
```

---

## Architettura del sistema

```
┌─────────────────────────────────────────────────────────┐
│  FASE 1 — Produzione del .pws                           │
│                                                         │
│  Docusaurus/Hugo/Next.js                                │
│       pnpm build  →  build/                             │
│       pws pack    →  site.pws          ← TODO           │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  FASE 2 — Lettura del .pws                              │
│                                                         │
│  PWS Browser (questa app)                               │
│  FilePicker → apre site.pws                             │
│       ↓                                                 │
│  PwsFileContentProvider                ← TODO           │
│  (legge ZIP in-memory, zero estrazione)                 │
│       ↓                                                 │
│  NavigationService + NavigationHistory                  │
│       ↓                                                 │
│  WebView GTK4 (WebKitGTK)                               │
└─────────────────────────────────────────────────────────┘
```

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
│   ├── PWS.Format/        ← libreria formato .pws (net10.0, zero NuGet extra)
│   │   ├── Manifest/      ← PwsManifest, SiteManifest
│   │   ├── Crypto/        ← JWT BCL-only, MerkleHasher, IPwsSigningKey
│   │   │                     NoneKey · HmacKey · EcDsaKey · PwsSigningKey
│   │   ├── Filesystem/    ← IPwsFileSystem, PwsFileEntry
│   │   ├── Packing/       ← PwsPacker, PwsPackOptions, PwsSiteSource
│   │   └── Reading/       ← PwsReader, PwsOpenOptions
│   └── PWS.App.Linux/     ← app MAUI GTK4 (Linux-only, net10.0)
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

# Prerequisiti di sistema (Arch / EndeavourOS / Manjaro)
sudo pacman -S webkitgtk-6.0   # gtk4 è già incluso in base

# C# — build  (MSBuildEnableWorkloadResolver=false è in Directory.Build.props)
dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj

# C# — avvio
dotnet run --project src/PWS.App.Linux/PWS.App.Linux.csproj

# Documentazione
cd docs && pnpm install && pnpm build
```


---

## Stato attuale

| Componente | Stato |
|-----------|-------|
| `PWS.Core` — astrazioni e navigazione | ✅ |
| `PWS.App.Linux` — UI browser GTK4 | ✅ |
| `PWS.Format` — manifest, packer, reader, JWT | ✅ |
| `InMemoryContentProvider` (demo/dev) | ✅ |
| `ApiContentProvider` (http/api) | ✅ |
| Specifica formato `.pws` e `manifest.json` | ✅ |
| **`PwsFileContentProvider`** — bridge Format→Core | 🔲 |
| **`pws pack`** — CLI packer (cartella → `.pws`) | 🔲 |
| Dialog apertura file `.pws` (FilePicker) | 🔲 |
| Barra di progresso caricamento | 🔲 |
| Test unitari (`PWS.Core`, `PWS.Format`) | 🔲 |

---

## Documentazione

La documentazione completa è in [`docs/`](./docs):

- **Getting Started** — prerequisiti, build, avvio
- **Architettura** — panoramica, PWS.Core, PWS.App.Linux
- **Content Providers** — `IContentProvider`, InMemory, Api, Composite

---

## Convenzioni

- Commit: [Conventional Commits](https://www.conventionalcommits.org/) + SemVer
  (`feat`→MINOR · `fix`→PATCH · `feat!`/`BREAKING CHANGE`→MAJOR)
- Pre-commit: `dotnet build` → 0 errori **e** `pnpm build` → SUCCESS
- C#: `nullable enable`, `implicit usings`, classi `sealed` di default
- `MauiXaml Include` (non `Update`) quando `EnableDefaultXamlItems=false`

