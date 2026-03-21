using Gio;
using Gtk;

namespace PWS.App.Linux.Services;

/// <summary>
/// Implementazione Linux/GTK del picker per archivi <c>.pws</c>.
/// Usa <see cref="Gtk.FileDialog"/> direttamente invece di <c>Microsoft.Maui.Storage.FilePicker</c>.
/// </summary>
public sealed class GtkPwsArchivePicker : IPwsArchivePicker
{
    public async Task<string?> PickAsync(CancellationToken cancellationToken = default)
    {
        var dialog = FileDialog.New();
        dialog.Title = "Seleziona un archivio .pws";

        // Parent window GTK corrente, se disponibile
        var parent = GetActiveGtkWindow();

        Gio.File? file;
        try
        {
            file = await dialog.OpenAsync(parent!);
        }
        catch
        {
            // Annullo / chiudo dialog / eccezioni GTK → trattate come no selection
            return null;
        }

        if (file is null)
            return null;

        // Preferisci il path locale; fallback a ParseName
        var path = file.GetPath();
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var parseName = file.GetParseName();
        return string.IsNullOrWhiteSpace(parseName) ? null : parseName;
    }

    private static Gtk.Window? GetActiveGtkWindow()
    {
        try
        {
            return Gio.Application.GetDefault() is Gtk.Application gtkApp
                ? gtkApp.ActiveWindow
                : null;
        }
        catch
        {
            return null;
        }
    }
}

