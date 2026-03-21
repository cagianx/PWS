---
sidebar_position: 1
---

# Panoramica dell'architettura

PWS è diviso in due layer nettamente separati: **PWS.Core** (logica pura) e **PWS.App** (UI MAUI).

## Contesto: dal build al browser

Il flusso completo del sistema PWS è diviso in due fasi:

```
FASE 1 — Produzione
  Docusaurus / Hugo / Next.js
    pnpm build  →  build/       (centinaia di file HTML/CSS/JS)
    pws pack    →  site.pws     (un solo archivio ZIP — TODO)

FASE 2 — Lettura
  PWS Browser
    FilePicker        →  apre site.pws
    PwsFileProvider   →  legge dal ZIP in-memory (TODO)
    NavigationService →  gestisce history / back / forward
    WebView GTK4      →  renderizza HTML senza toccare disco
```

## Diagramma

```
┌─────────────────────────────────────────────────────────┐
│                      PWS.App                            │
│                   (MAUI / GTK4)                         │
│                                                         │
│  ┌──────────────┐    ┌───────────────────────────────┐  │
│  │ BrowserPage  │    │      BrowserViewModel         │  │
│  │  (XAML/CS)   │◄──►│  AddressText, HtmlContent     │  │
│  │              │    │  NavigateCommand, GoBack...    │  │
│  │  [WebView]   │    └──────────────┬────────────────┘  │
│  └──────────────┘                   │ INavigationService│
└────────────────────────────────────-│───────────────────┘
                                      │
┌─────────────────────────────────────│───────────────────┐
│                   PWS.Core          │                    │
│               (net10.0, no MAUI)    ▼                    │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │              NavigationService                   │   │
│  │   NavigationHistory + IContentProvider           │   │
│  └──────────────────────┬───────────────────────────┘   │
│                         │ IContentProvider               │
│          ┌──────────────┼──────────────┐                │
│          ▼              ▼              ▼                 │
│  ┌──────────────┐ ┌──────────┐ ┌─────────────────┐     │
│  │  PwsFile     │ │   Api    │ │   Composite      │     │
│  │  (archivio   │ │(http/api)│ │  (delega ai      │     │
│  │   .pws)      │ └──────────┘ │   provider)      │     │
│  └──────────────┘              └─────────────────-┘     │
└─────────────────────────────────────────────────────────┘
                    ▲
             file.pws (ZIP)
```

## Flusso di navigazione

Il flusso completo quando l'utente apre `archivio.pws` e clicca un link interno:

```
1. Apertura file
   FilePicker → path del .pws → PwsFileContentProvider.Open(path)
   (l'archivio rimane aperto in-memory per tutta la sessione)

2. WebView.Navigating (evento MAUI) — es. link a "pws://about"
   └─ e.Cancel = true
   └─ BrowserViewModel.NavigateCommand.Execute("pws://about")

3. NavigationService.NavigateAsync(uri)
   └─ CompositeContentProvider → PwsFileContentProvider.GetAsync(request)
   └─ legge "about.html" dall'archivio ZIP (ZipArchive.GetEntry)
   └─ ContentResponse { Stream = <contenuto della entry> }

4. NavigationService.Navigated evento
   └─ BrowserViewModel.HtmlContent = StreamReader.ReadToEnd()

5. BrowserPage.OnViewModelPropertyChanged
   └─ BrowserWebView.Source = new HtmlWebViewSource { Html = HtmlContent }
```

## Principi di progetto

| Principio | Implementazione |
|-----------|----------------|
| **File unico** | Un sito = un `.pws` — portabile come un `.epub` |
| **Zero estrazione** | `ZipArchive` in-memory, nessun file temporaneo su disco |
| **Separation of concerns** | PWS.Core non sa nulla di MAUI |
| **Open/closed** | Nuovi provider → solo implementare `IContentProvider` |


```
┌─────────────────────────────────────────────────────────┐
│                      PWS.App                            │
│                   (MAUI / GTK4)                         │
│                                                         │
│  ┌──────────────┐    ┌───────────────────────────────┐  │
│  │ BrowserPage  │    │      BrowserViewModel         │  │
│  │  (XAML/CS)   │◄──►│  AddressText, HtmlContent     │  │
│  │              │    │  NavigateCommand, GoBack...    │  │
│  │  [WebView]   │    └──────────────┬────────────────┘  │
│  └──────────────┘                   │ INavigationService│
└────────────────────────────────────-│───────────────────┘
                                      │
┌─────────────────────────────────────│───────────────────┐
│                   PWS.Core          │                    │
│               (net10.0, no MAUI)    ▼                    │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │              NavigationService                   │   │
│  │   NavigationHistory + IContentProvider           │   │
│  └──────────────────────┬───────────────────────────┘   │
│                         │ IContentProvider               │
│          ┌──────────────┼──────────────┐                │
│          ▼              ▼              ▼                 │
│  ┌──────────────┐ ┌──────────┐ ┌─────────────────┐     │
│  │  InMemory    │ │   Api    │ │   Composite      │     │
│  │  (pws://)    │ │(http/api)│ │  (delega ai      │     │
│  └──────────────┘ └──────────┘ │   provider)      │     │
│                                └─────────────────-┘     │
└─────────────────────────────────────────────────────────┘
```

## Flusso di navigazione

Il flusso completo quando l'utente clicca un link `pws://about`:

```
1. WebView.Navigating (evento MAUI)
   └─ e.Cancel = true  (la WebView NON naviga da sola)
   └─ BrowserViewModel.NavigateCommand.Execute("pws://about")

2. NavigationService.NavigateAsync(uri)
   └─ CompositeContentProvider.CanHandle(uri) → true
   └─ InMemoryContentProvider.GetAsync(request)
   └─ ContentResponse { Stream = <html>...</html> }

3. NavigationService.Navigated evento
   └─ BrowserViewModel.OnNavigated
   └─ HtmlContent = StreamReader.ReadToEnd()

4. BrowserPage.OnViewModelPropertyChanged
   └─ BrowserWebView.Source = new HtmlWebViewSource { Html = HtmlContent }
```

## Principi di progetto

| Principio | Implementazione |
|-----------|----------------|
| **Separation of concerns** | PWS.Core non sa nulla di MAUI |
| **Dependency inversion** | ViewModel dipende da `INavigationService`, non dall'implementazione |
| **Open/closed** | Nuovi provider → solo implementare `IContentProvider` |
| **No filesystem** | La WebView non tocca mai il filesystem locale |

