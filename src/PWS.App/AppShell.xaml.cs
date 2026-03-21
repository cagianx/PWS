using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace PWS.App;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }
}

