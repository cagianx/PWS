---
sidebar_position: 3
---

# PwsReader — Leggere archivi .pws

`PwsReader` apre un `.pws`, verifica i JWT di ogni sito e espone un filesystem virtuale.

## Apertura base

```csharp
using PWS.Format.Reading;

// La chiave di verifica viene letta automaticamente dal manifest.json
using var reader = await PwsReader.OpenAsync("docs.pws");

// Informazioni sul manifest
Console.WriteLine($"Versione: {reader.Manifest.Version}");
Console.WriteLine($"Siti: {reader.Manifest.Sites.Count}");

// Claims verificati per ogni sito
foreach (var site in reader.Sites)
{
    Console.WriteLine($"  [{site.SiteId}] {site.Title}");
    Console.WriteLine($"    Entry:    {site.EntryPoint}");
    Console.WriteLine($"    Hash:     {site.ContentHash}");
    Console.WriteLine($"    File:     {site.FileCount}");
    Console.WriteLine($"    Firmato:  {site.IsVerified}");
}
```

## Verifica dell'integrità dei file (tampering detection)

`PwsReader` esegue **due livelli di verifica** ad ogni apertura:

```
1. Firma JWT
   └─ verifica che il token non sia stato alterato
      (protegge metadati + hash)

2. Content hash (sempre attivo)
   └─ ricalcola il Merkle hash dai byte reali nel ZIP
   └─ lo confronta con pws:hash nel JWT
   └─ qualsiasi modifica a qualsiasi file → InvalidDataException
```

Questo significa che anche se un attaccante riuscisse a bypassare la firma
(ad esempio su archivi non firmati), qualsiasi modifica al contenuto dei file
viene comunque rilevata dal confronto dell'hash.

### Scenario: file modificato dopo il packing

```
archivio.pws
  └─ sites/docs/index.html  ← qualcuno ne modifica il contenuto

PwsReader.OpenAsync("archivio.pws")
  → verifica JWT:         OK  (token invariato)
  → ricalcola hash file:  sha256:AABB...  ≠  sha256:1234... (nel JWT)
  → InvalidDataException: "Site 'docs': content hash mismatch"
```

### Scenario: file aggiunto/iniettato

```
archivio.pws
  └─ sites/docs/inject.html  ← file extra inserito

PwsReader.OpenAsync("archivio.pws")
  → ricalcola hash (ora include inject.html): sha256:CCDD... ≠ sha256:1234...
  → InvalidDataException
```

:::info
La verifica del content hash è **incondizionata**: viene eseguita sempre,
indipendentemente dal fatto che l'archivio sia firmato o meno.
:::

## Filesystem virtuale — listare i file

```csharp
using var reader = await PwsReader.OpenAsync("monorepo.pws");
var fs = reader.FileSystem;

// Tutti i file di tutti i siti
var all = fs.ListFiles();

// Solo il sito "docs"
var docsFiles = fs.ListFiles(siteId: "docs");

foreach (var entry in docsFiles)
{
    Console.WriteLine($"{entry.RelativePath}  ({entry.Size} bytes)");
    // es: index.html  (4321 bytes)
    //     assets/main.css  (8765 bytes)
}
```

## Filesystem virtuale — leggere un file

```csharp
// Per path archivio completo
using var stream = fs.OpenFile("sites/docs/index.html");

// Per sito + path relativo (metodo preferito)
using var stream2 = fs.OpenSiteFile("docs", "assets/main.css");

var html = await new StreamReader(stream).ReadToEndAsync();
```

## Verifica obbligatoria (produzione)

```csharp
using var reader = await PwsReader.OpenAsync("docs.pws", new PwsOpenOptions
{
    RequireSignedTokens = true,  // lancia se il token è alg:none
});
```

## Override della chiave di verifica

```csharp
var myKey = PwsSigningKey.FromHmac("super-secret-key");

using var reader = await PwsReader.OpenAsync("docs.pws", new PwsOpenOptions
{
    VerificationKey     = myKey,
    RequireSignedTokens = true,
});
```

## Aprire da stream (senza file system)

```csharp
byte[] archiveBytes = /* da database, HTTP, ecc. */;
using var ms     = new MemoryStream(archiveBytes);
using var reader = await PwsReader.OpenAsync(ms);
```

## Errori

| Eccezione | Causa |
|-----------|-------|
| `InvalidDataException` | `manifest.json` mancante, malformato o JWT non valido |
| `InvalidDataException` | Token unsigned con `RequireSignedTokens = true` |
| `InvalidDataException` | Content hash mismatch — file alterati dopo il packing |
| `FileNotFoundException` | Path non trovato nel filesystem virtuale |
| `KeyNotFoundException` | Site ID non dichiarato nel manifest |

