---
sidebar_position: 1
slug: /intro
---

# Introduzione a PWS Browser

**PWS** sta per **Portable WebSite**: un formato file che incapsula l'**output di build
di un sito statico** — prodotto da Docusaurus, Hugo, Next.js, ecc. — in un
**singolo archivio portabile** con estensione `.pws`.

## Il problema che risolve

Un generatore di siti statici come **Docusaurus** produce una cartella `build/` con
centinaia di file HTML/CSS/JS. Distribuire quella cartella è scomodo:

- bisogna zippare, estrarre, gestire path relativi
- aprire un server locale solo per visualizzarla
- inviare un archivio da spacchettare ogni volta

Con PWS il flusso diventa:

```
pnpm build       →   docs/build/      (centinaia di file)
pws pack build/  →   docs.pws         (un solo file portabile)
PWS Browser      →   apre docs.pws    (rendering nativo, zero server)
```

**Un sito → un file.** Come un `.epub` per i libri, ma per i siti statici.

## Analogia con altri formati

| Formato | Cosa contiene |
|---------|--------------|
| `.epub` | libro elettronico (ZIP + HTML/CSS) |
| `.docx` | documento Word (ZIP + XML) |
| **`.pws`** | sito web portabile (ZIP + HTML/CSS/JS/asset) |

Il concetto è identico: un archivio ZIP con una struttura interna definita, che
un'applicazione dedicata sa come aprire e presentare.

## Il formato `.pws`

Un `.pws` è un archivio ZIP con un `manifest.json` all'interno:

```
docs.pws
├── manifest.json      ← metadati (entry point, titolo, versione)
├── index.html
├── assets/
│   ├── css/main.css
│   └── js/main.js
└── docs/
    ├── intro/index.html
    └── ...
```

Il manifest indica al browser come aprire il sito:

```json
{
  "title": "La mia documentazione",
  "version": "1.0.0",
  "entryPoint": "index.html"
}
```

## Le due parti del sistema

**1 — `pws pack`** (TODO): uno strumento CLI che prende la cartella `build/` di uno
SSG e la impacchetta in un file `.pws`.

**2 — PWS Browser** (questa app): legge il file `.pws` e lo renderizza via
`WebView GTK4` senza mai estrarre nulla su disco — ogni risorsa viene servita
in-memory da `PwsFileContentProvider`.

## Stack tecnologico

| Componente | Tecnologia |
|-----------|-----------|
| Framework UI | .NET MAUI 10 |
| Backend grafico | Platform.Maui.Linux.Gtk4 v0.6.0 |
| WebView | WebKitGTK (via GTK4) |
| Target | Linux nativo (net10.0) |
| Documentazione | Docusaurus 3, TypeScript, pnpm |

## Prossimi passi

- [Prerequisiti e installazione](./getting-started/prerequisites)
- [Come buildare e avviare](./getting-started/building)
- [Panoramica architetturale](./architecture/overview)
