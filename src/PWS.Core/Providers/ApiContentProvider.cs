using PWS.Core.Abstractions;
using PWS.Core.Models;

namespace PWS.Core.Providers;

/// <summary>
/// Provider che recupera contenuti da un endpoint HTTP/REST remoto.
/// Schema supportato: api:// o http:// / https://
/// </summary>
public sealed class ApiContentProvider : IContentProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseAddress;
    private readonly string[] _supportedSchemes;

    public ApiContentProvider(HttpClient httpClient, Uri baseAddress, params string[] extraSchemes)
    {
        _httpClient = httpClient;
        _baseAddress = baseAddress;
        _supportedSchemes = ["http", "https", .. extraSchemes];
    }

    public bool CanHandle(Uri uri) =>
        _supportedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);

    public async Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        // Se lo schema è "api://", risolve rispetto al baseAddress
        var targetUri = request.Uri.Scheme.Equals("api", StringComparison.OrdinalIgnoreCase)
            ? new Uri(_baseAddress, request.Uri.PathAndQuery)
            : request.Uri;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);
        foreach (var (k, v) in request.Headers)
            httpRequest.Headers.TryAddWithoutValidation(k, v);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            return ContentResponse.Error(503, $"Errore di rete: {ex.Message}");
        }

        var mimeType = httpResponse.Content.Headers.ContentType?.MediaType ?? "text/html";
        var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);

        return new ContentResponse
        {
            StatusCode = (int)httpResponse.StatusCode,
            MimeType = mimeType,
            FinalUri = httpResponse.RequestMessage?.RequestUri?.ToString(),
            Content = stream
        };
    }
}

