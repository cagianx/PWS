using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using PWS.App.Linux.Pages;
using PWS.App.Linux.Services;
using Serilog;

namespace PWS.App.Linux;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        RegisterGlobalExceptionHandlers();

        MainPage = new NavigationPage(new StartupPage());
    }

    // ── Handler globali per eccezioni non catturate ───────────────────────

    private void RegisterGlobalExceptionHandlers()
    {
        // Eccezioni su thread non-UI (es. thread pool senza await corretto)
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "AppDomain.UnhandledException (IsTerminating={T})", args.IsTerminating);
            Console.Error.WriteLine($"[FATAL] AppDomain exception: {ex}");
            Log.CloseAndFlush(); // flush prima di un eventuale terminazione
        };

        // Task non awaited che lanciano eccezioni e non vengono mai osservate
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved(); // impedisce il crash del processo
            var ex = args.Exception;
            Log.Error(ex, "TaskScheduler.UnobservedTaskException");
            Console.Error.WriteLine($"[ERROR] Unobserved task exception: {ex}");

            // Prova a mostrare il dialog UI (best-effort: siamo su un thread di background)
            var svc = IPlatformApplication.Current?.Services
                          .GetService<ErrorDialogService>();
            if (svc is not null)
                _ = svc.ShowAsync(ex, "Task non osservato");
        };
    }
}

