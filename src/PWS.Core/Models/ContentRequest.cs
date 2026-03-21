namespace PWS.Core.Models;

/// <summary>
/// Rappresenta una richiesta di contenuto verso un provider.
/// Non contiene alcun riferimento al filesystem.
/// </summary>
public sealed class ContentRequest
{
    public Uri Uri { get; init; } = new("pws://home");
    public string Method { get; init; } = "GET";
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; init; }

    public static ContentRequest Get(Uri uri) => new() { Uri = uri, Method = "GET" };
    public static ContentRequest Get(string uri) => Get(new Uri(uri));
}

