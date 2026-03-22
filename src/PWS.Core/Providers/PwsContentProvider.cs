using PWS.Core.Abstractions;
using PWS.Core.Models;
using PWS.Format.Reading;

namespace PWS.Core.Providers;

/// <summary>
/// Content provider che serve file da un archivio .pws aperto in memoria.
/// <para>
/// Schema URI: <c>pws://{siteId}/{relativePath}</c>
/// </para>
/// <para>
/// Esempio: <c>pws://docs/index.html</c> → legge <c>sites/docs/index.html</c> dal .pws.
/// </para>
/// <para>
/// Il provider mantiene aperto il <see cref="PwsReader"/> fino a Dispose.
/// </para>
/// </summary>
public sealed class PwsContentProvider : IContentProvider, IDisposable
{
    private readonly PwsReader _reader;
    private bool _disposed;

    /// <summary>
    /// Site ID di default quando l'URI non specifica un host (es. <c>pws:///index.html</c>).
    /// Usato se il .pws contiene un solo sito.
    /// </summary>
    public string? DefaultSiteId { get; }

    /// <summary>
    /// Crea un provider da un <see cref="PwsReader"/> esistente.
    /// Il provider prende ownership del reader e lo dispose automaticamente.
    /// </summary>
    /// <param name="reader">Reader aperto sul file .pws.</param>
    /// <param name="defaultSiteId">
    /// Site ID di default. Se <see langword="null"/> e il manifest contiene un solo sito,
    /// usa quello automaticamente.
    /// </param>
    public PwsContentProvider(PwsReader reader, string? defaultSiteId = null)
    {
        _reader = reader;

        // Auto-detect default site se c'è un solo sito nel manifest
        DefaultSiteId = defaultSiteId
                        ?? (reader.Manifest.Sites.Count == 1
                            ? reader.Manifest.Sites[0].Id
                            : null);
    }

    // ── IContentProvider ─────────────────────────────────────────────────────

    public bool CanHandle(Uri uri)
    {
        if (!uri.Scheme.Equals("pws", StringComparison.OrdinalIgnoreCase))
            return false;

        // URI senza host (pws:///path): gestibile solo se c'è un DefaultSiteId
        if (string.IsNullOrEmpty(uri.Host))
            return DefaultSiteId is not null;

        // URI con host: verifica che l'host corrisponda a un site ID noto nell'archivio.
        // Questo evita conflitti con pws://home e pws://about (gestiti da InMemoryContentProvider).
        return _reader.Manifest.Sites.Any(s =>
            s.Id.Equals(uri.Host, StringComparison.OrdinalIgnoreCase));
    }

    public Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Task.FromResult(ContentResponse.Error(500, "Provider già disposed."));

        try
        {
            var (siteId, relativePath) = ParseUri(request.Uri);

            // Apri il file dal filesystem virtuale
            var stream = _reader.FileSystem.OpenSiteFile(siteId, relativePath);

            // Determina MIME type dal path
            var mimeType = GetMimeType(relativePath);

            return Task.FromResult(new ContentResponse
            {
                StatusCode  = 200,
                Content     = stream,
                MimeType    = mimeType,
                FinalUri    = request.Uri.ToString(),
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(ContentResponse.Error(404,
                $"Sito '{request.Uri.Host}' non trovato nel manifest."));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult(ContentResponse.Error(404,
                $"File '{request.Uri.AbsolutePath}' non trovato nell'archivio."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ContentResponse.Error(500,
                $"Errore lettura file: {ex.Message}"));
        }
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (string siteId, string relativePath) ParseUri(Uri uri)
    {
        // pws://docs/index.html       → siteId="docs", path="index.html"
        // pws:///index.html            → siteId=DefaultSiteId, path="index.html"
        // pws://docs/assets/main.css  → siteId="docs", path="assets/main.css"

        var siteId = string.IsNullOrEmpty(uri.Host) ? DefaultSiteId : uri.Host;
        if (string.IsNullOrEmpty(siteId))
            throw new InvalidOperationException(
                "URI pws:/// richiede un DefaultSiteId. " +
                "Specificare l'host (es. pws://docs/) o impostare DefaultSiteId.");

        var relativePath = uri.AbsolutePath.TrimStart('/');
        return (siteId, relativePath);
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html",
            ".css"            => "text/css",
            ".js"             => "application/javascript",
            ".json"           => "application/json",
            ".xml"            => "application/xml",
            ".svg"            => "image/svg+xml",
            ".png"            => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            ".woff"           => "font/woff",
            ".woff2"          => "font/woff2",
            ".ttf"            => "font/ttf",
            ".txt"            => "text/plain",
            ".md"             => "text/markdown",
            _                 => "application/octet-stream",
        };
    }
}

