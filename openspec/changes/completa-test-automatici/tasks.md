# Tasks

## 1. Provider di PWS.Core

- [x] 1.1 Aggiungere `Providers/InMemoryContentProviderTests` in `PWS.IntegrationTests`: rotte predefinite `home` e `about` (200, `text/html`, titolo), registrazione statica e dinamica, sostituzione di una rotta, `pws://HOME/`, 404 su rotta sconosciuta, `CanHandle` con schemi di default e personalizzati. Verifica: `dotnet test src/PWS.IntegrationTests` verde con i nuovi test.
- [x] 1.2 Aggiungere `Providers/CompositeContentProviderTests`: delega alla prima sorgente capace nell'ordine dato, `CanHandle` falso senza sorgenti capaci, `GetAsync` senza sorgenti capaci solleva `InvalidOperationException`. Verifica: suite verde.
- [x] 1.3 Commit `test(providers): cover in-memory and composite content providers`.

## 2. Robustezza dei percorsi dei siti

- [x] 2.1 Aggiungere a `PwsContentProviderTests` i casi della spec: host con maiuscole, `pws:///` con più siti e senza sito di default non gestibile, MIME per `.html`, `.css`, `.js`, `.svg`, `.woff2`, `.bin` (test parametrico), segmenti `..` che non restituiscono file di un altro sito. Verifica: suite verde, oppure test rossi annotati per il task 2.3.
- [x] 2.2 Aggiungere a `PwsContentProviderTests` e `LoopbackContentServerTests` il file `pagine/città vecchia.html` richiesto con percorso codificato, e un file con `%25` nel nome per escludere la doppia decodifica. Verifica: test eseguiti; se rossi, si procede con 2.3.
- [x] 2.3 Se 2.1 o 2.2 hanno test rossi: decodificare il percorso una sola volta in `PwsContentProvider.ParseUri` (decisione 6 di `design.md`) e correggere gli altri casi emersi. Verifica: suite verde. Commit `fix(providers): ...` con i test che dimostrano il fix; i test già verdi vanno in un commit `test(providers): ...` separato.

## 3. Casi di errore di PWS.Format

- [x] 3.1 Aggiungere a `PWS.Format.Tests` i test del reader: `manifest.json` non JSON, `manifest.json` con `null`, sito con token vuoto. Tutti devono fallire con `InvalidDataException`. Verifica: `dotnet test src/PWS.Format.Tests`; se `JsonException` esce non convertita, correggere `PwsReader` (decisione 6) e fare un commit `fix(format): ...` con i test.
- [x] 3.2 Aggiungere i test del packer: nessun sito, id duplicati `docs` e `Docs`, id `mio sito`. Tutti devono fallire con `ArgumentException` senza scrivere l'archivio. Verifica: suite verde dopo l'aggiunta della validazione in `PwsPacker` (decisione 6); commit `fix(format): reject duplicate or invalid site ids when packing` con i test.
- [x] 3.3 Aggiungere a `ValidateCommandTests` un archivio con manifest malformato che `pwstool validate` rifiuta con codice diverso da 0, così il fix di 3.1 è verificato anche dalla CLI. Verifica: suite `PWS.IntegrationTests` verde.
- [x] 3.4 Documentare in `docs/docs/cli/` (pagina di `pack`) le regole sugli id di sito e in `docs/docs/format/` il rifiuto dei manifest malformati. Verifica: `cd docs && pnpm build` → `[SUCCESS]`.

## 4. Riga di comando di pwstool

- [x] 4.1 Aggiungere `Tool/CommandLineTests`, che lancia `dotnet PWS.Tool.dll` come processo (decisione 5): nessun argomento → aiuto e codice 1; verbo `unpack` → messaggio di verbo sconosciuto e codice 1; `pack` senza sorgente → codice diverso da 0 e nessun archivio creato. Verifica: suite verde. Commit `test(tool): cover the pwstool command line entry point`.

## 5. Strato non grafico dell'app

- [ ] 5.1 Creare `src/PWS.App.Linux.Tests` (NUnit + FluentAssertions, stesse versioni di `PWS.IntegrationTests`) che referenzia `PWS.App.Linux` e include per link `Common/ArchiveTestBase.cs` e `Common/SampleSite.cs`; aggiungerlo a `PWS.slnx`. Verifica: `dotnet test src/PWS.App.Linux.Tests` compila ed esegue un test di prova senza GTK. Se fallisce, applicare l'alternativa della decisione 2 e annotarla in `design.md` prima di proseguire.
- [ ] 5.2 Aggiungere `BrowserViewModelTests`: input rifiutati (vuoto, relativo, `pws://`, parametrico) con messaggio di stato e `RenderedUrl` invariato; URL `http` accettato; `NavigateToCurrentSite` con e senza sito aperto; `OnPageNavigating` e `OnWebViewNavigated`; `CanExecute` di indietro, avanti e stop; eventi `GoBackRequested`, `GoForwardRequested`, `ReloadRequested`; `PropertyChanged` per le proprietà modificate. Verifica: suite verde.
- [ ] 5.3 Aggiungere `PwsFileServiceTests` con archivi e HTTP reali: il primo archivio viene servito dall'indirizzo loopback; al secondo archivio il nuovo indirizzo serve il nuovo sito e il vecchio rifiuta la connessione; `FileOpened` sollevato con il provider; dopo `Dispose` l'indirizzo non risponde. Verifica: suite verde.
- [ ] 5.4 Aggiungere il job `test-app` a `.github/workflows/ci.yml`, senza installare GTK né WebKitGTK (decisione 7). Verifica: YAML valido e job presente nei `needs` di eventuali job aggregati.
- [ ] 5.5 Aggiornare `docs/docs/getting-started/testing.md` (terza suite, nuove aree nella tabella, cosa resta manuale) e la sezione Struttura di `CLAUDE.md`. Verifica: `cd docs && pnpm build` → `[SUCCESS]`. Commit `test(app): cover browser view model and loopback file service`.

## 6. Verifica integrata

- [ ] 6.1 Eseguire la verifica pre-commit di `CLAUDE.md`: build dell'app con 0 errori, `dotnet test` verde su tutte e tre le suite, `pnpm build` riuscito. Verifica: output dei comandi.
- [ ] 6.2 Push e controllo che la CI sia verde in tutti i job, compreso `test-app`. Verifica: esito della run.
