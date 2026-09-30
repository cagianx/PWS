using PWS.Core.Abstractions;
using PWS.Core.Navigation;
using PWS.Core.Providers;
using PWS.IntegrationTests.Common;

namespace PWS.IntegrationTests.Navigation;

/// <summary>
/// <see cref="NavigationService"/> con la stessa composizione di provider dell'app:
/// prima l'archivio .pws aperto, poi le pagine in memoria (<c>pws://home</c>, <c>pws://about</c>).
/// </summary>
[TestFixture]
public sealed class NavigationServiceTests : ArchiveTestBase
{
    /// <summary>Esito di una navigazione, letto dentro l'evento (la risposta viene poi rilasciata).</summary>
    private sealed record Visit(Uri Uri, string? Title, int StatusCode, string Body);

    private PwsContentProvider? _archive;

    [TearDown]
    public void DisposeArchive() => _archive?.Dispose();

    private async Task<(NavigationService Navigation, List<Visit> Visits)> CreateBrowserAsync()
    {
        var archive    = await OpenProviderAsync(await PackSampleSiteAsync());
        _archive       = archive;
        var composite  = new CompositeContentProvider([archive, new InMemoryContentProvider()]);
        var navigation = new NavigationService(composite);
        var visits     = new List<Visit>();

        navigation.Navigated += (_, e) =>
        {
            var body = e.Response is null ? string.Empty : new StreamReader(e.Response.Content).ReadToEnd();
            visits.Add(new Visit(e.Entry.Uri, e.Entry.Title, e.Response?.StatusCode ?? 0, body));
        };

        return (navigation, visits);
    }

    [Test]
    public async Task NavigateAsync_verso_un_file_dell_archivio_mostra_la_pagina()
    {
        var (navigation, visits) = await CreateBrowserAsync();

        await navigation.NavigateAsync(new Uri("pws://docs/index.html"));

        navigation.Current!.Uri.Should().Be(new Uri("pws://docs/index.html"));
        visits.Should().ContainSingle();
        visits[0].StatusCode.Should().Be(200);
        visits[0].Body.Should().Be(SampleSite.IndexHtml);
    }

    [Test]
    public async Task NavigateAsync_verso_una_pagina_in_memoria_usa_il_provider_in_memoria()
    {
        var (navigation, visits) = await CreateBrowserAsync();

        await navigation.NavigateAsync(new Uri("pws://home"));

        navigation.Current!.Title.Should().Be("Benvenuto — PWS");
        visits.Should().ContainSingle().Which.Body.Should().Contain("PWS Browser");
    }

    [Test]
    public async Task NavigateAsync_verso_file_mancante_mostra_errore_404()
    {
        var (navigation, visits) = await CreateBrowserAsync();

        await navigation.NavigateAsync(new Uri("pws://docs/manca.html"));

        visits.Should().ContainSingle().Which.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task NavigateAsync_verso_schema_senza_provider_mostra_errore_404()
    {
        var (navigation, visits) = await CreateBrowserAsync();

        await navigation.NavigateAsync(new Uri("ftp://esempio.it/file.txt"));

        visits.Should().ContainSingle().Which.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Navigating_precede_Navigated()
    {
        var (navigation, _) = await CreateBrowserAsync();
        var events = new List<string>();
        navigation.Navigating += (_, _) => events.Add("navigating");
        navigation.Navigated  += (_, _) => events.Add("navigated");

        await navigation.NavigateAsync(new Uri("pws://docs/index.html"));

        events.Should().Equal("navigating", "navigated");
    }

    [Test]
    public async Task GoBack_e_GoForward_ripercorrono_la_cronologia()
    {
        var (navigation, visits) = await CreateBrowserAsync();
        await navigation.NavigateAsync(new Uri("pws://docs/index.html"));
        await navigation.NavigateAsync(new Uri("pws://docs/guide/index.html"));

        await navigation.GoBackAsync();

        navigation.Current!.Uri.Should().Be(new Uri("pws://docs/index.html"));
        navigation.CanGoBack.Should().BeFalse();
        navigation.CanGoForward.Should().BeTrue();
        visits[^1].Body.Should().Be(SampleSite.IndexHtml);

        await navigation.GoForwardAsync();

        navigation.Current!.Uri.Should().Be(new Uri("pws://docs/guide/index.html"));
        navigation.CanGoForward.Should().BeFalse();
        visits[^1].Body.Should().Be(SampleSite.GuideHtml);
    }

    [Test]
    public async Task NavigateAsync_dopo_GoBack_tronca_la_cronologia_in_avanti()
    {
        var (navigation, _) = await CreateBrowserAsync();
        await navigation.NavigateAsync(new Uri("pws://docs/index.html"));
        await navigation.NavigateAsync(new Uri("pws://docs/guide/index.html"));
        await navigation.GoBackAsync();

        await navigation.NavigateAsync(new Uri("pws://home"));

        navigation.CanGoForward.Should().BeFalse();
        navigation.CanGoBack.Should().BeTrue();
    }

    [Test]
    public async Task GoBack_senza_cronologia_non_naviga()
    {
        var (navigation, visits) = await CreateBrowserAsync();
        await navigation.NavigateAsync(new Uri("pws://docs/index.html"));

        await navigation.GoBackAsync();

        visits.Should().ContainSingle();
        navigation.Current!.Uri.Should().Be(new Uri("pws://docs/index.html"));
    }

    [Test]
    public async Task RefreshAsync_ricarica_la_pagina_corrente()
    {
        var (navigation, visits) = await CreateBrowserAsync();
        await navigation.NavigateAsync(new Uri("pws://docs/assets/site.css"));

        await navigation.RefreshAsync();

        visits.Should().HaveCount(2);
        visits.Should().AllSatisfy(v => v.Body.Should().Be(SampleSite.Css));
        navigation.CanGoBack.Should().BeFalse();
    }
}
