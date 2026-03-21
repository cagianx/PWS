using Microsoft.Maui.Hosting;
using Platform.Maui.Linux.Gtk4.Platform;

namespace PWS.App;

/// <summary>
/// Entry point Linux/GTK4.
/// Eredita da GtkMauiApplication che avvia il loop GTK4 e il runtime MAUI.
/// </summary>
public class Program : GtkMauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public static void Main(string[] args)
    {
        var app = new Program();
        app.Run(args);
    }
}

