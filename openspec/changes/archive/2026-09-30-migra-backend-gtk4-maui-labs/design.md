# Design

## Context

Motivazione in `proposal.md`. Stato attuale verificato sul codice e sul repository `dotnet/maui-labs` (commit del 30/09/2026, `platforms/Linux.Gtk4`):

- `PWS.App.Linux.csproj` referenzia `Platform.Maui.Linux.Gtk4` e `Platform.Maui.Linux.Gtk4.Essentials` 0.6.0, con `UseMaui=true`, `SingleProject=true`, `EnableDefaultXamlItems=false` e le pagine in XAML incluse con `MauiXaml Include`. `Directory.Build.props` disabilita il workload resolver.
- Il codice usa tre punti del backend: `GtkMauiApplication` in `Program.cs`, `UseMauiAppLinuxGtk4<App>()` in `MauiProgram.cs`, `GtkLayoutPanel` in `BrowserPage.xaml.cs`. Il resto (selettore file, finestra) usa direttamente i binding GirCore (`Gtk.*`, `Gio.*`).
- Nessun servizio Essentials è registrato o usato. Il nuovo backend richiede una registrazione esplicita (`AddLinuxGtk4Essentials()`), assente oggi anche con il vecchio.
- Nel nuovo backend le tre API hanno lo stesso nome e cambia solo il namespace: `Microsoft.Maui.Platforms.Linux.Gtk4.Platform` (`GtkMauiApplication`, `GtkLayoutPanel`, con `CrossPlatformMeasure/Arrange` pubblici) e `Microsoft.Maui.Platforms.Linux.Gtk4.Hosting` (`UseMauiAppLinuxGtk4`).
- `LayoutHandler` del nuovo backend contiene ancora lo stesso difetto di resize: lambda su `window.OnNotify` mai sganciata e lettura di `GetAllocatedWidth/Height()` al `notify::default-width`. I workaround di `BrowserPage` e `StartupPage` restano necessari.
- Dipendenze del nuovo pacchetto: `Microsoft.Maui.Controls` 10.0.41, GirCore `Gtk-4.0`, `GdkPixbuf-2.0`, `WebKit-6.0` 0.7.0.
- La UI non ha test automatici: `PWS.IntegrationTests` copre `PWS.Core`, `PWS.Format` e `PWS.Tool`, non l'app.

## Goals / Non-Goals

**Goals:**
- Passare al backend ufficiale con il minor numero di modifiche al codice dell'app.
- Mantenere invariato il comportamento descritto in `specs/app-linux/backend-gtk4/spec.md`.

**Non-Goals:**
- Correggere il bug di resize nel backend o rimuovere i workaround: va segnalato a `dotnet/maui-labs` a parte.
- Adottare BlazorWebView, i template del nuovo backend o la registrazione di Essentials.
- Rivedere la struttura del progetto (`UseMaui`, XAML, `Directory.Build.props`) oltre a quanto serve per compilare.
- Aggiungere test automatici della UI.

## Decisions

**1. Versione preview fissata in modo esatto (`0.1.0-preview.12.26421.1`).**
Il backend è dichiarato sperimentale, con API che possono cambiare tra release. Una versione esatta rende la build riproducibile e trasforma ogni aggiornamento in un commit `chore(deps)` esplicito.
Alternative: `0.1.0-preview.*` (si aggiorna da solo, ma ogni restore può portare una rottura non voluta); feed nightly `dotnet10` di maui-labs (versioni più fresche, ma richiede un `NuGet.config` e non è necessario).
Vedi `my-docs/docs/regole/versionamento.md`.

**2. Rimuovere il pacchetto Essentials invece di migrarlo.**
L'app non registra né usa servizi Essentials: il selettore file è `Gtk.FileDialog` diretto e la UI si aggiorna con `Dispatcher.Dispatch()`. Un pacchetto non usato è superficie di aggiornamento senza beneficio.
Alternativa: migrare anche `Microsoft.Maui.Platforms.Linux.Gtk4.Essentials` e chiamare `AddLinuxGtk4Essentials()`. Si farà quando servirà un servizio concreto (es. `Preferences`, `Clipboard`).
Vedi `my-docs/docs/regole/principi.md`.

**3. Solo sostituzione di namespace nel codice, nessun adattamento preventivo.**
Le API usate esistono con lo stesso nome e la stessa firma. Si cambiano gli `using` e i riferimenti nei commenti, senza toccare la logica dei workaround.
Alternativa: riscrivere i workaround in modo più robusto approfittando della migrazione. Scartata: mescolerebbe due intenti (dipendenza e comportamento) nello stesso cambiamento.

**4. Mantenere `UseMaui=true` e `MSBuildEnableWorkloadResolver=false`.**
Il template ufficiale non usa `UseMaui` perché non ha XAML; PWS compila le pagine XAML e ha bisogno del target di compilazione XAML di `Microsoft.Maui.Controls`. Si mantiene la configurazione attuale e la si cambia solo se la build lo richiede. Se la build fallisce per il workload, l'alternativa è togliere `UseMaui` e verificare che `MauiXaml` venga comunque compilato dai target del pacchetto `Microsoft.Maui.Controls`.

**5. Verifica: CI per la compilazione, prova manuale per il comportamento.**
La CI (`build-dotnet`) garantisce che la solution compili con le librerie GTK4 e WebKitGTK 6. Gli scenari della spec vanno provati a mano su un desktop Linux, con particolare attenzione al resize dopo l'apertura di un archivio. I test di integrazione esistenti devono restare verdi come controllo che il resto della solution non sia toccato.

## Risks / Trade-offs

- [API cambiate in una preview futura] → versione esatta; ogni aggiornamento è un commit dedicato con prova manuale.
- [Differenze di comportamento non coperte da test automatici, es. layout, stili CSS dei widget, WebView] → prova manuale guidata dagli scenari della spec prima di chiudere la change.
- [Il workaround di resize smette di funzionare perché il `LayoutHandler` è cambiato internamente] → la firma di `GtkLayoutPanel.CrossPlatformMeasure/Arrange` è verificata identica; lo scenario «Resize dopo la navigazione tra pagine» lo controlla.
- [Build fallisce per `UseMaui` o per il workload] → decisione 4, con alternativa già individuata.
- [`MainThread.BeginInvokeOnMainThread` resta non affidabile] → la convenzione di usare `Dispatcher.Dispatch()` in `CLAUDE.md` resta valida; si aggiorna solo il nome del pacchetto citato.

## Migration Plan

1. Un solo commit `chore(deps)` con csproj, namespace, commenti e documentazione.
2. Push su `main`, verifica della CI.
3. Prova manuale degli scenari della spec.
4. Rollback: revert del commit, che riporta a `Platform.Maui.Linux.Gtk4` 0.6.0 senza altri effetti.

## Open Questions

- Segnalare il bug di resize del `LayoutHandler` come issue su `dotnet/maui-labs`, così da poter rimuovere i workaround in una change futura.
