namespace PWS.App.Linux.Services;

/// <summary>
/// Astrazione del picker per archivi <c>.pws</c>.
/// Su Linux/GTK usa un dialog nativo GTK, evitando MAUI Essentials FilePicker
/// che può non essere implementato dal backend corrente.
/// </summary>
public interface IPwsArchivePicker
{
    /// <summary>
    /// Mostra un dialog nativo per selezionare un archivio <c>.pws</c>.
    /// </summary>
    /// <returns>Percorso assoluto del file selezionato, oppure <see langword="null"/> se annullato.</returns>
    Task<string?> PickAsync(CancellationToken cancellationToken = default);
}

