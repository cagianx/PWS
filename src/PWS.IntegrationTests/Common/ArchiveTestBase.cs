using System.Text;
using PWS.Core.Providers;
using PWS.Format.Crypto;
using PWS.Format.Packing;
using PWS.Format.Reading;

namespace PWS.IntegrationTests.Common;

/// <summary>
/// Base per i test che lavorano su archivi .pws reali.
/// <para>
/// Ogni test riceve una directory temporanea propria (<see cref="WorkDir"/>), creata prima
/// del test e cancellata dopo: nessuno stato condiviso tra test, nessun residuo su disco.
/// </para>
/// </summary>
public abstract class ArchiveTestBase
{
    /// <summary>Directory temporanea dedicata al test corrente.</summary>
    protected string WorkDir { get; private set; } = string.Empty;

    [SetUp]
    public void CreateWorkDir()
    {
        WorkDir = Path.Combine(Path.GetTempPath(), "pws-it-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkDir);
    }

    [TearDown]
    public void DeleteWorkDir()
    {
        if (Directory.Exists(WorkDir))
            Directory.Delete(WorkDir, recursive: true);
    }

    /// <summary>Scrive su disco un sito con i file indicati e ne restituisce la directory.</summary>
    protected string CreateSiteDirectory(string name, IReadOnlyDictionary<string, byte[]> files)
    {
        var root = Path.Combine(WorkDir, "src-" + name);
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, content);
        }
        return root;
    }

    /// <summary>Scrive su disco il <see cref="SampleSite"/> e ne restituisce la directory.</summary>
    protected string CreateSiteDirectory(string name) => CreateSiteDirectory(name, SampleSite.Files);

    /// <summary>Percorso di un file (non ancora esistente) dentro <see cref="WorkDir"/>.</summary>
    protected string PathInWorkDir(string fileName) => Path.Combine(WorkDir, fileName);

    /// <summary>Crea con <see cref="PwsPacker"/> un archivio .pws con i siti indicati.</summary>
    protected async Task<string> PackAsync(
        IPwsSigningKey key,
        params (string Id, string Title, string SourceDirectory)[] sites)
    {
        var output = PathInWorkDir($"archivio-{Guid.NewGuid():N}.pws");

        await new PwsPacker().PackAsync(
            new PwsPackOptions
            {
                Sites = sites
                    .Select(s => new PwsSiteSource
                    {
                        Id              = s.Id,
                        Title           = s.Title,
                        SourceDirectory = s.SourceDirectory,
                    })
                    .ToList(),
                SigningKey = key,
            },
            output);

        return output;
    }

    /// <summary>Crea un archivio non firmato con il <see cref="SampleSite"/> come sito <c>docs</c>.</summary>
    protected Task<string> PackSampleSiteAsync(string siteId = "docs") =>
        PackAsync(PwsSigningKey.None(), (siteId, "Documentazione", CreateSiteDirectory(siteId)));

    /// <summary>Apre l'archivio da disco e lo espone con un <see cref="PwsContentProvider"/>.</summary>
    protected static async Task<PwsContentProvider> OpenProviderAsync(
        string pwsPath,
        string? defaultSiteId = null)
    {
        var reader = await PwsReader.OpenAsync(pwsPath);
        return new PwsContentProvider(reader, defaultSiteId);
    }

    /// <summary>
    /// <see cref="HttpClient"/> che non passa da eventuali proxy di sistema:
    /// i test parlano solo con server su 127.0.0.1.
    /// </summary>
    protected static HttpClient CreateHttpClient(Uri? baseAddress = null) =>
        new(new SocketsHttpHandler { UseProxy = false }) { BaseAddress = baseAddress };

    protected static async Task<string> ReadTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    protected static async Task<byte[]> ReadBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
