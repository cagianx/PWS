---
sidebar_position: 2
---

# PwsPacker — Creare archivi .pws

`PwsPacker` assembla uno o più siti web in un file `.pws`.

## Esempio base (non firmato)

```csharp
using PWS.Format.Crypto;
using PWS.Format.Packing;

var packer = new PwsPacker();

await packer.PackAsync(
    new PwsPackOptions
    {
        Sites =
        [
            new PwsSiteSource
            {
                Id             = "docs",
                Title          = "La mia documentazione",
                EntryPoint     = "index.html",
                SourceDirectory = "/path/to/docusaurus/build",
            },
        ],
        SigningKey = PwsSigningKey.None(), // sviluppo — nessuna firma
    },
    outputPath: "docs.pws");
```

## Pacchetto firmato con ES256

```csharp
// 1. Genera la coppia di chiavi (una tantum)
var (fullKey, publicOnlyKey, publicKeyExport) = PwsSigningKey.GenerateEcDsa();

// Salva fullKey e publicKeyExport in modo sicuro
// publicKeyExport → "ES256:MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcD..."

// 2. Crea l'archivio firmato
var packer = new PwsPacker();

await packer.PackAsync(
    new PwsPackOptions
    {
        Sites      = [ new PwsSiteSource { Id = "docs", Title = "Docs", SourceDirectory = "build/" } ],
        SigningKey  = fullKey,   // chiave privata — tienila segreta
    },
    "docs.pws");

// La chiave pubblica viene automaticamente embedded nel manifest.json:
// "publicKey": "ES256:MFkwEw..."
```

## Pacchetto firmato con HMAC-SHA256

```csharp
var packer = new PwsPacker();

await packer.PackAsync(
    new PwsPackOptions
    {
        Sites      = [ /* ... */ ],
        SigningKey  = PwsSigningKey.FromHmac("super-secret-key"),
    },
    "docs.pws");
```

:::caution
HMAC è simmetrico: chiunque conosca il segreto può sia firmare che verificare.
Usa ES256 per la distribuzione pubblica.
:::

## Più siti in un unico archivio

```csharp
await packer.PackAsync(
    new PwsPackOptions
    {
        Sites =
        [
            new PwsSiteSource { Id = "docs", Title = "Docs",  SourceDirectory = "docs/build" },
            new PwsSiteSource { Id = "blog", Title = "Blog",  SourceDirectory = "blog/build" },
            new PwsSiteSource { Id = "api",  Title = "API",   SourceDirectory = "api/build"  },
        ],
        SigningKey = fullKey,
    },
    "monorepo.pws");
```

Ogni sito viene scritto in `sites/{id}/` nel ZIP e ottiene il proprio JWT firmato.

## File espliciti (senza cartella)

```csharp
var site = new PwsSiteSource { Id = "hello", Title = "Hello" };
site.AddFile("index.html",  () => File.OpenRead("index.html"));
site.AddFile("style.css",   () => File.OpenRead("style.css"));

await packer.PackAsync(new PwsPackOptions { Sites = [site] }, "hello.pws");
```

