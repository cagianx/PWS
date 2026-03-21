using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace PWS.App;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}

