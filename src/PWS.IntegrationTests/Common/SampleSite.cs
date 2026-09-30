using System.Text;

namespace PWS.IntegrationTests.Common;

/// <summary>
/// Contenuto di un sito statico di esempio: pagina iniziale, sottopagina servita
/// come directory, asset testuali e un file binario.
/// </summary>
public static class SampleSite
{
    public const string IndexHtml =
        "<!doctype html><html><head><title>Home</title></head><body><h1>PWS home</h1></body></html>";

    public const string GuideHtml =
        "<!doctype html><html><head><title>Guida</title></head><body><h1>Guida</h1></body></html>";

    public const string Css = "body { color: #123456; }";

    public const string Js = "console.log('pws');";

    public static readonly byte[] Logo = CreateBytes(2048, seed: 42);

    /// <summary>File del sito, per percorso relativo alla radice.</summary>
    public static IReadOnlyDictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>
    {
        ["index.html"]       = Utf8(IndexHtml),
        ["guide/index.html"] = Utf8(GuideHtml),
        ["assets/site.css"]  = Utf8(Css),
        ["assets/app.js"]    = Utf8(Js),
        ["img/logo.png"]     = Logo,
    };

    /// <summary>Byte pseudo-casuali ma deterministici (stesso seme, stesso contenuto).</summary>
    public static byte[] CreateBytes(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
