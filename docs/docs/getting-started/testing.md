---
sidebar_position: 3
description: Suite di test del progetto, cosa verifica ciascuna e come eseguirle.
---

# Test

Il progetto ha due suite di test, entrambe senza dipendenze da MAUI o GTK: girano su qualunque
macchina Linux con il .NET SDK, senza librerie di sistema aggiuntive.

| Progetto | Framework | Cosa verifica |
|----------|-----------|---------------|
| `PWS.Format.Tests` | xUnit | formato `.pws`: JWT, hash Merkle, chiavi di firma, packer e reader, rilevamento delle manomissioni, pacchettizzazione della documentazione reale |
| `PWS.IntegrationTests` | NUnit + FluentAssertions | il sistema attraverso i suoi confini reali: archivi su disco, socket HTTP su loopback, comandi della CLI |

```bash
dotnet test src/PWS.Format.Tests/PWS.Format.Tests.csproj
dotnet test src/PWS.IntegrationTests/PWS.IntegrationTests.csproj
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
| `ApiContentProvider` | `Providers/ApiContentProviderTests` | server HTTP locale |
| `NavigationService` con provider composito | `Navigation/NavigationServiceTests` | archivio `.pws` + pagine in memoria |
| `LoopbackContentServer` | `Hosting/LoopbackContentServerTests` | client HTTP verso il server loopback |
| `pwstool pack` | `Tool/PackCommandTests` | directory e zip sorgente, archivio prodotto |
| `pwstool validate` | `Tool/ValidateCommandTests` | archivi integri, firmati, manomessi, corrotti |
| Ciclo di vita completo | `EndToEnd/ArchiveLifecycleTests` | CLI → reader → provider → server → HTTP |

La UI (`PWS.App.Linux`) non è coperta da questi test: richiede un display e il backend GTK4.
