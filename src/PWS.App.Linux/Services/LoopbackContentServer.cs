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
        var primaryUri = new Uri($"pws://{_siteId}/{relativePath}");
        var content    = await _provider.GetAsync(ContentRequest.Get(primaryUri));

        if (content.StatusCode != 404)
            return content;

        content.Dispose();

        // Fallback: prova index.html per path senza estensione
        if (!Path.HasExtension(relativePath))
        {
            var fallbackPath = relativePath.TrimEnd('/') + "/index.html";
            return await _provider.GetAsync(
                ContentRequest.Get(new Uri($"pws://{_siteId}/{fallbackPath}")));
        }

        return await _provider.GetAsync(ContentRequest.Get(primaryUri));
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
    }

    private static class HttpMethods
    {
        public static bool IsGet(string? method) =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
    }
}

