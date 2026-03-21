namespace PWS.Format.Filesystem;

/// <summary>
/// Read-only virtual filesystem over the contents of a .pws archive.
/// <para>
/// The filesystem is valid only while the owning <see cref="Reading.PwsReader"/>
/// has not been disposed.
/// </para>
/// </summary>
public interface IPwsFileSystem
{
    /// <summary>
    /// Lists all file entries, optionally filtered by <paramref name="siteId"/>.
    /// Passing <see langword="null"/> returns entries from every site.
    /// </summary>
    IReadOnlyList<PwsFileEntry> ListFiles(string? siteId = null);

    /// <summary>
    /// Opens a file stream by its full archive path (e.g. <c>sites/docs/index.html</c>).
    /// </summary>
    /// <exception cref="FileNotFoundException">Path not found in the archive.</exception>
    Stream OpenFile(string archivePath);

    /// <summary>
    /// Opens a file within a specific site by combining the site path prefix
    /// and the <paramref name="relativePath"/> (e.g. <c>assets/main.css</c>).
    /// </summary>
    /// <param name="siteId">Site identifier declared in the manifest.</param>
    /// <param name="relativePath">Path relative to the site root.</param>
    /// <exception cref="KeyNotFoundException">Site not found in manifest.</exception>
    /// <exception cref="FileNotFoundException">File not found in the archive.</exception>
    Stream OpenSiteFile(string siteId, string relativePath);

    /// <summary>Returns <see langword="true"/> if the archive contains the given archive path.</summary>
    bool FileExists(string archivePath);
}

