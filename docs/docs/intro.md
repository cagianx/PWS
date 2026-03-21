---
sidebar_position: 1
slug: /intro
---

# Introduzione a PWS Browser

**PWS** sta per **Portable WebSite**: un formato file (simile a uno ZIP) che racchiude
un intero sito web statico — HTML, CSS, JavaScript, immagini e tutti gli asset — in un
**singolo archivio portabile** con estensione `.pws`.

Il browser PWS apre questi archivi e li renderizza tramite la `WebView` nativa
**senza mai estrarre i file su disco**: il contenuto viene servito completamente
in-memory dall'astrazione `IContentProvider`.

## Analogia con altri formati

| Formato | Cosa contiene |
|---------|--------------|
| `.epub` | libro elettronico (ZIP + HTML/CSS) |
| `.docx` | documento Word (ZIP + XML) |
| **`.pws`** | sito web portabile (ZIP + HTML/CSS/JS/asset) |

Il concetto è lo stesso: un archivio compresso con una struttura interna definita,
che un'applicazione dedicata sa come aprire e presentare all'utente.

## Perché non aprire i file direttamente?

Aprire i file di un sito web direttamente dal filesystem crea diversi problemi:

- **Sicurezza**: la WebView avrebbe accesso libero al disco
- **Portabilità**: i path assoluti si rompono spostandosi tra macchine
- **Atomicità**: un sito è composto da decine/centinaia di file; un `.pws` è un file solo

Con il formato `.pws` il sito è un **singolo file portabile**: si copia, si condivide,
si apre — esattamente come si farebbe con un `.epub` o un `.pdf`.

## Come funziona internamente

La WebView non sa nulla del formato `.pws`. L'archivio viene aperto una volta sola,
e ogni richiesta di risorsa (pagina, immagine, script, font…) viene intercettata e
servita dal `IContentProvider` senza mai scrivere nulla su disco:

```
file.pws (archivio ZIP)
  ├── manifest.json        ← metadati del sito (entry point, titolo, versione)
  ├── index.html
  ├── css/style.css
  ├── js/app.js
  └── img/logo.png
        │
        ▼ (tutto in-memory, mai su disco)
  PwsFileContentProvider  ←── serve le risorse all'interno dell'archivio
        │
        ▼
  WebView (GTK4 / WebKitGTK)
```

## Stack tecnologico

| Componente | Tecnologia |
|-----------|-----------|
| Framework UI | .NET MAUI 10 |
| Backend grafico | Platform.Maui.Linux.Gtk4 v0.6.0 |
| WebView | WebKitGTK (via GTK4) |
| Target | Linux nativo (net10.0) |
| Documentazione | Docusaurus 3, TypeScript, pnpm |

## Struttura del repository

```
PWS_MAUI/
├── src/
│   ├── PWS.Core/     ← logica pura, zero dipendenze MAUI
│   └── PWS.App/      ← applicazione MAUI GTK4
├── docs/             ← questa documentazione
├── CLAUDE.md         ← istruzioni per Claude AI
└── .github/
    └── copilot-instructions.md
```

## Prossimi passi

- [Prerequisiti e installazione](./getting-started/prerequisites)
- [Come buildare e avviare](./getting-started/building)
- [Panoramica architetturale](./architecture/overview)
