# CLAUDE.md — Istruzioni per Claude AI

Questo file descrive il progetto per l'assistente AI Claude.
Viene letto automaticamente da Claude all'inizio di ogni sessione.

## Progetto: PWS Browser

**PWS** è un browser .NET MAUI nativo per **Linux/GTK4** (`Microsoft.Maui.Platforms.Linux.Gtk4` 0.1.0-ci.821.1 dalla CI della PR dotnet/maui-labs#594, backend sperimentale di `dotnet/maui-labs`).
La caratteristica chiave è che la WebView **non carica mai contenuti dal filesystem**:
tutto passa attraverso l'astrazione `IContentProvider`.

---

## Knowledge base

Questo progetto segue le convenzioni in `my-docs/`, submodule git pinnato a un tag
(clonare con `git clone --recurse-submodules`, oppure `git submodule update --init`).
Riferimenti principali:

- Principi generali → `my-docs/docs/regole/principi.md`
- Architettura solution → `my-docs/docs/regole/architettura.md`, `my-docs/docs/tecnologie/csharp/struttura-soluzione/`
- Gestione errori → `my-docs/docs/regole/gestione-errori.md`
- Logging → `my-docs/docs/regole/logging.md`
- Dependency injection → `my-docs/docs/tecnologie/csharp/16-dependency-injection.md`
- Async e cancellazione → `my-docs/docs/tecnologie/csharp/linguaggio/15-async.md`, `my-docs/docs/tecnologie/csharp/concorrenza/23-cancellation-token.md`
- Test → `my-docs/docs/regole/testing.md`, `my-docs/docs/tecnologie/csharp/test-integrazione/`, `my-docs/docs/tecnologie/csharp/test-unitari/`
- App desktop con server locale → `my-docs/docs/tecnologie/desktop/index.md`
- Documentazione → `my-docs/docs/regole/documentazione.md`
- Versionamento → `my-docs/docs/regole/versionamento.md`
- Git e commit → `my-docs/docs/tecnologie/git/index.md`
- Ambiente di sviluppo → `my-docs/docs/regole/ambiente-di-sviluppo.md`
- Analisi tecnica con OpenSpec → `my-docs/docs/processi/analisi-tecnica/02-openspec.md`
- Glossario → `my-docs/docs/glossario.md` (termini generali) e `docs/docs/glossario.md` (termini di dominio PWS)

Per cercare altri concetti, partire da `my-docs/docs/indice-analitico.md`, non da un grep
cieco sul submodule.

Regole d'uso:

- Usare i termini del glossario in nomi di classi, commit e artefatti OpenSpec. Un nuovo
  termine di dominio PWS va aggiunto a `docs/docs/glossario.md`, non a MyDocs.
- Quando si applica una regola di MyDocs, citarla nel corpo del commit
  (es. `Vedi my-docs/docs/regole/testing.md.`).
- Il bump di MyDocs è un commit dedicato:
  `cd my-docs && git checkout vX.Y.Z && cd .. && git add my-docs && git commit -m "chore(my-docs): bump to vX.Y.Z"`.
- Documentazione solo in Markdown e Mermaid: niente screenshot o PDF come fonte.

### Server MCP di Rider — via primaria per il codice

La solution è aperta in JetBrains Rider, che espone un server MCP (`mcp__rider__*`).
È la **via primaria** per leggere, cercare, scrivere e verificare il codice; gli strumenti
generici (`Read`, `Edit`, `Write`, `grep`/`find` via Bash) sono il fallback, da usare solo
se Rider non è connesso o se lo strumento Rider equivalente non esiste o fallisce.
Passare sempre `rootFolder` = radice del repository.

| Attività | Strumento Rider |
|----------|-----------------|
| Leggere file | `read_file` |
| Cercare file / testo / simboli | `search_file`, `search_text`, `search_regex`, `search_symbol`, `get_symbol_info` |
| Struttura | `list_directory_tree`, `get_solution_projects`, `get_project_dependencies`, `get_class_hierarchy` |
| Creare / modificare file | `create_new_file`, `apply_patch` |
| Refactoring | `rename_refactoring`, `change_api_signature`, `extract_method`, `extract_interface`, `move_type_to_namespace`, `safe_delete` |
| Verifica dopo ogni modifica | `lint_files` / `get_file_problems`, `reformat_file` |
| Build e test | `build_solution_start` + `build_solution_state`, `findTests`, `execute_run_configuration` |

Per rinomine e cambi di firma usare sempre i refactoring di Rider, non la sostituzione
testuale: aggiornano anche XAML, `nameof` e `<see cref>`.
La verifica pre-commit con `dotnet build` / `dotnet test` / `pnpm build` resta obbligatoria
(sezione «Regola fondamentale»).

### Skill disponibili

- `/commit` → commit assistito dall'IA (`.claude/skills/commit/SKILL.md`, procedura in
  `my-docs/docs/ia/skills/commit-ia.md`). Usarla quando l'utente chiede di committare.
