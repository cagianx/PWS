# Tasks

## 1. Migrazione del backend

- [x] 1.1 In `src/PWS.App.Linux/PWS.App.Linux.csproj` sostituire `Platform.Maui.Linux.Gtk4` 0.6.0 con `Microsoft.Maui.Platforms.Linux.Gtk4` `0.1.0-preview.12.26421.1` (versione esatta) e rimuovere `Platform.Maui.Linux.Gtk4.Essentials`; aggiornare il commento sul pacchetto nel csproj. Verifica: `grep -rn "Platform.Maui.Linux" src/` non trova più riferimenti nel csproj.
- [x] 1.2 Aggiornare i namespace: `Program.cs` e `Pages/BrowserPage.xaml.cs` a `Microsoft.Maui.Platforms.Linux.Gtk4.Platform`, `MauiProgram.cs` a `Microsoft.Maui.Platforms.Linux.Gtk4.Hosting`. Verifica: `grep -rn "using Platform.Maui" src/` non restituisce nulla.
- [x] 1.3 Aggiornare i commenti dei workaround di resize in `BrowserPage.xaml.cs` e `StartupPage.xaml.cs`: il bug è presente anche nel backend `dotnet/maui-labs` (versione fissata), la logica non cambia. Verifica: diff dei due file limitato a commenti e `using`.
- [ ] 1.4 Compilare l'app: `dotnet build src/PWS.App.Linux/PWS.App.Linux.csproj` con 0 errori. Se fallisce per `UseMaui` o per il workload, applicare l'alternativa della decisione 4 di `design.md` e annotarla lì.
- [x] 1.5 Aggiornare la documentazione che cita il pacchetto e la versione: `CLAUDE.md` (intestazione, convenzione su `Dispatcher.Dispatch()`, esempio di commit, tabella dipendenze), `README.md` (stack), `.github/copilot-instructions.md`, `docs/docs/intro.md`, `docs/docs/getting-started/prerequisites.md`, `docs/docs/architecture/pws-app.md` (link al repo, paragrafi sul bug di resize). Verifica: `grep -rn "Platform.Maui.Linux.Gtk4\|Redth/Maui.Gtk" --include=*.md . | grep -v "my-docs/\|openspec/"` non trova più riferimenti al vecchio pacchetto, e `cd docs && pnpm build` termina con `[SUCCESS]`. Nota: restano solo due citazioni storiche in `docs/docs/architecture/pws-app.md`, che documentano il bug di resize presente sia nella 0.6.0 sia nel nuovo backend.

## 2. Verifica integrata

- [ ] 2.1 Push su `main` e verifica che la CI sia verde in tutti i job (`build-dotnet`, `build-docs`, `package-docs-pws`, `test-format`, `test-integration`).
- [ ] 2.2 Prova manuale su desktop Linux con GTK4 e WebKitGTK 6, seguendo gli scenari di `specs/app-linux/backend-gtk4/spec.md`: avvio, apertura del `docs.pws` prodotto dalla CI, selezione annullata, file non valido, link interno, resize ripetuti dopo l'apertura dell'archivio. Verifica: tutti gli scenari passano; eventuali differenze vengono annotate nella change prima di archiviarla.
