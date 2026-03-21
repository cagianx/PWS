---
sidebar_position: 1
slug: /intro
---

# Introduzione a PWS Browser

**PWS** (Platform Web Surface) è un browser nativo per **Linux/GTK4** costruito con **.NET MAUI**.

La caratteristica fondamentale di PWS è che la `WebView` **non carica mai contenuti direttamente dal filesystem o da URL arbitrari**: tutto il contenuto è fornito da un'astrazione chiamata `IContentProvider`, che può essere implementata in qualsiasi modo — dizionario in memoria, API REST, database, generazione dinamica, ecc.

## Perché PWS?

I browser tradizionali sono accoppiati al protocollo HTTP e al filesystem. PWS rompe questo accoppiamento:

| Browser tradizionale | PWS |
|---------------------|-----|
| Carica URL HTTP/HTTPS | Carica da `IContentProvider` |
| Dipende da server web | Indipendente dalla sorgente |
| Hardcoded su HTTP | Qualsiasi schema (`pws://`, `api://`, ...) |
| Nessuna astrazione contenuto | `IContentProvider` intercambiabile |

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
    └── copilot-instructions.md  ← istruzioni per GitHub Copilot
```

## Prossimi passi

- [Prerequisiti e installazione](./getting-started/prerequisites)
- [Come buildare e avviare](./getting-started/building)
- [Panoramica architetturale](./architecture/overview)
