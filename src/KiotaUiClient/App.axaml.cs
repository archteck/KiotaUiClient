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

        try
        {
            // Initialize infrastructure (migrations, etc)
            var startupService = Services.GetRequiredService<IStartupService>();
            await startupService.InitializeAsync();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Exit += async (_, _) => { await DisposeServicesAsync(); };
                desktop.MainWindow = new MainWindow
                {
                    DataContext = Services.GetRequiredService<MainWindowViewModel>(),
                };
            }
        }
        catch
        {
            await DisposeServicesAsync();
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task DisposeServicesAsync()
    {
        if (Services is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            Services = null;
            return;
        }

        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
            Services = null;
        }
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
