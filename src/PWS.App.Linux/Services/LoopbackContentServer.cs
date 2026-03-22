using System.Net;
using Microsoft.Extensions.Logging;
using PWS.Core.Models;
using PWS.Core.Providers;

namespace PWS.App.Linux.Services;

/// <summary>
/// Espone il contenuto del <see cref="PwsContentProvider"/> corrente tramite un piccolo
/// server HTTP locale su loopback, così la WebView può caricare correttamente anche asset
/// secondari (JS/CSS/img) richiesti da siti statici come Docusaurus.
/// </summary>
public sealed class LoopbackContentServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly PwsFileService _pwsFileService;
    private readonly ILogger<LoopbackContentServer> _logger;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();
    private string? _activeSiteId;
    private bool _disposed;

    public LoopbackContentServer(
        PwsFileService pwsFileService,
        ILogger<LoopbackContentServer> logger)
    {
        _pwsFileService = pwsFileService;
        _logger = logger;
        _port = GetFreePort();

        var prefix = $"http://127.0.0.1:{_port}/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _logger.LogInformation("LoopbackContentServer avviato su {Prefix}", prefix);
        _ = Task.Run(ListenLoopAsync);
    }

    public string BaseAddress => $"http://127.0.0.1:{_port}/";

    /// <summary>
    /// Converte un URI <c>pws://siteId/path</c> in un URL loopback HTTP equivalente.
    /// Il siteId viene ricordato come contesto attivo per servire i path root-relative.
    /// </summary>
    public string GetUrlFor(Uri pwsUri)
    {
        var siteId = string.IsNullOrWhiteSpace(pwsUri.Host)
            ? _pwsFileService.CurrentProvider?.DefaultSiteId
            : pwsUri.Host;

        _activeSiteId = siteId;

        var path = pwsUri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            path = "/index.html";

        var url = $"{BaseAddress.TrimEnd('/')}{path}{pwsUri.Query}";
        _logger.LogDebug("LoopbackContentServer.GetUrlFor: {PwsUri} -> {Url} (siteId={SiteId})", pwsUri, url, _activeSiteId);
        return url;
    }

    /// <summary>
    /// Prova a mappare un URL loopback HTTP al corrispondente URI pws:// del sito attivo.
    /// </summary>
    public bool TryMapLoopbackUrlToPwsUri(string url, out string pwsUri)
    {
        pwsUri = string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            uri.Host != "127.0.0.1" ||
            uri.Port != _port)
            return false;

        if (string.IsNullOrWhiteSpace(_activeSiteId))
            return false;

        var path = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            path = "/index.html";

        pwsUri = $"pws://{_activeSiteId}{path}{uri.Query}";
        return true;
    }

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
            var request = context.Request;
            var response = context.Response;

            _logger.LogTrace("Loopback request: {Method} {Url}", request.HttpMethod, request.Url);

            if (!HttpMethods.IsGet(request.HttpMethod))
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            var provider = _pwsFileService.CurrentProvider;
            if (provider is null)
            {
                _logger.LogWarning("Loopback request senza provider corrente.");
                await WriteStringAsync(response, 503, "text/plain", "No active PWS provider.");
                return;
            }

            var siteId = _activeSiteId ?? provider.DefaultSiteId;
            if (string.IsNullOrWhiteSpace(siteId))
            {
                _logger.LogWarning("Loopback request senza siteId attivo.");
                await WriteStringAsync(response, 500, "text/plain", "No active siteId.");
                return;
            }

            var relativePath = request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath))
                relativePath = "index.html";

            using var content = await GetContentAsync(provider, siteId, relativePath);

            response.StatusCode = content.StatusCode;
            response.ContentType = content.MimeType;

            foreach (var (key, value) in content.Headers)
                response.Headers[key] = value;

            await content.Content.CopyToAsync(response.OutputStream, _cts.Token);
            response.OutputStream.Close();

            _logger.LogDebug("Loopback response: {Status} {Path} ({Mime})", response.StatusCode, relativePath, content.MimeType);
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

    private static async Task<ContentResponse> GetContentAsync(PwsContentProvider provider, string siteId, string relativePath)
    {
        var primaryUri = new Uri($"pws://{siteId}/{relativePath}");
        var content = await provider.GetAsync(ContentRequest.Get(primaryUri));

        if (content.StatusCode != 404)
            return content;

        content.Dispose();

        if (!Path.HasExtension(relativePath))
        {
            var fallbackPath = relativePath.TrimEnd('/') + "/index.html";
            return await provider.GetAsync(ContentRequest.Get(new Uri($"pws://{siteId}/{fallbackPath}")));
        }

        return await provider.GetAsync(ContentRequest.Get(primaryUri));
    }

    private static async Task WriteStringAsync(HttpListenerResponse response, int statusCode, string contentType, string text)
    {
        response.StatusCode = statusCode;
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

        _logger.LogDebug("LoopbackContentServer.Dispose");
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
    }

    private static class HttpMethods
    {
        public static bool IsGet(string? method) => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
    }
}

