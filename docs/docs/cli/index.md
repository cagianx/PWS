---
sidebar_position: 1
title: pwstool
---

# `pwstool` — CLI per archivi .pws

`PWS.Tool` è uno strumento a riga di comando per lavorare con gli archivi `.pws`
direttamente dal terminale, senza aprire l'applicazione grafica.

## Installazione / esecuzione rapida

```bash
# Dalla radice del repository
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- <verbo> [opzioni]
```

## Verbi disponibili

| Verbo | Descrizione |
|-------|-------------|
| [`validate`](./validate) | Verifica l'integrità di un archivio `.pws` |

## Esempio

```bash
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- validate mio-sito.pws
```

## Dipendenze

| Package | Versione | Scopo |
|---------|----------|-------|
| `CommandLineParser` | 2.9.1 | Parsing argomenti CLI |
| `Microsoft.Extensions.Logging.Console` | 10.0.* | Output diagnostica su console |
| `PWS.Format` | (riferimento locale) | Lettura e verifica archivi `.pws` |

