using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Platform.Maui.Linux.Gtk4.Hosting;
using PWS.App.Linux.Services;
using PWS.App.Linux.ViewModels;
using PWS.Core.Abstractions;
using PWS.Core.Models;
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

        // ── Services ────────────────────────────────────────────────
        // Servizio che mantiene il PwsContentProvider corrente
        builder.Services.AddSingleton<PwsFileService>();
        builder.Services.AddSingleton<IPwsArchivePicker, GtkPwsArchivePicker>();

        // ── Core (PWS.Core) ─────────────────────────────────────────
        // Provider in-memory per schema pws://
        builder.Services.AddSingleton<InMemoryContentProvider>(_ =>
            new InMemoryContentProvider("pws"));

        // Provider composito dinamico: include PwsContentProvider quando disponibile
        builder.Services.AddSingleton<IContentProvider>(sp =>
        {
            var inMemoryProvider = sp.GetRequiredService<InMemoryContentProvider>();
            var pwsFileService   = sp.GetRequiredService<PwsFileService>();

            // DynamicCompositeContentProvider che delega a pwsFileService.CurrentProvider
            return new DynamicCompositeContentProvider(inMemoryProvider, pwsFileService);
        });

        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // ── UI (PWS.App) ────────────────────────────────────────────
        builder.Services.AddTransient<BrowserViewModel>();

        return builder.Build();
    }

    /// <summary>
    /// CompositeContentProvider che include dinamicamente il PwsContentProvider
    /// dal PwsFileService quando disponibile.
    /// </summary>
    private sealed class DynamicCompositeContentProvider : IContentProvider
    {
        private readonly InMemoryContentProvider _inMemory;
        private readonly PwsFileService _pwsFileService;

        public DynamicCompositeContentProvider(
            InMemoryContentProvider inMemory,
            PwsFileService pwsFileService)
        {
            _inMemory = inMemory;
            _pwsFileService = pwsFileService;
        }

        public bool CanHandle(Uri uri)
        {
            if (_inMemory.CanHandle(uri)) return true;
            return _pwsFileService.CurrentProvider?.CanHandle(uri) ?? false;
        }

        public Task<ContentResponse> GetAsync(ContentRequest request, CancellationToken cancellationToken = default)
        {
            if (_inMemory.CanHandle(request.Uri))
                return _inMemory.GetAsync(request, cancellationToken);

            if (_pwsFileService.CurrentProvider?.CanHandle(request.Uri) == true)
                return _pwsFileService.CurrentProvider.GetAsync(request, cancellationToken);

            return Task.FromResult(ContentResponse.Error(404,
                $"Nessun provider registrato per lo schema '{request.Uri.Scheme}'."));
        }
    }
}


