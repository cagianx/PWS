---
sidebar_position: 4
---

# CompositeContentProvider

Aggrega più `IContentProvider` in uno solo. Delega al **primo provider** che
dichiara di poter gestire l'URI (`CanHandle`).

## Utilizzo

```csharp
var composite = new CompositeContentProvider([
    inMemoryProvider,   // gestisce pws://
    apiProvider,        // gestisce http://, https://, api://
    myCustomProvider,   // gestisce myscheme://
]);
```

L'ordine nell'array determina la priorità: il primo provider con `CanHandle = true`
vince.

## Registrazione in MauiProgram.cs

```csharp
builder.Services.AddSingleton<IContentProvider>(sp =>
    new CompositeContentProvider([
        sp.GetRequiredService<InMemoryContentProvider>(),
        // aggiungere altri provider qui
    ]));
```

## Comportamento in caso di nessun match

Se nessun provider dichiara di poter gestire l'URI, `GetAsync` lancia
`InvalidOperationException`. Il `NavigationService` cattura questa eccezione
e restituisce `ContentResponse.Error(404, ...)`.

## API

```csharp
public CompositeContentProvider(IEnumerable<IContentProvider> providers)

bool CanHandle(Uri uri)        // true se almeno un provider può gestire l'URI
Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken ct)
```

