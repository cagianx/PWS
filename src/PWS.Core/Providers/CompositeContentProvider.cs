using PWS.Core.Abstractions;
using PWS.Core.Models;

namespace PWS.Core.Providers;

/// <summary>
/// Provider composito che aggrega più IContentProvider.
/// Delega al primo provider che dichiara di poter gestire l'URI richiesto.
/// </summary>
public sealed class CompositeContentProvider : IContentProvider
{
    private readonly IReadOnlyList<IContentProvider> _providers;

    public CompositeContentProvider(IEnumerable<IContentProvider> providers)
    {
        _providers = providers.ToList();
    }

    public bool CanHandle(Uri uri) =>
        _providers.Any(p => p.CanHandle(uri));

    public Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(request.Uri))
            ?? throw new InvalidOperationException($"Nessun provider disponibile per '{request.Uri}'");

        return provider.GetAsync(request, cancellationToken);
    }
}

