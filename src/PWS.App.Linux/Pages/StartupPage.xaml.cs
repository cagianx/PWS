using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Storage;
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
        try
        {
            // 1. Mostra file picker
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Seleziona un archivio .pws",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/zip" } },
                    { DevicePlatform.iOS,     new[] { "public.zip-archive" } },
                    { DevicePlatform.WinUI,   new[] { ".pws", ".zip" } },
                    { DevicePlatform.macOS,   new[] { "zip" } },
                    // Linux/GTK non ha restrizioni native, accetta tutto
                    { DevicePlatform.Unknown, new[] { "*" } },
                }),
            });

            if (result is null)
                return; // Utente ha annullato

            StatusLabel.Text = $"Apertura {result.FileName}...";
            StatusLabel.TextColor = Colors.Gray;
            StatusLabel.IsVisible = true;

            // 2. Apri e verifica il .pws
            var stream = await result.OpenReadAsync();
            var reader = await PwsReader.OpenAsync(stream);

            // 3. Mostra info verifica
            var site = reader.Sites.FirstOrDefault();
            if (site is null)
            {
                ShowError("Il file .pws non contiene siti.");
                reader.Dispose();
                return;
            }

            StatusLabel.Text = site.IsVerified
                ? $"✓ Verificato: {site.Title} ({site.FileCount} file)"
                : $"⚠ Non verificato: {site.Title} ({site.FileCount} file)";
            StatusLabel.TextColor = site.IsVerified ? Colors.Green : Colors.Orange;

            // 4. Registra il provider nel servizio e naviga al browser
            await Task.Delay(800); // Mostra il messaggio per un attimo
            await OpenBrowser(reader, site.SiteId, site.EntryPoint);
        }
        catch (Exception ex)
        {
            ShowError($"Errore: {ex.Message}");
        }
    }

    private async Task OpenBrowser(PwsReader reader, string defaultSiteId, string entryPoint)
    {
        // Crea il provider e registralo nel servizio globale
        var provider = new PwsContentProvider(reader, defaultSiteId);
        
        var services = IPlatformApplication.Current!.Services;
        var pwsFileService = services.GetRequiredService<PwsFileService>();
        pwsFileService.SetProvider(provider);

        // Naviga alla pagina browser
        var browserPage = new BrowserPage();
        await Navigation.PushAsync(browserPage);

        // Dopo che la pagina è apparsa, naviga al file entry point
        // (il ViewModel viene risolto nel costruttore di BrowserPage)
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100); // Attendi che la pagina sia pronta
            var vm = services.GetRequiredService<BrowserViewModel>();
            await vm.NavigateToUri($"pack://{defaultSiteId}/{entryPoint}");
        });
    }

    private void ShowError(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Colors.Red;
        StatusLabel.IsVisible = true;
    }
}




