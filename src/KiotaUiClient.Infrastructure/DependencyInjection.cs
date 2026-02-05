using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Infrastructure.Data;
using KiotaUiClient.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KiotaUiClient.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Infrastructure Directory Setup
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(baseDir, "KiotaUiClient");
        Directory.CreateDirectory(appDir);
        var dbPath = Path.Combine(appDir, "kiotauiclient.sqlite");

        // Database
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Services
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IKiotaService, KiotaService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        return services;
    }
}
