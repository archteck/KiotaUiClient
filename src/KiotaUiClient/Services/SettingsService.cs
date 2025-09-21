using System.Globalization;

using KiotaUiClient.Data;

using Microsoft.EntityFrameworkCore;

namespace KiotaUiClient.Services;

public static class SettingsService
{
    static SettingsService() => AppDbContext.EnsureCreated();

    public static double GetDouble(string key, double defaultValue)
    {
        using var ctx = new AppDbContext();
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

    public static void SetDouble(string key, double value)
    {
        using var ctx = new AppDbContext();
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
