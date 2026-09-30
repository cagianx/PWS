## Why

Le due suite esistenti (`PWS.Format.Tests`, `PWS.IntegrationTests`) coprono bene formato `.pws`, `PwsContentProvider`, `ApiContentProvider`, `LoopbackContentServer`, `NavigationService` e i comandi di `pwstool`. Restano però scoperti componenti con logica reale: due provider di `PWS.Core`, i casi di errore del reader e del packer, l'entry point della CLI e tutto lo strato non grafico dell'app (`BrowserViewModel`, `PwsFileService`). Per `my-docs/docs/regole/testing.md` un blocco di logica senza test è una bozza, e il crash di oggi su WebKitGTK ha mostrato quanto poco dell'app sia verificato in automatico.

## What Changes

- **Test di `PWS.Core` mancanti** in `PWS.IntegrationTests`:
  - `InMemoryContentProvider`: rotte predefinite (`pws://home`, `pws://about`), registrazione statica e dinamica, sostituzione di una rotta, confronto senza distinzione di maiuscole, `/` finale, 404 su rotta sconosciuta, schemi gestiti.
  - `CompositeContentProvider`: delega al primo provider capace nell'ordine dato, `CanHandle` falso senza provider capaci, errore esplicito in `GetAsync` senza provider.
  - Robustezza dei percorsi in `PwsContentProvider` e `LoopbackContentServer`: nomi di file con spazi o caratteri non ASCII (percent-encoding), segmenti `..` che non devono uscire dal sito, host del sito senza distinzione di maiuscole, `pws:///` senza sito di default, MIME per le estensioni servite.
- **Test di `PWS.Format` mancanti** in `PWS.Format.Tests`: reader con `manifest.json` malformato o `null`, sito dichiarato nel manifest senza token o senza file; packer senza siti e con id di sito duplicati o non validi come host di URI.
- **Test dell'entry point di `pwstool`**: nessun argomento, verbo sconosciuto, opzioni obbligatorie mancanti, codici di uscita.
- **Nuovo progetto `PWS.App.Linux.Tests`** per lo strato dell'app che non tocca GTK:
  - `BrowserViewModel`: validazione degli URL, navigazione al sito corrente, stato durante e dopo la navigazione, abilitazione dei comandi, eventi verso la View, notifiche di proprietà.
  - `PwsFileService`: avvio del server loopback sul sito aperto, sostituzione del sito con rilascio di server e provider precedenti, evento `FileOpened`, `Dispose`.
- **Nuovo job CI** che esegue `PWS.App.Linux.Tests`.
- **Correzioni emerse dai test**: dove un test descrive il comportamento atteso e il codice non lo rispetta (candidati: percorsi percent-encoded, id di sito duplicati o non validi, manifest `null` o malformato), la correzione entra nella change come commit `fix` separato.
- **Documentazione**: `docs/docs/getting-started/testing.md` descrive la nuova suite e le nuove aree coperte.

Fuori ambito, perché non automatizzabili senza display o senza valore:

- pagine XAML (`StartupPage`, `BrowserPage`, `ErrorPage`), `ErrorDialogService`, `GtkPwsArchivePicker`: richiedono GTK e un display; restano coperte dagli scenari manuali di `app-linux/backend-gtk4`;
- `DynamicCompositeContentProvider` e la registrazione di `INavigationService` in `MauiProgram.cs`: non sono usati dall'app, che naviga via HTTP loopback. Vanno rimossi in una change di pulizia, non testati;
- `CreateTestPws` (utility) e modelli banali (`ContentRequest`, `NavigationEntry`).

## Capabilities

### New Capabilities
- `qualita/test-automatici`: cosa la suite di test automatici garantisce per ciascun componente non grafico (provider, server loopback, formato, CLI, ViewModel e servizi dell'app) e quali comportamenti di robustezza certifica.

### Modified Capabilities
(nessuna: `app-linux/backend-gtk4` descrive comportamenti verificati a mano e non cambia)

## Impact

- **Test**: nuove classi in `src/PWS.IntegrationTests/` e `src/PWS.Format.Tests/`, nuovo progetto `src/PWS.App.Linux.Tests/` aggiunto a `PWS.slnx`.
- **Codice di produzione**: nessuna modifica prevista, salvo le correzioni che i test fanno emergere (commit `fix` dedicati) e l'eventuale `InternalsVisibleTo` se servisse.
- **CI**: nuovo job in `.github/workflows/ci.yml`; il progetto referenzia l'app, quindi porta con sé i pacchetti MAUI e GirCore, ma non carica librerie native a runtime.
- **Documentazione**: `docs/docs/getting-started/testing.md`, tabella in `CLAUDE.md` se cambia la struttura dei progetti.
