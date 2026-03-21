---
sidebar_position: 2
---

# InMemoryContentProvider

:::info Ruolo nel progetto
`InMemoryContentProvider` è un **provider di sviluppo e demo**: permette di avviare
il browser e navigare pagine di test senza un file `.pws` reale. Il provider
destinato alla produzione sarà `PwsFileContentProvider`, che legge le risorse
direttamente dall'archivio ZIP `.pws`.
:::

Serve contenuti da un dizionario in-memory. Ideale per pagine statiche, contenuti
embeddati nel binario o testing.

**Schema supportato:** `pws://`

## Utilizzo base

```csharp
var provider = new InMemoryContentProvider("pws");

// Registra una pagina statica
provider.Register("home", "<html><body><h1>Home</h1></body></html>", "Home");
provider.Register("about", "<html><body><h1>About</h1></body></html>", "About");
```

La navigazione `pws://home` invocherà il route registrato come `"home"`.

## Registrare una factory dinamica

```csharp
provider.Register("dynamic", () => ContentResponse.FromHtml(
    $"<p>Ora: {DateTime.Now:HH:mm:ss}</p>",
    title: "Pagina dinamica"
));
```

## Pagine predefinite

Il costruttore registra automaticamente due pagine:

| URI | Titolo |
|-----|--------|
| `pws://home` | Benvenuto — PWS |
| `pws://about` | Informazioni — PWS |

## Risoluzione degli URI

Il path viene estratto come `host + path` e normalizzato:

| URI richiesto | Chiave cercata |
|---------------|---------------|
| `pws://home` | `home` |
| `pws://pages/docs` | `pages/docs` |

## API

```csharp
// Costruttore — specifica gli schemi supportati
new InMemoryContentProvider("pws", "custom")

// Registra HTML statico
InMemoryContentProvider Register(string path, string html, string? title = null)

// Registra factory dinamica
InMemoryContentProvider Register(string path, Func<ContentResponse> factory)

// IContentProvider
bool CanHandle(Uri uri)
Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken ct)
```

