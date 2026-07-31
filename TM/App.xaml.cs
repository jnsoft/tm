using Microsoft.Extensions.DependencyInjection;
using TM.Services;

namespace TM;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ServiceCollection services = new();

        services.AddSingleton<UserInteractionService>();
        services.AddSingleton<ShellService>();
        services.AddSingleton<ProjectDocumentService>();
        services.AddSingleton<SecurityToolsService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Clipboard.Clear();
        serviceProvider?.Dispose();
    }
}
