using PWS.Core.Models;
using PWS.Core.Providers;

namespace PWS.IntegrationTests.Providers;

/// <summary>
/// <see cref="CompositeContentProvider"/>: ogni richiesta va alla prima sorgente,
/// nell'ordine di registrazione, che dichiara di gestire l'URI.
/// </summary>
[TestFixture]
public sealed class CompositeContentProviderTests
{
    [Test]
    public async Task Se_piu_sorgenti_gestiscono_l_uri_risponde_la_prima_registrata()
    {
        var first  = new InMemoryContentProvider().Register("pagina", "<p>prima</p>");
        var second = new InMemoryContentProvider().Register("pagina", "<p>seconda</p>");
        var composite = new CompositeContentProvider([first, second]);

        using var response = await composite.GetAsync(ContentRequest.Get("pws://pagina"));

        (await ReadTextAsync(response)).Should().Be("<p>prima</p>");
    }

    [Test]
    public async Task Una_sorgente_che_non_gestisce_l_uri_viene_saltata()
    {
        var app = new InMemoryContentProvider("app").Register("pagina", "<p>app</p>");
        var pws = new InMemoryContentProvider("pws").Register("pagina", "<p>pws</p>");
        var composite = new CompositeContentProvider([app, pws]);

        using var response = await composite.GetAsync(ContentRequest.Get("pws://pagina"));

        (await ReadTextAsync(response)).Should().Be("<p>pws</p>");
    }

    [Test]
    public void Dichiara_di_gestire_un_uri_se_almeno_una_sorgente_lo_gestisce()
    {
        var composite = new CompositeContentProvider(
            [new InMemoryContentProvider("app"), new InMemoryContentProvider("pws")]);

        composite.CanHandle(new Uri("pws://home")).Should().BeTrue();
        composite.CanHandle(new Uri("app://home")).Should().BeTrue();
        composite.CanHandle(new Uri("ftp://home")).Should().BeFalse();
    }

    [Test]
    public void Senza_sorgenti_non_gestisce_alcun_uri()
    {
        var composite = new CompositeContentProvider([]);

        composite.CanHandle(new Uri("pws://home")).Should().BeFalse();
    }

    [Test]
    public async Task Una_richiesta_che_nessuna_sorgente_gestisce_fallisce_con_errore_esplicito()
    {
        var composite = new CompositeContentProvider([new InMemoryContentProvider()]);

        var act = () => composite.GetAsync(ContentRequest.Get("ftp://file"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ftp://file*");
    }

    private static async Task<string> ReadTextAsync(ContentResponse response)
    {
        using var reader = new StreamReader(response.Content);
        return await reader.ReadToEndAsync();
    }
}
