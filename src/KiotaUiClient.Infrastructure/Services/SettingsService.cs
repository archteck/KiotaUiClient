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

    public double GetDouble(string key, double defaultValue)
    {
        using var ctx = _dbFactory.CreateDbContext();
        var entry = ctx.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == key);
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

    public void SetDouble(string key, double value)
    {
        using var ctx = _dbFactory.CreateDbContext();
        var entry = ctx.AppSettings.FirstOrDefault(s => s.Key == key);
        if (entry is null)
        {
            entry = new AppSetting { Key = key, Value = value.ToString(CultureInfo.InvariantCulture) };
            ctx.AppSettings.Add(entry);
        }
        else
        {
            entry.Value = value.ToString(CultureInfo.InvariantCulture);
        }

        ctx.SaveChanges();
    }
}
