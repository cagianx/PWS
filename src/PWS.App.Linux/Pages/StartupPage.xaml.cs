using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<StartupPage> _logger;

    public StartupPage()
    {
        InitializeComponent();
        _logger = IPlatformApplication.Current!.Services.GetRequiredService<ILogger<StartupPage>>();
        _logger.LogDebug("StartupPage ctor: pagina inizializzata.");
    }

    private async void OnOpenFileClicked(object? sender, EventArgs e)
    {
        var services     = IPlatformApplication.Current!.Services;
        var errorService = services.GetRequiredService<ErrorDialogService>();
        _logger.LogDebug("StartupPage.OnOpenFileClicked: avvio selezione file.");

        try
        {
            var archivePicker = services.GetRequiredService<IPwsArchivePicker>();

            // 1. Mostra file chooser nativo GTK
            var path = await archivePicker.PickAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogDebug("StartupPage.OnOpenFileClicked: selezione annullata.");
                return; // Utente ha annullato
            }

            _logger.LogInformation("StartupPage.OnOpenFileClicked: file selezionato '{Path}'.", path);

            SetStatus($"Apertura {Path.GetFileName(path)}…", Colors.Gray);

            // 2. Apri e verifica il .pws
            _logger.LogDebug("StartupPage.OnOpenFileClicked: apro PwsReader per '{Path}'.", path);
            var reader = await PwsReader.OpenAsync(path, new PwsOpenOptions
            {
                Logger = _logger,
            });
            _logger.LogInformation("StartupPage.OnOpenFileClicked: PwsReader aperto. Siti={Count}", reader.Sites.Count);

            // 3. Mostra info verifica
            var site = reader.Sites.FirstOrDefault();
            if (site is null)
            {
                _logger.LogWarning("StartupPage.OnOpenFileClicked: nessun sito nel file '{Path}'.", path);
                reader.Dispose();
                SetStatus("Il file .pws non contiene siti.", Colors.Red);
                return;
            }

            _logger.LogInformation(
                "StartupPage.OnOpenFileClicked: primo sito SiteId={SiteId} Title='{Title}' Verified={Verified} Files={Files}",
                site.SiteId, site.Title, site.IsVerified, site.FileCount);

            SetStatus(
                site.IsVerified
                    ? $"✓ Verificato: {site.Title} ({site.FileCount} file)"
                    : $"⚠ Non verificato: {site.Title} ({site.FileCount} file)",
                site.IsVerified ? Colors.Green : Colors.Orange);

            // 4. Piccola pausa per mostrare lo stato, poi naviga al browser
            await Task.Delay(600);
            _logger.LogDebug("StartupPage.OnOpenFileClicked: apro BrowserPage.");
            await OpenBrowserAsync(reader, site.SiteId, errorService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupPage.OnOpenFileClicked: errore durante apertura/verifica .pws.");
            SetStatus($"Errore: {ex.Message}", Colors.Red);
            await errorService.ShowAsync(ex, "Apertura file .pws");
        }
    }

    private async Task OpenBrowserAsync(
        PwsReader          reader,
        string             defaultSiteId,
        ErrorDialogService errorService)
    {
        try
        {
            var services      = IPlatformApplication.Current!.Services;
            var pwsFileService = services.GetRequiredService<PwsFileService>();
            var logFactory    = services.GetRequiredService<ILoggerFactory>();

            _logger.LogDebug("StartupPage.OpenBrowserAsync: creo PwsContentProvider per siteId={SiteId}.", defaultSiteId);

            var provider = new PwsContentProvider(
                reader,
                defaultSiteId,
                logFactory.CreateLogger<PwsContentProvider>());

            pwsFileService.SetProvider(provider);
            _logger.LogDebug("StartupPage.OpenBrowserAsync: provider registrato in PwsFileService.");

            // BrowserPage parte vuoto — nessun URI, nessuna navigazione automatica.
            // L'utente digita pws://<siteId>/index.html nella barra indirizzi.
            _logger.LogDebug("StartupPage.OpenBrowserAsync: Navigation.PushAsync(new BrowserPage()).");
            await Navigation.PushAsync(new BrowserPage());
            _logger.LogInformation("StartupPage.OpenBrowserAsync: BrowserPage aperta con successo.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupPage.OpenBrowserAsync: errore aprendo BrowserPage.");
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

