---
sidebar_position: 1
---

# IContentProvider

`IContentProvider` è l'astrazione centrale di PWS. Tutto il contenuto mostrato
nella WebView passa attraverso questa interfaccia.

## Definizione

```csharp
public interface IContentProvider
{
    /// <summary>
    /// Verifica se questo provider è in grado di gestire l'URI richiesto.
    /// </summary>
    bool CanHandle(Uri uri);

    /// <summary>
    /// Recupera il contenuto per la richiesta specificata.
    /// </summary>
    Task<ContentResponse> GetAsync(
        ContentRequest request,
        CancellationToken cancellationToken = default);
}
```

## Implementare un provider personalizzato

```csharp
public sealed class MyProvider : IContentProvider
{
    public bool CanHandle(Uri uri) =>
        uri.Scheme.Equals("myscheme", StringComparison.OrdinalIgnoreCase);

    public Task<ContentResponse> GetAsync(
        ContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var html = $"<h1>Ciao da {request.Uri}</h1>";
        return Task.FromResult(ContentResponse.FromHtml(html, "Titolo"));
    }
}
```

## Registrare il provider

In `MauiProgram.cs`:

```csharp
builder.Services.AddSingleton<MyProvider>();

builder.Services.AddSingleton<IContentProvider>(sp =>
    new CompositeContentProvider([
        sp.GetRequiredService<InMemoryContentProvider>(),
        sp.GetRequiredService<MyProvider>(),   // ← aggiungi qui
    ]));
```

## Provider disponibili

| Provider | Schema | Descrizione |
|----------|--------|-------------|
| [`InMemoryContentProvider`](./in-memory) | `pws://` | Dizionario in-memory |
| [`ApiContentProvider`](./api) | `http://`, `https://`, `api://` | Endpoint HTTP/REST |
| [`CompositeContentProvider`](./composite) | (tutti) | Delega ai provider registrati |

