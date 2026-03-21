---
sidebar_position: 2
---

# PWS.Core

`PWS.Core` è una libreria **portable** (`net10.0`) senza alcuna dipendenza su MAUI o GTK.
Contiene tutta la logica di navigazione e l'astrazione dei contenuti.

## Dipendenze

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.*" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.*" />
```

Solo BCL + `Microsoft.Extensions.*`. Zero MAUI, zero GTK.

## Struttura

```
PWS.Core/
├── Abstractions/
│   ├── IContentProvider.cs    ← astrazione sorgente contenuti
│   └── INavigationService.cs  ← astrazione navigazione
├── Models/
│   ├── ContentRequest.cs      ← richiesta al provider
│   ├── ContentResponse.cs     ← risposta (Stream + metadata)
│   └── NavigationEntry.cs     ← voce nella history
├── Navigation/
│   ├── NavigationHistory.cs   ← stack back/forward
│   └── NavigationService.cs   ← coordina provider + history
└── Providers/
    ├── InMemoryContentProvider.cs
    ├── ApiContentProvider.cs
    └── CompositeContentProvider.cs
```

## Modelli chiave

### ContentRequest

```csharp
public sealed class ContentRequest
{
    public Uri Uri { get; init; }
    public string Method { get; init; } = "GET";
    public IReadOnlyDictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }

    public static ContentRequest Get(Uri uri) => new() { Uri = uri };
}
```

### ContentResponse

```csharp
public sealed class ContentResponse : IDisposable
{
    public int StatusCode { get; init; } = 200;
    public string MimeType { get; init; } = "text/html";
    public string? Title { get; init; }
    public Stream Content { get; init; } = Stream.Null;  // sempre Stream, mai string

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public static ContentResponse FromHtml(string html, string? title = null);
    public static ContentResponse Error(int statusCode, string message);
}
```

:::important ContentResponse è IDisposable
Fare sempre `using` o `Dispose()` su `ContentResponse` per liberare il `Stream` interno.
:::

### NavigationEntry

```csharp
public sealed class NavigationEntry
{
    public Uri Uri { get; init; }
    public string? Title { get; set; }   // mutabile: aggiornato dopo la risposta
    public DateTimeOffset Timestamp { get; init; }
}
```

## NavigationHistory

Gestisce uno stack back/forward classico. `Push()` tronca il forward stack.

```csharp
history.Push(entry);        // naviga → azzera "avanti"
history.GoBack();           // sposta cursore indietro
history.GoForward();        // sposta cursore avanti
history.CanGoBack           // bool
history.CanGoForward        // bool
```

## NavigationService

Coordina `NavigationHistory` e `IContentProvider`. Espone gli eventi `Navigating` e `Navigated`.

```csharp
await nav.NavigateAsync(new Uri("pws://about"));
await nav.GoBackAsync();
await nav.GoForwardAsync();
await nav.RefreshAsync();
```

