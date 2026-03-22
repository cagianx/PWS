---
sidebar_position: 2
---

# Build e avvio

## Compilare il progetto C#

```bash
# Dalla root del repository
# (MSBuildEnableWorkloadResolver=false è già in Directory.Build.props)
dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj
```

Output atteso:
```
Compilazione completata.
    Avvisi: 0
    Errori: 0
```

### Avviare l'applicazione

```bash
dotnet run --project src/PWS.App.Linux/PWS.App.Linux.csproj
```

All'avvio si aprirà una finestra GTK4 con la `StartupPage`. Dopo aver aperto un archivio `.pws`,
la `BrowserPage` si apre **vuota** e l'utente inserisce manualmente un URI del tipo
`pws://<siteId>/index.html`.

## Compilare la documentazione

```bash
cd docs

# Installa le dipendenze (solo la prima volta)
pnpm install

# Build di produzione (verifica pre-commit)
pnpm build

# Build alla root del sito (equivalente a baseUrl "blank")
pnpm run build:root

# Build con prefisso GitHub Pages
pnpm run build:ghpages

# Server di sviluppo con hot-reload
pnpm start
```

### `DOCS_BASE_URL`

La configurazione Docusaurus legge `DOCS_BASE_URL` dall'ambiente:

| Valore | Risultato |
|--------|-----------|
| non impostato | `baseUrl = '/'` |
| `blank` | `baseUrl = '/'` |
| `/PWS_MAUI/` | `baseUrl = '/PWS_MAUI/'` |

Questo permette di usare una build portabile per il packaging `.pws` e, quando serve,
di ripristinare facilmente il prefisso GitHub Pages.

## Checklist pre-commit

Prima di ogni commit verificare che **entrambi** i seguenti comandi abbiano esito positivo:

```bash
# 1. C# — 0 errori su tutti i progetti della solution
dotnet build PWS.slnx

# 2. Test formato .pws
dotnet test src/PWS.Format.Tests/PWS.Format.Tests.csproj

# 3. Docusaurus — [SUCCESS]
cd docs && pnpm build
```

## GitHub Actions

Il repository include il workflow `.github/workflows/ci.yml`, eseguito su push, pull request e manualmente.

Job eseguiti in CI:

```text
build-dotnet  → dotnet restore/build PWS.slnx su ubuntu-24.04
build-docs    → pnpm install --frozen-lockfile && pnpm build
package-docs-pws → dotnet run CreateTestPws su docs/build e genera docs.pws
test-format   → dotnet test PWS.Format.Tests (incl. test runtime con docs/build)
```

> Nota: nel workflow GitHub Actions `pnpm` viene configurato **prima** di `actions/setup-node`,
> altrimenti il runner non trova l'eseguibile e fallisce con `Unable to locate executable file: pnpm`.

Il job `build-dotnet` installa anche le dipendenze Linux richieste da GTK/WebKit:

```bash
sudo apt-get install -y libgtk-4-dev libwebkitgtk-6.0-dev
```

## Artifact scaricabili da GitHub UI

Alla fine del workflow puoi scaricare questi artifact dalla pagina della run:

| Artifact | Contenuto |
|----------|-----------|
| `pws-app-linux-build` | output Release dell'app Linux e del tool `CreateTestPws` |
| `docs-build` | sito Docusaurus statico (`docs/build/`) |
| `docs-pws` | archivio `.pws` generato dalla documentazione |
| `pws-format-test-results` | risultati test `.trx` |

## Struttura degli output

| Artefatto | Percorso |
|-----------|---------|
| Assembly C# (Debug) | `src/PWS.App.Linux/bin/Debug/net10.0/PWS.App.Linux.dll` |
| Assembly C# (Release) | `src/PWS.App.Linux/bin/Release/net10.0/PWS.App.Linux.dll` |
| Sito Docusaurus | `docs/build/` |

