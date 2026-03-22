---
sidebar_position: 4
---

# PwsContentProvider

Provider che serve i file di un archivio `.pws` attraverso lo schema URI `pws://`.
È il provider **principale in produzione**: legge i contenuti dall'archivio ZIP
aperto tramite `PwsReader` e li espone alla WebView senza mai accedere al filesystem
del sistema operativo host durante la navigazione.

## Schema URI

```
pws://{siteId}/{percorso/relativo}
```

| Parte URI | Significato |
|-----------|-------------|
| `siteId`  | ID del sito dichiarato nel `manifest.json` del `.pws` |
| `percorso/relativo` | Percorso del file all'interno della cartella del sito |

Esempio: `pws://docs/assets/main.css` → entry ZIP `sites/docs/assets/main.css`.

## Cache in-memoria

Per evitare di decomprimere la stessa entry ZIP più volte, `PwsContentProvider`
mantiene una **cache in-memoria** basata su `byte[]`.

| Parametro | Valore |
|-----------|--------|
| Limite massimo complessivo | **5 MB** (`PwsContentProvider.MaxCacheBytes`) |
| Strategia di eviction | Nessuna — quando il budget è esaurito il file viene servito direttamente dallo zip (bypass silenzioso) |
| Chiave | `{siteId}/{percorso/relativo}` (case-insensitive) |

### Flusso per ogni richiesta

```
GetAsync(uri)
  │
  ├─▶ cache HIT? ──────yes──▶ MemoryStream(bytes[]) → ContentResponse 200
  │
  ├─ NO: leggi da zip → byte[]
  │
  ├─▶ usedBytes + fileSize ≤ 5 MB? ──yes──▶ inserisci in cache
  │                                   no──▶ bypass (solo questa volta)
  │
  └─▶ MemoryStream(bytes[]) → ContentResponse 200
```

La cache viene **svuotata automaticamente** al `Dispose()` del provider
(cioè quando si carica un nuovo file `.pws`).

## Utilizzo

```csharp
// Apertura archivio
var reader = await PwsReader.OpenAsync("/path/to/archive.pws");

// Creazione provider (prende ownership del reader)
var provider = new PwsContentProvider(reader);

// Navigazione
var response = await provider.GetAsync(
    new ContentRequest { Uri = new Uri("pws://docs/index.html") });

// Pulizia (rilascia reader + cache)
provider.Dispose();
```

### DefaultSiteId

Se il `.pws` contiene un **solo sito** e l'URI non specifica l'host
(`pws:///index.html`), il provider usa il sito unico come default.

```csharp
// Auto-detect: funziona solo se c'è un unico sito nel manifest
var provider = new PwsContentProvider(reader);

// Override esplicito
var provider = new PwsContentProvider(reader, defaultSiteId: "docs");
```

## API

```csharp
// Costante — limite cache in-memoria
public const long MaxCacheBytes = 5 * 1024 * 1024; // 5 MB

// Costruttore
PwsContentProvider(
    PwsReader reader,
    string? defaultSiteId = null,
    ILogger<PwsContentProvider>? logger = null)

// IContentProvider
bool CanHandle(Uri uri)
Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken ct)

// IDisposable
void Dispose()   // rilascia reader e svuota la cache
```

## Registrazione in MauiProgram.cs

`PwsContentProvider` **non** viene registrato direttamente nella DI globale:
viene creato da `PwsFileService` ogni volta che l'utente apre un nuovo
archivio e passato al `CompositeContentProvider` attivo.

```csharp
// Services/PwsFileService.cs
public void SetProvider(PwsContentProvider provider)
{
    _current?.Dispose();     // dispose vecchio provider (→ svuota cache)
    _current = provider;
    _composite.Replace(provider);
}
```