- `/opsx:*` → flusso OpenSpec (sezione seguente).

---

## OpenSpec: flusso per le modifiche

Le modifiche non banali al repository (nuove funzionalità, cambi di comportamento,
migrazioni, refactoring estesi) passano da una change [OpenSpec](https://github.com/Fission-AI/OpenSpec)
prima di toccare il codice. Fanno eccezione le correzioni banali che l'utente chiede
esplicitamente di applicare al volo (typo, un valore, un link, un bump di dipendenza).

1. `/opsx:propose "<descrizione>"` → crea `openspec/changes/<nome>/` con `proposal.md`,
   `design.md`, `tasks.md` e la delta spec in `specs/<capability>/spec.md`. Non tocca il codice.
2. L'utente approva l'ambito della change.
3. `/opsx:apply` → implementa i task della change approvata.
4. `/opsx:archive` → archivia la change e aggiorna le spec in `openspec/specs/`.

Altri comandi: `/opsx:explore` (esplorazione prima di proporre), `/opsx:update`
(rivede una change), `/opsx:sync` (sincronizza una delta spec senza archiviare).
Configurazione in `openspec/config.yaml` (schema `spec-driven`, artefatti in italiano).
La CLI è una devDependency del `package.json` alla radice:

```bash
pnpm install          # alla radice del repo, installa la CLI OpenSpec
pnpm openspec list    # change attive
pnpm openspec:ui      # dashboard web delle change
```

---

## Struttura

```
PWS/
├── src/
│   ├── PWS.Core/          ← libreria portable net10.0, ZERO dipendenze MAUI
│   ├── PWS.Format/        ← libreria formato .pws net10.0, ZERO NuGet aggiuntivi
│   ├── PWS.Format.Tests/  ← test xUnit di PWS.Format
│   ├── PWS.IntegrationTests/ ← test di integrazione NUnit (Core, server loopback, CLI)
│   ├── PWS.App.Linux.Tests/  ← test NUnit di ViewModel e servizi dell'app, senza GTK
│   ├── PWS.Tool/          ← CLI pwstool (pack, validate)
│   ├── CreateTestPws/     ← utility che crea il .pws della documentazione
│   └── PWS.App.Linux/     ← app MAUI GTK4 net10.0 (Linux-only)
├── docs/                  ← documentazione Docusaurus (TypeScript, pnpm)
├── my-docs/               ← knowledge base (submodule pinnato)
├── openspec/              ← change e spec OpenSpec
├── .claude/               ← skill e comandi per Claude Code
└── CLAUDE.md
```

### PWS.Core (nessuna dipendenza MAUI)
- `Abstractions/` → `IContentProvider`, `INavigationService`
- `Models/`       → `ContentRequest`, `ContentResponse` (usa `Stream`, è `IDisposable`), `NavigationEntry`
- `Navigation/`   → `NavigationHistory`, `NavigationService`
- `Hosting/`      → `LoopbackContentServer` (server HTTP su 127.0.0.1 dedicato a un sito)
- `Providers/`    → `InMemoryContentProvider` (pws://), `ApiContentProvider` (http/https/api://), `CompositeContentProvider`, `PwsContentProvider`

### PWS.Format (nessuna dipendenza MAUI, zero NuGet extra)
- `Manifest/`    → `PwsManifest`, `SiteManifest`
- `Crypto/`      → JWT BCL-only: `IPwsSigningKey`, `PwsSigningKey` (factory), `NoneKey`, `HmacKey`, `EcDsaKey`, `MerkleHasher`, `SiteClaims`
- `Filesystem/`  → `IPwsFileSystem`, `PwsFileEntry`, `PwsFileSystem` (internal)
- `Packing/`     → `PwsPacker`, `PwsPackOptions`, `PwsSiteSource`
- `Reading/`     → `PwsReader`, `PwsOpenOptions`

### PWS.App.Linux (MAUI GTK4 — Linux)
- Progetto **separato** dedicato a Linux: le dipendenze native GTK4 non inquinano altri target
- `Program.cs`        → entry point `GtkMauiApplication`
- `MauiProgram.cs`    → DI builder con `UseMauiAppLinuxGtk4<App>`
- `Pages/StartupPage` → chooser nativo `Gtk.FileDialog` per aprire `.pws`
- `Pages/BrowserPage` → WebView + toolbar + status bar
- `Services/PwsFileService` → mantiene il `PwsContentProvider` corrente
- `ViewModels/BrowserViewModel` → comandi nav, `HtmlContent`, `AddressText`

**Flusso apertura `.pws`**: `StartupPage` → `Gtk.FileDialog` → `PwsReader.OpenAsync(path)`
→ `PwsFileService.SetProvider(new PwsContentProvider(reader))` → `Navigation.PushAsync(new BrowserPage())`
→ l'utente digita `pws://<siteId>/index.html` nella barra indirizzi

**Flusso link custom**: `WebView.Navigating` → `e.Cancel = true` → `BrowserViewModel.NavigateCommand`
→ `NavigationService` → `IContentProvider` → `HtmlWebViewSource`

---

## Comandi di build

```bash
# C# — (MSBuildEnableWorkloadResolver=false è già in Directory.Build.props)
dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj

# Docs — sviluppo
cd docs && pnpm start

# Docs — produzione (verifica pre-commit)
cd docs && pnpm build
```

---

## Regola fondamentale — Prima di ogni commit

1. ✅ `dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj` → **0 errori**
   e `dotnet test` su `PWS.Format.Tests`, `PWS.IntegrationTests` e `PWS.App.Linux.Tests` → **verdi**
2. ✅ `cd docs && pnpm build` → **[SUCCESS]**
3. ✅ Documentazione aggiornata con le modifiche apportate
4. ✅ Messaggio di commit in formato **Conventional Commits**

## Policy sui commit assistiti

- L'assistente **non deve mai eseguire automaticamente** `git commit`, `git push`, `git tag` o altre operazioni equivalenti sul repository.
- Dopo una modifica può al massimo:
  - verificare build, test e documentazione;
  - preparare un riepilogo delle modifiche;
  - proporre un messaggio di commit in formato Conventional Commits.
- Qualunque commit o push può essere eseguito **solo dopo conferma esplicita dell'utente**.

---

## Conventional Commits + SemVer

Ogni commit **approvato esplicitamente dall'utente** deve seguire il formato [Conventional Commits](https://www.conventionalcommits.org/):

```
<tipo>[scope opzionale][! per breaking]: <descrizione>

[corpo opzionale]

[footer opzionale — es. BREAKING CHANGE: ...]
```

### Tipi ammessi e impatto SemVer

| Tipo | Descrizione | SemVer |
|------|-------------|--------|
| `feat` | Nuova funzionalità | **MINOR** `0.x.0` |
| `fix` | Correzione di un bug | **PATCH** `0.0.x` |
| `feat!` / `fix!` / `BREAKING CHANGE` | Rottura compatibilità API | **MAJOR** `x.0.0` |
| `docs` | Solo documentazione | no bump |
| `refactor` | Refactoring senza nuove feature o fix | no bump |
| `test` | Aggiunta/modifica test | no bump |
| `chore` | Aggiornamenti build, dipendenze, CI | no bump |
| `perf` | Miglioramento prestazioni | no bump |
| `style` | Formattazione, whitespace | no bump |
| `ci` | Modifiche pipeline CI/CD | no bump |
| `build` | Modifiche al sistema di build | no bump |

### Esempi

```bash
feat(providers): aggiunge SqliteContentProvider
fix(navigation): corregge doppio push su GoBack
feat!: ContentResponse.Content diventa required
docs(providers): documenta ApiContentProvider
chore(deps): aggiorna Microsoft.Maui.Platforms.Linux.Gtk4 a 0.1.0-preview.13
refactor(core): estrae interfaccia INavigationHistory
```

### Scope consigliati

| Scope | Riguarda |
|-------|----------|
| `core` | PWS.Core (qualsiasi) |
| `app` | PWS.App.Linux (qualsiasi) |
| `providers` | IContentProvider e implementazioni |
| `navigation` | NavigationService, NavigationHistory |
| `ui` | XAML, stili, layout |
| `vm` | BrowserViewModel, BaseViewModel |
| `docs` | Documentazione Docusaurus |
| `deps` | Dipendenze NuGet o npm |

---

## Convenzioni di codice (C#)

- `LangVersion latest`, `nullable enable`, `implicit usings enable`
- Classi `sealed` di default
- `MauiXaml Include` (mai `Update`) quando `EnableDefaultXamlItems=false`
- Usare **`Dispatcher.Dispatch()`** (dalla pagina/view) per aggiornare la UI da thread diversi.
  `MainThread.BeginInvokeOnMainThread` non è affidabile sul backend GTK4 e l'app non usa Essentials.
- Provider → implementano `IContentProvider` → registrati in `CompositeContentProvider` in `MauiProgram.cs`
- Il ViewModel NON dipende da MAUI Controls (solo `ICommand`, `INotifyPropertyChanged`)
- `BrowserPage.xaml.cs` è l'**unico** punto in cui si tocca la `WebView`
- DI nelle pagine via `IPlatformApplication.Current!.Services.GetRequiredService<T>()`
  (Shell crea le pagine via reflection, non constructor-injection)

## Convenzioni di codice (TypeScript/Docusaurus)

- TypeScript strict, tema `classic`
- Nessun contenuto di esempio (tutorial Docusaurus, blog placeholder, ecc.)
- Ogni nuova feature deve avere la propria pagina doc in `docs/docs/`

---

## Dipendenze chiave

| Package | Versione | Scopo |
|---------|----------|-------|
| `Microsoft.Maui.Platforms.Linux.Gtk4` | 0.1.0-ci.821.1 | Backend GTK4 per MAUI su Linux (dotnet/maui-labs, sperimentale: versione esatta). Build CI della PR #594 servita da `packages/local` via `NuGet.config`: da sostituire con la prima preview su nuget.org che la include |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.12 | DI in PWS.Core |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.12 | Logging in PWS.Core |

### Prerequisiti di sistema (Linux)

Verifica: `./scripts/check-dev-env.sh` (stampa il comando per installare ciò che manca).
Se manca una dipendenza di sistema, non installarla: indicare all'utente il comando
e lasciare che la installi lui.

```bash
sudo apt install libgtk-4-dev libwebkitgtk-6.0-dev   # Debian/Ubuntu
sudo dnf install gtk4-devel webkitgtk6.0-devel        # Fedora
sudo pacman -S webkitgtk-6.0                          # Arch/EndeavourOS/Manjaro
```

---

## Roadmap / TODO

- [ ] `ApiContentProvider` nel `CompositeContentProvider` di `MauiProgram.cs`
- [ ] Gestire `http://` e `https://` via `ApiContentProvider` nella WebView
- [ ] Barra di progresso durante il caricamento
- [x] Test di integrazione per `PWS.Core`, `LoopbackContentServer` e `pwstool` (NUnit)
- [ ] Completare la documentazione in `/docs`
