namespace PWS.Core.Models;

/// <summary>
/// Rappresenta la risposta di un provider al contenuto richiesto.
/// Il contenuto può essere HTML, testo, JSON, immagine — qualunque tipo MIME.
/// </summary>
public sealed class ContentResponse : IDisposable
{
    public int StatusCode { get; init; } = 200;
    public string MimeType { get; init; } = "text/html";
    public string? Title { get; init; }
    public string? FinalUri { get; init; }
    public Stream Content { get; init; } = Stream.Null;
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    /// <summary>Helper per creare una risposta HTML inline.</summary>
    public static ContentResponse FromHtml(string html, string? title = null, string? finalUri = null) =>
        new()
        {
            MimeType = "text/html; charset=utf-8",
            Title = title,
            FinalUri = finalUri,
            Content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html))
        };

    /// <summary>Helper per una risposta di errore.</summary>
    public static ContentResponse Error(int statusCode, string message) =>
        new()
        {
            StatusCode = statusCode,
            MimeType = "text/html; charset=utf-8",
            Title = $"Errore {statusCode}",
            Content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                $"<html><body><h1>Errore {statusCode}</h1><p>{message}</p></body></html>"))
        };

    public void Dispose() => Content.Dispose();
}

