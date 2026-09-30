using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using PWS.App.Linux.Services;
using PWS.App.Linux.ViewModels;
using PWS.IntegrationTests.Common;

namespace PWS.App.Linux.Tests.ViewModels;

/// <summary>
/// <see cref="BrowserViewModel"/>: barra indirizzi, stato di navigazione e comandi.
/// Logica di stato senza confini esterni: test unitari.
/// Vedi my-docs/docs/tecnologie/csharp/test-unitari/01-scopo.md.
/// </summary>
[TestFixture]
public sealed class BrowserViewModelTests : ArchiveTestBase
{
    private PwsFileService _files = null!;

    [SetUp]
    public void CreateFileService() =>
        _files = new PwsFileService(NullLoggerFactory.Instance, NullLogger<PwsFileService>.Instance);

    [TearDown]
    public void DisposeFileService() => _files.Dispose();

    private BrowserViewModel CreateViewModel() =>
        new(_files, NullLogger<BrowserViewModel>.Instance);

    // ── Barra indirizzi ──────────────────────────────────────────────────────

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("pagina.html")]
    [TestCase("/relativo/pagina.html")]
    [TestCase("pws://docs/index.html")]
    [TestCase("file:///etc/passwd")]
    [TestCase("ftp://127.0.0.1/file")]
    public void Un_url_non_valido_o_non_http_non_cambia_la_pagina(string? input)
    {
        var vm = CreateViewModel();

        vm.NavigateCommand.Execute(input);

        vm.RenderedUrl.Should().BeEmpty();
        vm.IsBusy.Should().BeFalse();
        vm.StatusMessage.Should().NotBe("Caricamento…").And.NotBeNullOrWhiteSpace();
    }

    [TestCase("http://127.0.0.1:5000/index.html")]
    [TestCase("https://example.org/")]
    [TestCase("HTTP://127.0.0.1/")]
    public void Un_url_http_assoluto_diventa_la_pagina_caricata(string url)
    {
        var vm = CreateViewModel();

        vm.NavigateCommand.Execute(url);

        vm.RenderedUrl.Should().Be(url);
        vm.AddressText.Should().Be(url);
        vm.IsBusy.Should().BeTrue();
        vm.StatusMessage.Should().Be("Caricamento…");
    }

    // ── Sito corrente ────────────────────────────────────────────────────────

    [Test]
    public void Senza_sito_aperto_invita_ad_aprire_un_file_e_non_carica_pagine()
    {
        var vm = CreateViewModel();

        vm.NavigateToCurrentSite();

        vm.RenderedUrl.Should().BeEmpty();
        vm.StatusMessage.Should().Be("Apri un file .pws per iniziare");
    }

    [Test]
    public async Task Con_un_sito_aperto_carica_l_indirizzo_del_server_loopback()
    {
        _files.SetProvider(await OpenProviderAsync(await PackSampleSiteAsync()));
        var vm = CreateViewModel();

        vm.NavigateToCurrentSite();

        vm.RenderedUrl.Should().Be(_files.CurrentServer!.BaseAddress);
        vm.IsBusy.Should().BeTrue();
    }

    // ── Ciclo di navigazione della WebView ───────────────────────────────────

    [Test]
    public void Durante_il_caricamento_il_browser_e_occupato_e_stop_e_abilitato()
    {
        var vm = CreateViewModel();

        vm.OnPageNavigating("http://127.0.0.1:5000/guide/");

        vm.AddressText.Should().Be("http://127.0.0.1:5000/guide/");
        vm.IsBusy.Should().BeTrue();
        vm.StatusMessage.Should().Be("Caricamento…");
        vm.StopCommand.CanExecute(null).Should().BeTrue();
        vm.RefreshCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void A_fine_caricamento_aggiorna_indirizzo_e_disponibilita_di_indietro_e_avanti()
    {
        var vm = CreateViewModel();
        vm.OnPageNavigating("http://127.0.0.1:5000/guide/");

        vm.OnWebViewNavigated("http://127.0.0.1:5000/guide/", canGoBack: true, canGoForward: false);

        vm.AddressText.Should().Be("http://127.0.0.1:5000/guide/");
        vm.IsBusy.Should().BeFalse();
        vm.StatusMessage.Should().Be("Completato");
        vm.GoBackCommand.CanExecute(null).Should().BeTrue();
        vm.GoForwardCommand.CanExecute(null).Should().BeFalse();
        vm.StopCommand.CanExecute(null).Should().BeFalse();
        vm.RefreshCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void Stop_interrompe_il_caricamento()
    {
        var vm = CreateViewModel();
        vm.OnPageNavigating("http://127.0.0.1:5000/");

        vm.StopCommand.Execute(null);

        vm.IsBusy.Should().BeFalse();
        vm.StatusMessage.Should().Be("Interrotto");
        vm.StopCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void All_avvio_indietro_avanti_e_stop_sono_disabilitati()
    {
        var vm = CreateViewModel();

        vm.GoBackCommand.CanExecute(null).Should().BeFalse();
        vm.GoForwardCommand.CanExecute(null).Should().BeFalse();
        vm.StopCommand.CanExecute(null).Should().BeFalse();
        vm.RefreshCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void I_comandi_notificano_il_cambio_di_abilitazione()
    {
        var vm      = CreateViewModel();
        var changed = new List<string>();
        vm.GoBackCommand.CanExecuteChanged    += (_, _) => changed.Add("back");
        vm.GoForwardCommand.CanExecuteChanged += (_, _) => changed.Add("forward");
        vm.StopCommand.CanExecuteChanged      += (_, _) => changed.Add("stop");

        vm.OnWebViewNavigated("http://127.0.0.1:5000/", canGoBack: true, canGoForward: true);

        changed.Should().Contain(["back", "forward", "stop"]);
    }

    // ── Eventi verso la View ─────────────────────────────────────────────────

    [Test]
    public void Indietro_avanti_e_ricarica_sono_delegati_alla_view()
    {
        var vm     = CreateViewModel();
        var raised = new List<string>();
        vm.GoBackRequested    += (_, _) => raised.Add("back");
        vm.GoForwardRequested += (_, _) => raised.Add("forward");
        vm.ReloadRequested    += (_, _) => raised.Add("reload");
        vm.OnWebViewNavigated("http://127.0.0.1:5000/", canGoBack: true, canGoForward: true);

        vm.GoBackCommand.Execute(null);
        vm.GoForwardCommand.Execute(null);
        vm.RefreshCommand.Execute(null);

        raised.Should().Equal("back", "forward", "reload");
    }

    // ── Notifiche di proprietà ───────────────────────────────────────────────

    [Test]
    public void Una_navigazione_notifica_le_proprieta_modificate()
    {
        var vm      = CreateViewModel();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.NavigateCommand.Execute("http://127.0.0.1:5000/");

        changed.Should().Contain(
        [
            nameof(BrowserViewModel.AddressText),
            nameof(BrowserViewModel.IsBusy),
            nameof(BrowserViewModel.StatusMessage),
            nameof(BrowserViewModel.RenderedUrl),
        ]);
    }

    [Test]
    public void Assegnare_lo_stesso_valore_non_notifica()
    {
        var vm = CreateViewModel();
        vm.AddressText = "http://127.0.0.1/";
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.AddressText = "http://127.0.0.1/";

        changed.Should().BeEmpty();
    }
}
