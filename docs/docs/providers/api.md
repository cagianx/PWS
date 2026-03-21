---
sidebar_position: 3
---

# ApiContentProvider

Recupera contenuti da endpoint HTTP/REST remoti. Supporta i protocolli standard
web più lo schema virtuale `api://`.

**Schemi supportati:** `http://`, `https://`, `api://` (e altri configurabili)

## Configurazione

```csharp
var httpClient = new HttpClient();
var baseAddress = new Uri("https://my-api.example.com");

var provider = new ApiContentProvider(httpClient, baseAddress, "api");
```

## Schema `api://`

Lo schema `api://` è un alias per il `baseAddress`. Una richiesta a `api://users/1`
viene tradotta in `https://my-api.example.com/users/1`.

```
api://users/1  →  https://my-api.example.com/users/1
api://pages    →  https://my-api.example.com/pages
```

## Registrazione in MauiProgram.cs

```csharp
builder.Services.AddSingleton<ApiContentProvider>(_ =>
    new ApiContentProvider(
        new HttpClient(),
        new Uri("https://my-api.example.com"),
        "api"  // abilita schema api://
    ));

builder.Services.AddSingleton<IContentProvider>(sp =>
    new CompositeContentProvider([
        sp.GetRequiredService<InMemoryContentProvider>(),
        sp.GetRequiredService<ApiContentProvider>(),
    ]));
```

## Comportamento

- Headers della risposta HTTP sono trasparenti (incluso `Content-Type` → `MimeType`)
- `FinalUri` è impostato all'URL finale dopo eventuali redirect
- Gli errori di rete restituiscono `ContentResponse.Error(503, ...)`
- Gli errori HTTP (4xx, 5xx) sono propagati con lo stesso status code

## API

```csharp
public ApiContentProvider(
    HttpClient httpClient,
    Uri baseAddress,
    params string[] extraSchemes)  // aggiunge schemi oltre a http/https
```

:::info TODO
`ApiContentProvider` è implementato ma non ancora registrato nel `CompositeContentProvider`
di default. Vedi la [roadmap](../intro#prossimi-passi).
:::

