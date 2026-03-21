using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using PWS.App.Linux.Pages;

namespace PWS.App.Linux;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new NavigationPage(new StartupPage());
    }
}

