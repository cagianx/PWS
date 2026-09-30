using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PWS.IntegrationTests.Common;

/// <summary>Risposta che <see cref="TestHttpServer"/> restituisce per una richiesta.</summary>
public sealed record TestHttpResponse(int StatusCode, string ContentType, string Body);

/// <summary>Richiesta ricevuta da <see cref="TestHttpServer"/>, registrata per le asserzioni.</summary>
public sealed record RecordedRequest(
    string Method,
    string PathAndQuery,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Server HTTP reale su <c>127.0.0.1</c>, porta libera: i test di
/// <see cref="PWS.Core.Providers.ApiContentProvider"/> passano da un socket vero,
/// non da un <c>HttpMessageHandler</c> finto.
/// </summary>
public sealed class TestHttpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<RecordedRequest, TestHttpResponse> _handler;

    public TestHttpServer(Func<RecordedRequest, TestHttpResponse> handler)
    {
        _handler    = handler;
        BaseAddress = new Uri($"http://127.0.0.1:{FreeTcpPort.Next()}/");

        _listener.Prefixes.Add(BaseAddress.ToString());
        _listener.Start();
        _ = Task.Run(ListenAsync);
    }

    public Uri BaseAddress { get; }

    /// <summary>Richieste ricevute, in ordine di arrivo.</summary>
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in context.Request.Headers.AllKeys)
                if (key is not null)
                    headers[key] = context.Request.Headers[key] ?? string.Empty;

            var recorded = new RecordedRequest(
                context.Request.HttpMethod,
                context.Request.Url?.PathAndQuery ?? string.Empty,
                headers);
            Requests.Enqueue(recorded);

            var reply = _handler(recorded);
            var body  = Encoding.UTF8.GetBytes(reply.Body);

            context.Response.StatusCode      = reply.StatusCode;
            context.Response.ContentType     = reply.ContentType;
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }
    }

    public void Dispose() => _listener.Close();
}

/// <summary>Porta TCP libera su loopback, scelta dal sistema operativo.</summary>
public static class FreeTcpPort
{
    public static int Next()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
