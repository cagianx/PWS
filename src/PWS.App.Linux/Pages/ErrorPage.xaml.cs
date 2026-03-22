using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace PWS.App.Linux.Pages;

/// <summary>
/// Pagina modale che mostra i dettagli completi di un'eccezione (tipo, messaggio, stack trace).
/// Viene aperta da <see cref="Services.ErrorDialogService.ShowAsync"/>.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ErrorPage : ContentPage
{
    private readonly string _fullText;

    public ErrorPage(Exception ex, string? context = null)
    {
        InitializeComponent();

        if (context is not null)
        {
            ContextLabel.Text      = $"Contesto: {context}";
            ContextLabel.IsVisible = true;
        }

        MessageLabel.Text    = $"{ex.GetType().Name}: {ex.Message}";
        _fullText            = ex.ToString();        // include inner exceptions
        StackTraceLabel.Text = _fullText;
    }

    private async void OnCloseClicked(object? sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(_fullText);
        CopyButton.Text = "✓ Copiato";
        await Task.Delay(1500);
        CopyButton.Text = "📋 Copia";
    }
}

