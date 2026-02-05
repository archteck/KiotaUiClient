using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KiotaUiClient.Infrastructure.Services;

public class StartupService : IStartupService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public StartupService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task InitializeAsync()
    {
        using var dbContext = await _dbFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();
    }
}
