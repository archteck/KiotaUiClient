using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Infrastructure;
using KiotaUiClient.Services;
using KiotaUiClient.ViewModels;
using KiotaUiClient.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KiotaUiClient;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        // Initialize infrastructure (migrations, etc)
        var startupService = Services.GetRequiredService<IStartupService>();
        await startupService.InitializeAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Infrastructure
        services.AddInfrastructure();

        // UI Services
        services.AddSingleton<IUiService, UiService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
    }
}
