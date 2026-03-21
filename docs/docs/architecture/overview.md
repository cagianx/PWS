---
sidebar_position: 1
---

# Panoramica dell'architettura

PWS è diviso in due layer nettamente separati: **PWS.Core** (logica pura) e **PWS.App** (UI MAUI).

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

