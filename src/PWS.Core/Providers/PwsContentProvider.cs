using PWS.Core.Abstractions;
using PWS.Core.Models;
using Microsoft.Extensions.Logging;
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
/// Il provider mantiene aperto il <see cref="PwsReader"/> fino a Dispose e gestisce
/// una cache in-memoria con limite massimo di <see cref="MaxCacheBytes"/> byte (5 MB).
/// Quando lo spazio è esaurito il file viene letto direttamente dallo zip senza cache.
/// </para>
/// </summary>
public sealed class PwsContentProvider : IContentProvider, IDisposable
{
    /// <summary>Dimensione massima complessiva della cache in-memoria (5 MB).</summary>
    public const long MaxCacheBytes = 5 * 1024 * 1024;

    private readonly PwsReader _reader;
    private readonly ILogger<PwsContentProvider>? _logger;
    private bool _disposed;

    // ── Cache in-memoria ─────────────────────────────────────────────────────
    // Chiave: archivePath normalizzato (es. "sites/docs/index.html")
    // Valore: contenuto del file come byte[]
    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    private long _cacheUsedBytes;
    private readonly object _cacheLock = new();

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
    /// <param name="logger">Logger opzionale.</param>
    public PwsContentProvider(PwsReader reader, string? defaultSiteId = null, ILogger<PwsContentProvider>? logger = null)
    {
        _reader = reader;
        _logger = logger;

        // Auto-detect default site se c'è un solo sito nel manifest
        DefaultSiteId = defaultSiteId
                        ?? (reader.Manifest.Sites.Count == 1
                            ? reader.Manifest.Sites[0].Id
                            : null);

        _logger?.LogDebug("PwsContentProvider creato: siti={Count} defaultSiteId={Sid}",
            reader.Manifest.Sites.Count, DefaultSiteId);
    }

    // ── IContentProvider ─────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce <see langword="true"/> se l'URI usa lo schema <c>pws://</c>
    /// e l'host corrisponde a un sito presente nell'archivio corrente.
    /// </summary>
    public bool CanHandle(Uri uri)
    {
        if (!uri.Scheme.Equals("pws", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogTrace("CanHandle false — schema '{Scheme}' (atteso 'pws')", uri.Scheme);
            return false;
        }

        // URI senza host (pws:///path): gestibile solo se c'è un DefaultSiteId
        if (string.IsNullOrEmpty(uri.Host))
        {
            var ok = DefaultSiteId is not null;
            _logger?.LogTrace("CanHandle {Ok} — nessun host, DefaultSiteId={Sid}", ok, DefaultSiteId);
            return ok;
        }

        // URI con host: verifica che l'host corrisponda a un site ID noto nell'archivio.
        // Questo evita conflitti con pws://home e pws://about (gestiti da InMemoryContentProvider).
        var found = _reader.Manifest.Sites.Any(s =>
            s.Id.Equals(uri.Host, StringComparison.OrdinalIgnoreCase));
        _logger?.LogTrace("CanHandle {Ok} — host='{Host}' knownSite={Found}", found, uri.Host, found);
        return found;
    }

    /// <summary>
    /// Recupera il file richiesto dall'archivio `.pws` come <see cref="ContentResponse"/>.
    /// I file vengono memorizzati in una cache in-memoria (limite <see cref="MaxCacheBytes"/>).
    /// Quando il budget è esaurito la risposta viene servita direttamente dallo zip.
    /// </summary>
    public async Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("GetAsync ← {Uri}", request.Uri);

        if (_disposed)
        {
            _logger?.LogError("GetAsync: provider già disposed per {Uri}", request.Uri);
            return ContentResponse.Error(500, "Provider già disposed.");
        }

        try
        {
            var (siteId, relativePath) = ParseUri(request.Uri);
            _logger?.LogDebug("GetAsync: siteId='{SiteId}' path='{Path}'", siteId, relativePath);

            var mimeType   = GetMimeType(relativePath);
            var cacheKey   = BuildCacheKey(siteId, relativePath);

            // ── 1. Cache HIT ─────────────────────────────────────────────────
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                {
                    _logger?.LogTrace("Cache HIT: {Key} ({Bytes} B)", cacheKey, cached.Length);
                    return new ContentResponse
                    {
                        StatusCode = 200,
                        Content    = new MemoryStream(cached, writable: false),
                        MimeType   = mimeType,
                        FinalUri   = request.Uri.ToString(),
                    };
                }
            }

            // ── 2. Lettura dallo zip ──────────────────────────────────────────
            var zipStream = _reader.FileSystem.OpenSiteFile(siteId, relativePath);
            using var ms  = new MemoryStream();
            await zipStream.CopyToAsync(ms, cancellationToken);
            await zipStream.DisposeAsync();
            var bytes = ms.ToArray();

            // ── 3. Tentativo di inserimento in cache ──────────────────────────
            lock (_cacheLock)
            {
                if (!_cache.ContainsKey(cacheKey) &&
                    _cacheUsedBytes + bytes.Length <= MaxCacheBytes)
                {
                    _cache[cacheKey]  = bytes;
                    _cacheUsedBytes  += bytes.Length;
                    _logger?.LogDebug(
                        "Cache STORE: {Key} ({Size} B) — usato {Used}/{Max} B",
                        cacheKey, bytes.Length, _cacheUsedBytes, MaxCacheBytes);
                }
                else
                {
                    _logger?.LogDebug(
                        "Cache BYPASS: {Key} ({Size} B) — usato {Used}/{Max} B",
                        cacheKey, bytes.Length, _cacheUsedBytes, MaxCacheBytes);
                }
            }

            _logger?.LogDebug("GetAsync → 200 mimeType='{Mime}' for {Uri}", mimeType, request.Uri);

            return new ContentResponse
            {
                StatusCode = 200,
                Content    = new MemoryStream(bytes, writable: false),
                MimeType   = mimeType,
                FinalUri   = request.Uri.ToString(),
            };
        }
        catch (KeyNotFoundException)
        {
            _logger?.LogWarning("GetAsync → 404 sito '{Host}' non nel manifest", request.Uri.Host);
            return ContentResponse.Error(404,
                $"Sito '{request.Uri.Host}' non trovato nel manifest.");
        }
        catch (FileNotFoundException)
        {
            _logger?.LogWarning("GetAsync → 404 file '{Path}' non nell'archivio", request.Uri.AbsolutePath);
            return ContentResponse.Error(404,
                $"File '{request.Uri.AbsolutePath}' non trovato nell'archivio.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "GetAsync → 500 per {Uri}", request.Uri);
            return ContentResponse.Error(500, $"Errore lettura file: {ex.Message}");
        }
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    /// <summary>Rilascia il <see cref="PwsReader"/> posseduto dal provider e svuota la cache.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _logger?.LogDebug("PwsContentProvider.Dispose: rilascio reader. DefaultSiteId={SiteId}", DefaultSiteId);
        _disposed = true;
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheUsedBytes = 0;
        }
        _reader.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Costruisce la chiave di cache da siteId + relativePath.</summary>
    private static string BuildCacheKey(string siteId, string relativePath) =>
        siteId + "/" + relativePath.TrimStart('/');

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

