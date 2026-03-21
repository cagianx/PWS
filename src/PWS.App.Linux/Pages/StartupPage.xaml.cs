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
        try
        {
            var services = IPlatformApplication.Current!.Services;
            var archivePicker = services.GetRequiredService<IPwsArchivePicker>();

            // 1. Mostra file chooser nativo GTK
            var path = await archivePicker.PickAsync();

            if (string.IsNullOrWhiteSpace(path))
                return; // Utente ha annullato

            StatusLabel.Text = $"Apertura {Path.GetFileName(path)}...";
            StatusLabel.TextColor = Colors.Gray;
            StatusLabel.IsVisible = true;

            // 2. Apri e verifica il .pws
            var reader = await PwsReader.OpenAsync(path);

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

        var browserPage = new BrowserPage();

        // Usa il ViewModel della pagina appena creata, non un nuovo transient dal container
        if (browserPage.BindingContext is BrowserViewModel vm)
            await vm.NavigateToUri($"pack://{defaultSiteId}/{entryPoint}");

        await Navigation.PushAsync(browserPage);
    }

    private void ShowError(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Colors.Red;
        StatusLabel.IsVisible = true;
    }
}




