# Design

## Context

Motivazione e ambito in `proposal.md`, comportamenti da certificare in `specs/qualita/test-automatici/spec.md`. Stato verificato sul codice:

- `PWS.IntegrationTests` (NUnit + FluentAssertions) referenzia `PWS.Format`, `PWS.Core` e `PWS.Tool`. Ha già le basi comuni `Common/ArchiveTestBase`, `SampleSite` e `TestHttpServer`: archivi creati con `PwsPacker` in una directory temporanea, server su porta libera, test paralleli.
- `PWS.Format.Tests` (xUnit) copre formato, crittografia, packer e reader in `PackerReaderTests`.
- I test di `pwstool` chiamano direttamente `PackCommand.RunAsync` e `ValidateCommand.RunAsync`: l'entry point (`Program.cs`, top-level statements con CommandLineParser) non è mai esercitato.
- `BrowserViewModel` usa `Microsoft.Maui.Controls.Command`, quindi dipende da MAUI Controls (in contrasto con la convenzione di `CLAUDE.md`, che dice il contrario). `PwsFileService` dipende solo da `PWS.Core` e dal logging. Nessuno dei due tocca tipi GTK.
- `PwsContentProvider.ParseUri` e `LoopbackContentServer` usano `Uri.AbsolutePath` senza decodificarlo: un file con spazi o accenti probabilmente risponde 404.
- `PwsPacker` controlla solo che ci sia almeno un sito; non verifica id duplicati né la validità dell'id come host.
- `PwsReader` deserializza il manifest con `JsonSerializer`: un JSON malformato probabilmente esce come `JsonException` e non come `InvalidDataException`.

## Goals / Non-Goals

**Goals:**
- Ogni requirement della spec ha almeno un test automatico che gira in CI senza display.
- Dove un test mostra che il codice non rispetta la spec, il codice viene corretto nella stessa change.

**Non-Goals:**
- Test della UI GTK (pagine, dialoghi, selettore file), neanche con display virtuale.
- Rimuovere il codice non usato (`DynamicCompositeContentProvider`, `INavigationService` nell'app) o togliere `Command` di MAUI dal ViewModel: sono refactoring separati.
- Soglie di coverage: si copre ogni comportamento della spec, non ogni riga.

## Decisions

**1. Le nuove aree seguono la suite che già copre il componente.**
Provider di `PWS.Core`, robustezza dei percorsi e riga di comando vanno in `PWS.IntegrationTests` (confini reali: archivi su disco, HTTP, processo). I casi di errore di reader e packer vanno in `PWS.Format.Tests`, accanto a `PackerReaderTests`. Si riusano `ArchiveTestBase` e `SampleSite` invece di creare nuove fixture.
Alternativa: una suite nuova per i test di robustezza. Scartata: duplicherebbe fixture già esistenti.
Vedi `my-docs/docs/regole/testing.md` («I test sono codice di produzione», nessuna logica duplicata).

**2. Un progetto `PWS.App.Linux.Tests` separato che referenzia l'app.**
`BrowserViewModel` e `PwsFileService` vivono nell'app, che porta con sé MAUI e GirCore. Un progetto dedicato lascia intatta la proprietà documentata delle due suite esistenti («nessuna dipendenza da MAUI o GTK»). Il progetto usa NUnit + FluentAssertions come `PWS.IntegrationTests` e ne include per link i file di `Common/` necessari (`ArchiveTestBase`, `SampleSite`).
Alternative: referenziare l'app da `PWS.IntegrationTests` (la contamina con MAUI); spostare ViewModel e servizi in una libreria senza MAUI (refactoring fuori ambito, andrebbe fatto prima togliendo `Command`).
Il primo task è una verifica: se referenziare un progetto `Exe` con `UseMaui=true` da un progetto di test non compila, oppure a runtime carica librerie native, si ripiega su una libreria `PWS.App.Presentation` e si aggiorna questa decisione.
Vedi `my-docs/docs/tecnologie/csharp/struttura-soluzione/`.

**3. `PwsFileService` si testa con archivi e HTTP reali.**
Nessun mock: si apre un archivio creato con `PwsPacker`, si chiama `SetProvider` e si interroga il server loopback con `HttpClient`. La sostituzione si verifica aprendo un secondo archivio e controllando che il vecchio indirizzo non risponda più.
Vedi `my-docs/docs/regole/testing.md` (test di integrazione sui confini reali).

**4. `BrowserViewModel` si testa con test unitari.**
È logica di stato senza confini esterni: validazione degli URL, transizioni di stato, `CanExecute` dei comandi, eventi. Test parametrici per gli input rifiutati; nomi che descrivono il comportamento, come nelle suite esistenti.
Vedi `my-docs/docs/tecnologie/csharp/test-unitari/01-scopo.md` (logica pura) e `02-come-scriverli.md` («Il nome è la specifica», test parametrici).

**5. L'entry point di `pwstool` si testa lanciando il processo.**
I test eseguono `dotnet <output>/PWS.Tool.dll` con `Process`, leggono stdout e stderr e controllano il codice di uscita. È il vero confine della CLI, non richiede di catturare `Console` (che non è sicuro con i test paralleli) e non dipende dai nomi generati dai top-level statements.
Alternativa: invocare `Assembly.EntryPoint` via reflection con `Console.SetOut`. Scartata: obbliga a rendere i test non paralleli.

**6. Correzioni: il test e il fix nello stesso commit `fix`.**
I test che passano subito entrano in commit `test(...)`. Un test che fallisce perché il codice non rispetta la spec entra nel commit `fix(...)` che corregge il codice: nessun commit lascia la CI rossa e ogni fix porta il test che lo dimostra.
Correzioni attese, da confermare con i test:
- percorsi: decodificare il percorso (`Uri.UnescapeDataString`) una sola volta, in `PwsContentProvider.ParseUri`, così vale sia per `pws://` sia per il loopback, che gli passa il percorso ancora codificato;
- packer: rifiutare id duplicati (senza distinzione di maiuscole) e id che non sono un'etichetta DNS (`[A-Za-z0-9]`, `-` interno, massimo 63 caratteri), con `ArgumentException`;
- reader: convertire `JsonException` e manifest `null` in `InvalidDataException`.
Vedi `my-docs/docs/regole/gestione-errori.md` e `my-docs/docs/tecnologie/git/index.md` (un commit, un intento).

**7. Nuovo job CI `test-app`.**
Esegue `dotnet test` su `PWS.App.Linux.Tests` su `ubuntu-24.04` senza installare GTK né WebKitGTK: se un test caricasse librerie native fallirebbe, e questo è il controllo del requirement «Suite eseguibile senza display».

## Risks / Trade-offs

- [Il progetto di test non riesce a referenziare l'app MAUI] → verifica come primo task, alternativa già definita nella decisione 2.
- [`Command` di MAUI richiede un dispatcher o un'app inizializzata] → `Command.ChangeCanExecute` solleva `CanExecuteChanged` in modo sincrono; se servisse un `Application`, la verifica del primo task lo mostra e si ripiega come nella decisione 2.
- [La validazione degli id nel packer rifiuta archivi che prima si creavano] → oggi un id non valido come host produce comunque un archivio non navigabile via `pws://`; il cambiamento va documentato nella pagina della CLI.
- [Test HTTP instabili per porte o tempi] → porte libere assegnate dal sistema come nelle suite esistenti, nessuna attesa a tempo fisso.
- [Decodifica doppia di `%25`] → la decodifica avviene in un solo punto; un test con `%2520` nel nome lo verifica.

## Migration Plan

Nessuna migrazione: si aggiungono test e un job CI. Rollback: revert dei commit della change.
