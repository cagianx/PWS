using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using PWS.Core.Models;
using PWS.Core.Providers;

namespace PWS.App.Linux.Services;

/// <summary>
/// Espone il contenuto di un <see cref="PwsContentProvider"/> tramite un piccolo
/// server HTTP locale su loopback. Ogni istanza è dedicata a un singolo sito/archivio,
/// e gira su una porta TCP casuale (una porta per sito aperto).
/// </summary>
public sealed class LoopbackContentServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly PwsContentProvider _provider;
    private readonly string _siteId;
    private readonly ILogger<LoopbackContentServer> _logger;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Serializza l'accesso allo <see cref="System.IO.Compression.ZipArchive"/> sottostante.
    /// <para>
    /// <c>ZipArchive</c> non è thread-safe: leggere entry diverse in parallelo
    /// (p.es. index.html + main.js + styles.css nella stessa pagina) corrode
    /// lo stream interno e genera <c>InvalidDataException: unsupported compression method</c>.
    /// La soluzione è acquisire il lock, leggere e bufferizzare l'entry in un
    /// <c>MemoryStream</c>, poi rilasciare il lock — così il <c>CopyToAsync</c>
    /// verso il client avviene su un buffer in-memory, mai su ZipArchive direttamente.
    /// </para>
    /// </summary>
    private readonly SemaphoreSlim _zipLock = new(1, 1);

    // ── Cache in memoria ──────────────────────────────────────────────────────

    /// <summary>
    /// Voce della cache: bytes del file + metadati HTTP.
    /// </summary>
    private sealed record CacheEntry(byte[] Data, string MimeType, string? FinalUri);

    /// <summary>
    /// Cache in-memoria delle risorse già lette dallo ZIP.
    /// Chiave: <c>relativePath</c> normalizzato (es. <c>"assets/js/main.js"</c>).
    /// Thread-safe per letture concorrenti; le scritture avvengono solo
    /// mentre si detiene <see cref="_zipLock"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Totale byte occupati dalla cache. Modificato solo mentre si detiene
    /// <see cref="_zipLock"/>, quindi non servono operazioni atomiche.
    /// </summary>
    private long _cacheSizeBytes;

    /// <summary>Limite massimo complessivo della cache (5 MB).</summary>
    private const long MaxCacheSizeBytes = 5L * 1024 * 1024;

    public LoopbackContentServer(
        PwsContentProvider provider,
        string siteId,
        ILogger<LoopbackContentServer> logger)
    {
        _provider = provider;
        _siteId   = siteId;
        _logger   = logger;
        _port     = GetFreePort();

        var prefix = $"http://127.0.0.1:{_port}/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _logger.LogInformation(
            "LoopbackContentServer avviato su {Prefix} per sito '{SiteId}'", prefix, siteId);
        _ = Task.Run(ListenLoopAsync);
    }

    /// <summary>Indirizzo base del server, es. <c>http://127.0.0.1:49152/</c>.</summary>
    public string BaseAddress => $"http://127.0.0.1:{_port}/";

    /// <summary>Identificatore del sito servito da questa istanza.</summary>
    public string SiteId => _siteId;

    /// <summary>Porta TCP su cui il server è in ascolto.</summary>
    public int Port => _port;

    // ── Loop di ascolto ───────────────────────────────────────────────────────

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoopbackContentServer: errore in accept loop.");
                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(context), _cts.Token);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request  = context.Request;
            var response = context.Response;

            _logger.LogTrace("Loopback request: {Method} {Url}", request.HttpMethod, request.Url);

            if (!HttpMethods.IsGet(request.HttpMethod))
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            var relativePath = request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath))
                relativePath = "index.html";

            using var content = await GetContentAsync(relativePath);

            response.StatusCode  = content.StatusCode;
            response.ContentType = content.MimeType;

            foreach (var (key, value) in content.Headers)
                response.Headers[key] = value;

            await content.Content.CopyToAsync(response.OutputStream, _cts.Token);
            response.OutputStream.Close();

            _logger.LogDebug(
                "Loopback response: {Status} {Path} ({Mime})",
                response.StatusCode, relativePath, content.MimeType);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoopbackContentServer: errore gestendo la request.");
            try
            {
                await WriteStringAsync(context.Response, 500, "text/plain", ex.Message);
            }
            catch
            {
                // ignore secondary failures
            }
        }
    }

    private async Task<ContentResponse> GetContentAsync(string relativePath)
    {
        // ── Fast path: cache hit ─────────────────────────────────────────────
        if (_cache.TryGetValue(relativePath, out var cached))
        {
            _logger.LogTrace("Cache HIT: {Path} ({Bytes}B)", relativePath, cached.Data.Length);
            return CacheEntryToResponse(cached);
        }

        // ── Slow path: lettura ZIP (serializzata) ────────────────────────────
        await _zipLock.WaitAsync(_cts.Token);
        try
        {
            // Double-check: un altro thread potrebbe aver popolato la cache
            // tra il primo TryGetValue e l'acquisizione del lock.
            if (_cache.TryGetValue(relativePath, out cached))
                return CacheEntryToResponse(cached);

            return await ReadZipAndMaybeCacheAsync(relativePath);
        }
        finally
        {
            _zipLock.Release();
        }
    }

    /// <summary>
    /// Legge il file dallo ZIP (già sotto <see cref="_zipLock"/>), lo bufferizza
    /// e, se la risposta è 200 e c'è spazio disponibile, lo inserisce in cache.
    /// </summary>
    private async Task<ContentResponse> ReadZipAndMaybeCacheAsync(string relativePath)
    {
        var buffered = await ReadAndBufferAsync(relativePath);

        if (buffered.IsSuccess && buffered.Content is MemoryStream ms)
        {
            var bytes = ms.ToArray();
            ms.Position = 0; // riposiziona per la lettura del client

            if (_cacheSizeBytes + bytes.Length <= MaxCacheSizeBytes)
            {
                var entry = new CacheEntry(bytes, buffered.MimeType, buffered.FinalUri);
                if (_cache.TryAdd(relativePath, entry))
                {
                    _cacheSizeBytes += bytes.Length;
                    _logger.LogDebug(
                        "Cache SET: {Path} ({Bytes}B) — totale {Total}B / {Max}B",
                        relativePath, bytes.Length, _cacheSizeBytes, MaxCacheSizeBytes);
                }
            }
            else
            {
                _logger.LogTrace(
                    "Cache SKIP (limite 5 MB raggiunto): {Path} ({Bytes}B) — totale {Total}B",
                    relativePath, bytes.Length, _cacheSizeBytes);
            }
        }

        return buffered;
    }

    /// <summary>
    /// Ricostruisce un <see cref="ContentResponse"/> a partire da una voce di cache.
    /// Usa <c>new MemoryStream(byte[], writable:false)</c> per evitare copie inutili.
    /// </summary>
    private static ContentResponse CacheEntryToResponse(CacheEntry entry) =>
        new()
        {
            StatusCode = 200,
            MimeType   = entry.MimeType,
            Content    = new MemoryStream(entry.Data, writable: false),
            FinalUri   = entry.FinalUri,
        };

    /// <summary>
    /// Legge il file dal provider e ne copia il contenuto in un <see cref="MemoryStream"/>.
    /// Deve essere chiamata mentre si detiene <see cref="_zipLock"/>.
    /// </summary>
    private async Task<ContentResponse> ReadAndBufferAsync(string relativePath)
    {
        var primaryUri = new Uri($"pws://{_siteId}/{relativePath}");
        var raw = await _provider.GetAsync(ContentRequest.Get(primaryUri));

        if (raw.StatusCode != 404)
            return await BufferAsync(raw);

        raw.Dispose();

        // Fallback: per path senza estensione prova /index.html
        if (!Path.HasExtension(relativePath))
        {
            var fallbackPath = relativePath.TrimEnd('/') + "/index.html";
            var fallback = await _provider.GetAsync(
                ContentRequest.Get(new Uri($"pws://{_siteId}/{fallbackPath}")));
            return await BufferAsync(fallback);
        }

        // File con estensione non trovato → 404
        return ContentResponse.Error(404, $"File '{relativePath}' non trovato nell'archivio.");
    }

    /// <summary>
    /// Copia il contenuto di <paramref name="raw"/> in un <see cref="MemoryStream"/> e
    /// restituisce un nuovo <see cref="ContentResponse"/> che non dipende più dallo
    /// stream ZipArchive originale (che viene poi disposed).
    /// </summary>
    private static async Task<ContentResponse> BufferAsync(ContentResponse raw)
    {
        using (raw)
        {
            var ms = new MemoryStream();
            await raw.Content.CopyToAsync(ms);
            ms.Position = 0;
            return new ContentResponse
            {
                StatusCode = raw.StatusCode,
                MimeType   = raw.MimeType,
                Content    = ms,
                FinalUri   = raw.FinalUri,
                Headers    = raw.Headers,
            };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task WriteStringAsync(
        HttpListenerResponse response, int statusCode, string contentType, string text)
    {
        response.StatusCode  = statusCode;
        response.ContentType = contentType;
        await using var writer = new StreamWriter(response.OutputStream);
        await writer.WriteAsync(text);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogDebug("LoopbackContentServer.Dispose (siteId={SiteId}, port={Port})", _siteId, _port);
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
        _zipLock.Dispose();
        _cache.Clear();   // libera la memoria occupata dalla cache
    }

    private static class HttpMethods
    {
        public static bool IsGet(string? method) =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
    }
}

