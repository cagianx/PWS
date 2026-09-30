## Why

L'app Linux dipende da `Platform.Maui.Linux.Gtk4` 0.6.0, pubblicato da Redth/Maui.Gtk. Quel repository è stato spostato in `dotnet/maui-labs` (`platforms/Linux.Gtk4`) e non riceverà altri aggiornamenti. Restare sul pacchetto vecchio significa niente correzioni, niente allineamento alle nuove versioni di MAUI 10 e un debito che cresce a ogni release del backend ufficiale.

## What Changes

- Sostituzione del pacchetto `Platform.Maui.Linux.Gtk4` 0.6.0 con `Microsoft.Maui.Platforms.Linux.Gtk4`, versione preview fissata in modo esatto (oggi `0.1.0-preview.12.26421.1`).
- Rimozione di `Platform.Maui.Linux.Gtk4.Essentials`: l'app non registra né usa alcun servizio Essentials (il file picker è `Gtk.FileDialog` diretto, gli aggiornamenti della UI passano da `Dispatcher`).
- Aggiornamento dei namespace da `Platform.Maui.Linux.Gtk4.*` a `Microsoft.Maui.Platforms.Linux.Gtk4.*` in `Program.cs`, `MauiProgram.cs` e `BrowserPage.xaml.cs`. Le API usate (`GtkMauiApplication`, `UseMauiAppLinuxGtk4<App>()`, `GtkLayoutPanel`) mantengono lo stesso nome.
- Mantenimento dei workaround per il bug di resize di GTK4: nel nuovo backend il `LayoutHandler` aggancia ancora una lambda a `GtkWindow.OnNotify` che non viene mai sganciata e legge `GetAllocatedWidth/Height()` non aggiornati. Si aggiornano solo i commenti che citano il vecchio pacchetto.
- Aggiornamento di `CLAUDE.md`, `README.md` e della documentazione in `docs/docs/` che citano il pacchetto e la versione.
- Nessun cambiamento funzionale visibile all'utente.

## Capabilities

### New Capabilities
- `app-linux/backend-gtk4`: backend di piattaforma dell'app Linux. Fissa la sorgente del backend (pacchetto mantenuto, versione esatta) e i comportamenti della finestra che la migrazione deve preservare: avvio, apertura di un archivio, navigazione tra pagine, ridimensionamento.

### Modified Capabilities
(nessuna: non esistono ancora spec nel progetto)

## Impact

- **Codice**: `src/PWS.App.Linux/PWS.App.Linux.csproj`, `Program.cs`, `MauiProgram.cs`, `Pages/BrowserPage.xaml.cs`, commenti in `Pages/StartupPage.xaml.cs`.
- **Dipendenze**: `Microsoft.Maui.Controls` passa a 10.0.41 in modo transitivo, GirCore a 0.7.0. Il pacchetto è sperimentale: le API possono cambiare tra una preview e l'altra.
- **Build e CI**: nessun cambiamento atteso al job `build-dotnet` (stesse librerie di sistema GTK4 e WebKitGTK 6).
- **Non toccati**: `PWS.Core`, `PWS.Format`, `PWS.Tool` e i test di integrazione, che non dipendono da MAUI.
- **Verifica**: la UI non è coperta da test automatici; la migrazione richiede una prova manuale sull'app.
