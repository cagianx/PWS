---
sidebar_position: 3
description: Suite di test del progetto, cosa verifica ciascuna e come eseguirle.
---

# Test

Il progetto ha tre suite di test. Nessuna richiede un display né le librerie native GTK4 o
WebKitGTK: girano su qualunque macchina Linux con il .NET SDK.

| Progetto | Framework | Cosa verifica |
|----------|-----------|---------------|
| `PWS.Format.Tests` | xUnit | formato `.pws`: JWT, hash Merkle, chiavi di firma, packer e reader, rilevamento delle manomissioni, archivi e input non validi, pacchettizzazione della documentazione reale |
| `PWS.IntegrationTests` | NUnit + FluentAssertions | il sistema attraverso i suoi confini reali: archivi su disco, socket HTTP su loopback, comandi e processo della CLI |
| `PWS.App.Linux.Tests` | NUnit + FluentAssertions | lo strato dell'app che non tocca GTK: `BrowserViewModel` e `PwsFileService` |

```bash
dotnet test src/PWS.Format.Tests/PWS.Format.Tests.csproj
dotnet test src/PWS.IntegrationTests/PWS.IntegrationTests.csproj
dotnet test src/PWS.App.Linux.Tests/PWS.App.Linux.Tests.csproj
```

## Test di integrazione

Seguono le regole di [testing di MyDocs](https://github.com/cagianx/my-docs/blob/main/docs/regole/testing.md)
(nel repository: `my-docs/docs/regole/testing.md`): nessun mock dei confini del sistema, un test
per ogni comportamento significativo, verifica esplicita che le operazioni invalide vengano rifiutate.

Ogni test crea gli archivi `.pws` con `PwsPacker` in una directory temporanea propria, cancellata
a fine test. I server HTTP ascoltano su `127.0.0.1` su una porta libera, quindi le fixture possono
girare in parallelo.

| Area | Classe di test | Confine reale attraversato |
|------|----------------|----------------------------|
| `PwsContentProvider` | `Providers/PwsContentProviderTests` | archivio `.pws` su disco |
| `InMemoryContentProvider` | `Providers/InMemoryContentProviderTests` | pagine integrate e registrate |
| `CompositeContentProvider` | `Providers/CompositeContentProviderTests` | più sorgenti di contenuto reali |
| `ApiContentProvider` | `Providers/ApiContentProviderTests` | server HTTP locale |
| `NavigationService` con provider composito | `Navigation/NavigationServiceTests` | archivio `.pws` + pagine in memoria |
| `LoopbackContentServer` | `Hosting/LoopbackContentServerTests` | client HTTP e socket grezzo verso il server loopback |
| `pwstool pack` | `Tool/PackCommandTests` | directory e zip sorgente, archivio prodotto |
| `pwstool validate` | `Tool/ValidateCommandTests` | archivi integri, firmati, manomessi, corrotti |
| Riga di comando di `pwstool` | `Tool/CommandLineTests` | processo `dotnet PWS.Tool.dll`, output e codice di uscita |
| Ciclo di vita completo | `EndToEnd/ArchiveLifecycleTests` | CLI → reader → provider → server → HTTP |

### Robustezza

Una parte dei test verifica che il sistema non si rompa con input anomali:

- percorsi con spazi, lettere accentate o `%` vengono decodificati una sola volta, sia su `pws://`
  sia sul server loopback;
- i segmenti `..`, anche codificati come `%2e%2e`, non servono mai file di un altro sito. Sul
  server loopback la richiesta viene inviata su un socket grezzo, perché `HttpClient` normalizzerebbe
  il percorso prima di spedirlo;
- il reader rifiuta con `InvalidDataException` manifest non JSON, vuoti o con token vuoti;
- il packer rifiuta, senza creare il file, id di sito duplicati o non validi come host di un URI.

## Test dell'app

`PWS.App.Linux.Tests` referenzia l'app, quindi porta con sé MAUI e i binding GirCore, ma nessun test
istanzia tipi GTK: le librerie native non vengono mai caricate. Il job CI `test-app` gira su un
runner senza GTK né WebKitGTK installati, così un test che le caricasse fallirebbe subito.

| Area | Classe di test | Tipo |
|------|----------------|------|
| `BrowserViewModel` | `ViewModels/BrowserViewModelTests` | unitario: validazione degli URL, stato di navigazione, comandi, eventi verso la View, notifiche di proprietà |
| `PwsFileService` | `Services/PwsFileServiceTests` | integrazione: archivio reale servito via HTTP loopback, sostituzione dell'archivio, `Dispose` |

Le basi comuni (`ArchiveTestBase`, `SampleSite`) sono quelle di `PWS.IntegrationTests`, incluse per link.

## Cosa resta manuale

Le pagine XAML (`StartupPage`, `BrowserPage`, `ErrorPage`), la finestra di errore e il selettore file
nativo richiedono GTK e un display. Si verificano a mano seguendo gli scenari della spec
`openspec/specs/app-linux/backend-gtk4/spec.md`: avvio, apertura di un archivio, selezione annullata,
file non valido, link interni, ridimensionamento.
