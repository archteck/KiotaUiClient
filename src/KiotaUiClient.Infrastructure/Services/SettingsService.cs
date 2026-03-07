using System.Globalization;
using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Core.Domain.Entities;
using KiotaUiClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KiotaUiClient.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SettingsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<double> GetDoubleAsync(string key, double defaultValue)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync();
        var entry = await ctx.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
        if (entry?.Value is null)
        {
            return defaultValue;
        }

        if (double.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    public async Task SetDoubleAsync(string key, double value)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync();
        var entry = await ctx.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (entry is null)
        {
            entry = new AppSetting { Key = key, Value = value.ToString(CultureInfo.InvariantCulture) };
            ctx.AppSettings.Add(entry);
        }
        else
        {
            entry.Value = value.ToString(CultureInfo.InvariantCulture);
        }

        await ctx.SaveChangesAsync();
    }
}
