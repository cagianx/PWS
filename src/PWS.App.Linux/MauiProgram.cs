using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Platform.Maui.Linux.Gtk4.Hosting;
using PWS.App.Linux.ViewModels;
using PWS.Core.Abstractions;
using PWS.Core.Navigation;
using PWS.Core.Providers;

namespace PWS.App.Linux;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiAppLinuxGtk4<App>();

        // ── Core (PWS.Core) ─────────────────────────────────────────
        // Provider in-memory per schema pws://
        builder.Services.AddSingleton<InMemoryContentProvider>(_ =>
            new InMemoryContentProvider("pws"));

        // Provider composito: aggiungere altri provider qui in futuro
        // (es. ApiContentProvider per api:// o http://)
        builder.Services.AddSingleton<IContentProvider>(sp =>
            new CompositeContentProvider([
                sp.GetRequiredService<InMemoryContentProvider>()
            ]));

        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // ── UI (PWS.App) ────────────────────────────────────────────
        builder.Services.AddTransient<BrowserViewModel>();

        return builder.Build();
    }
}

