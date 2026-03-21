using PWS.Core.Models;

namespace PWS.Core.Abstractions;

/// <summary>
/// Astrae la sorgente da cui vengono caricati i contenuti.
/// Il browser non sa DOVE si trova il contenuto: potrebbe venire da un API remota,
/// da una store in memoria, da un database, o da qualsiasi altra sorgente.
/// </summary>
public interface IContentProvider
{
    /// <summary>
    /// Verifica se questo provider è in grado di gestire l'URI richiesto.
    /// </summary>
    bool CanHandle(Uri uri);

    /// <summary>
    /// Recupera il contenuto per la richiesta specificata.
    /// </summary>
    Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default);
}

