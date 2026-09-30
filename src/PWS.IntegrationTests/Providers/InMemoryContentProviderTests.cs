using PWS.Core.Models;
using PWS.Core.Providers;

namespace PWS.IntegrationTests.Providers;

/// <summary>
/// <see cref="InMemoryContentProvider"/>: pagine integrate del browser e pagine registrate a runtime.
/// </summary>
[TestFixture]
public sealed class InMemoryContentProviderTests
{
    [TestCase("pws://home",  "Benvenuto — PWS")]
    [TestCase("pws://about", "Informazioni — PWS")]
    public async Task Le_pagine_predefinite_sono_servite_come_html(string uri, string expectedTitle)
    {
        var provider = new InMemoryContentProvider();

        using var response = await provider.GetAsync(ContentRequest.Get(uri));

        response.StatusCode.Should().Be(200);
        response.MimeType.Should().Be("text/html; charset=utf-8");
        response.Title.Should().Be(expectedTitle);
        (await ReadTextAsync(response)).Should().Contain($"<title>{expectedTitle}</title>");
    }

    [Test]
    public async Task Una_pagina_statica_registrata_viene_servita_al_suo_percorso()
    {
        var provider = new InMemoryContentProvider()
            .Register("guida/intro", "<p>intro</p>", "Intro");

        using var response = await provider.GetAsync(ContentRequest.Get("pws://guida/intro"));

        response.StatusCode.Should().Be(200);
        response.Title.Should().Be("Intro");
        response.FinalUri.Should().Be("pws://guida/intro");
        (await ReadTextAsync(response)).Should().Be("<p>intro</p>");
    }

    [Test]
    public async Task Una_pagina_dinamica_viene_generata_a_ogni_richiesta()
    {
        var calls    = 0;
        var provider = new InMemoryContentProvider()
            .Register("contatore", () => ContentResponse.FromHtml($"<p>{++calls}</p>"));

        using var first  = await provider.GetAsync(ContentRequest.Get("pws://contatore"));
        using var second = await provider.GetAsync(ContentRequest.Get("pws://contatore"));

        (await ReadTextAsync(first)).Should().Be("<p>1</p>");
        (await ReadTextAsync(second)).Should().Be("<p>2</p>");
    }

    [Test]
    public async Task Registrare_un_percorso_gia_usato_sostituisce_la_pagina()
    {
        var provider = new InMemoryContentProvider()
            .Register("home", "<p>nuova home</p>", "Nuova home");

        using var response = await provider.GetAsync(ContentRequest.Get("pws://home"));

        response.Title.Should().Be("Nuova home");
        (await ReadTextAsync(response)).Should().Be("<p>nuova home</p>");
    }

    [TestCase("pws://HOME")]
    [TestCase("pws://home/")]
    [TestCase("pws://Home/")]
    public async Task Il_percorso_ignora_maiuscole_e_slash_finale(string uri)
    {
        var provider = new InMemoryContentProvider();

        using var response = await provider.GetAsync(ContentRequest.Get(uri));

        response.StatusCode.Should().Be(200);
        response.Title.Should().Be("Benvenuto — PWS");
    }

    [Test]
    public async Task Una_pagina_non_registrata_restituisce_404()
    {
        var provider = new InMemoryContentProvider();

        using var response = await provider.GetAsync(ContentRequest.Get("pws://inesistente"));

        response.StatusCode.Should().Be(404);
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public void Senza_schemi_espliciti_gestisce_solo_pws()
    {
        var provider = new InMemoryContentProvider();

        provider.CanHandle(new Uri("pws://home")).Should().BeTrue();
        provider.CanHandle(new Uri("PWS://home")).Should().BeTrue();
        provider.CanHandle(new Uri("http://home")).Should().BeFalse();
    }

    [Test]
    public void Con_schemi_espliciti_gestisce_solo_quelli()
    {
        var provider = new InMemoryContentProvider("app", "interno");

        provider.CanHandle(new Uri("app://home")).Should().BeTrue();
        provider.CanHandle(new Uri("interno://home")).Should().BeTrue();
        provider.CanHandle(new Uri("pws://home")).Should().BeFalse();
    }

    private static async Task<string> ReadTextAsync(ContentResponse response)
    {
        using var reader = new StreamReader(response.Content);
        return await reader.ReadToEndAsync();
    }
}
