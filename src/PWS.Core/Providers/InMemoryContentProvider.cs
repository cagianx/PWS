using PWS.Core.Abstractions;
using PWS.Core.Models;

namespace PWS.Core.Providers;

/// <summary>
/// Provider che serve contenuti da un dizionario in-memory.
/// Utile per pagine statiche, contenuti embeddati o testing.
/// Schema supportato: pws://
/// </summary>
public sealed class InMemoryContentProvider : IContentProvider
{
    private readonly Dictionary<string, Func<ContentResponse>> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string[] _supportedSchemes;

    public InMemoryContentProvider(params string[] schemes)
    {
        _supportedSchemes = schemes.Length > 0 ? schemes : ["pws"];
        RegisterDefaults();
    }

    public bool CanHandle(Uri uri) =>
        _supportedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);

    public Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        var key = $"{request.Uri.Host}{request.Uri.AbsolutePath}".TrimEnd('/');

        if (_routes.TryGetValue(key, out var factory))
            return Task.FromResult(factory());

        return Task.FromResult(
            ContentResponse.Error(404, $"Risorsa '{request.Uri}' non trovata in memoria."));
    }

    /// <summary>Registra un percorso con una risposta HTML statica.</summary>
    public InMemoryContentProvider Register(string path, string html, string? title = null)
    {
        _routes[path.TrimStart('/')] =
            () => ContentResponse.FromHtml(html, title, $"pws://{path}");
        return this;
    }

    /// <summary>Registra un percorso con una factory di risposta dinamica.</summary>
    public InMemoryContentProvider Register(string path, Func<ContentResponse> factory)
    {
        _routes[path.TrimStart('/')] = factory;
        return this;
    }

    // ──────────────────────────────────────────────
    private void RegisterDefaults()
    {
        Register("home", HomeHtml(), "Benvenuto — PWS");
        Register("about", AboutHtml(), "Informazioni — PWS");
    }

    private static string HomeHtml() => """
        <html>
        <head><meta charset="utf-8"><title>Benvenuto — PWS</title></head>
        <body style="font-family:sans-serif;max-width:800px;margin:40px auto;padding:0 20px">
          <h1>🌐 PWS Browser</h1>
          <p>Benvenuto nel <strong>PWS Browser</strong>: un browser che carica contenuti
          da sorgenti astratte, non dal filesystem.</p>
          <h2>Pagine disponibili</h2>
          <ul>
            <li><a href="pws://home">pws://home</a> — questa pagina</li>
            <li><a href="pws://about">pws://about</a> — informazioni</li>
          </ul>
        </body>
        </html>
        """;

    private static string AboutHtml() => """
        <html>
        <head><meta charset="utf-8"><title>Informazioni — PWS</title></head>
        <body style="font-family:sans-serif;max-width:800px;margin:40px auto;padding:0 20px">
          <h1>Informazioni su PWS</h1>
          <p>PWS è un browser MAUI su Linux (GTK4) che astrae completamente la sorgente
          dei contenuti tramite <code>IContentProvider</code>.</p>
          <p>I provider disponibili sono:</p>
          <ul>
            <li><strong>InMemoryContentProvider</strong> — contenuti da dizionario (schema pws://)</li>
            <li><strong>ApiContentProvider</strong> — contenuti da API REST (schema api://)</li>
          </ul>
          <p><a href="pws://home">← Torna alla home</a></p>
        </body>
        </html>
        """;
}

