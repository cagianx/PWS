---
sidebar_position: 2
---

# Build e avvio

## Compilare il progetto C#

```bash
# Dalla root del repository
MSBuildEnableWorkloadResolver=false dotnet build src/PWS.App/PWS.App.csproj
```

Output atteso:
```
Compilazione completata.
    Avvisi: 0
    Errori: 0
```

### Avviare l'applicazione

```bash
MSBuildEnableWorkloadResolver=false dotnet run --project src/PWS.App/PWS.App.csproj
```

All'avvio si aprirà una finestra GTK4 con il browser che naviga automaticamente su `pws://home`.

## Compilare la documentazione

```bash
cd docs

# Installa le dipendenze (solo la prima volta)
pnpm install

# Build di produzione (verifica pre-commit)
pnpm build

# Server di sviluppo con hot-reload
pnpm start
```

## Checklist pre-commit

Prima di ogni commit verificare che **entrambi** i seguenti comandi abbiano esito positivo:

```bash
# 1. C# — 0 errori
MSBuildEnableWorkloadResolver=false dotnet build src/PWS.App/PWS.App.csproj

# 2. Docusaurus — [SUCCESS]
cd docs && pnpm build
```

## Struttura degli output

| Artefatto | Percorso |
|-----------|---------|
| Assembly C# (Debug) | `src/PWS.App/bin/Debug/net10.0/PWS.App.dll` |
| Assembly C# (Release) | `src/PWS.App/bin/Release/net10.0/PWS.App.dll` |
| Sito Docusaurus | `docs/build/` |

