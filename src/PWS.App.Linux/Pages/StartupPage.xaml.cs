using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using PWS.App.Linux.Services;
using PWS.App.Linux.ViewModels;
using PWS.Core.Providers;
using PWS.Format.Reading;

namespace PWS.App.Linux.Pages;

/// <summary>
/// Pagina di startup che mostra un file picker per aprire un archivio .pws.
/// Una volta selezionato e verificato, apre il browser.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class StartupPage : ContentPage
{
    public StartupPage()
    {
        InitializeComponent();
    }

    private async void OnOpenFileClicked(object? sender, EventArgs e)
    {
        var services     = IPlatformApplication.Current!.Services;
        var errorService = services.GetRequiredService<ErrorDialogService>();

        try
        {
            var archivePicker = services.GetRequiredService<IPwsArchivePicker>();

            // 1. Mostra file chooser nativo GTK
            var path = await archivePicker.PickAsync();
            if (string.IsNullOrWhiteSpace(path))
                return; // Utente ha annullato

            SetStatus($"Apertura {Path.GetFileName(path)}…", Colors.Gray);

            // 2. Apri e verifica il .pws
            var reader = await PwsReader.OpenAsync(path);

            // 3. Mostra info verifica
            var site = reader.Sites.FirstOrDefault();
            if (site is null)
            {
                reader.Dispose();
                SetStatus("Il file .pws non contiene siti.", Colors.Red);
                return;
            }

            SetStatus(
                site.IsVerified
                    ? $"✓ Verificato: {site.Title} ({site.FileCount} file)"
                    : $"⚠ Non verificato: {site.Title} ({site.FileCount} file)",
                site.IsVerified ? Colors.Green : Colors.Orange);

            // 4. Piccola pausa per mostrare lo stato, poi naviga al browser
            await Task.Delay(600);
            await OpenBrowserAsync(reader, site.SiteId, site.EntryPoint, errorService);
        }
        catch (Exception ex)
        {
            SetStatus($"Errore: {ex.Message}", Colors.Red);
            await errorService.ShowAsync(ex, "Apertura file .pws");
        }
    }

    private async Task OpenBrowserAsync(
        PwsReader          reader,
        string             defaultSiteId,
        string             entryPoint,
        ErrorDialogService errorService)
    {
        try
        {
            var services       = IPlatformApplication.Current!.Services;
            var pwsFileService = services.GetRequiredService<PwsFileService>();

            var provider = new PwsContentProvider(reader, defaultSiteId);
            pwsFileService.SetProvider(provider);

            var browserPage = new BrowserPage();

            // Naviga prima di fare PushAsync così HtmlContent è già pronto
            // e OnAppearing non ri-inizializza con pws://home
            if (browserPage.BindingContext is BrowserViewModel vm)
                await vm.NavigateToUri($"pack://{defaultSiteId}/{entryPoint}");

            await Navigation.PushAsync(browserPage);
        }
        catch (Exception ex)
        {
            SetStatus($"Errore apertura browser: {ex.Message}", Colors.Red);
            await errorService.ShowAsync(ex, "Apertura browser");
        }
    }

    private void SetStatus(string message, Color color)
    {
        StatusLabel.Text      = message;
        StatusLabel.TextColor = color;
        StatusLabel.IsVisible = true;
    }
}

