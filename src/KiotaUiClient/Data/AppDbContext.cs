using Microsoft.EntityFrameworkCore;

namespace KiotaUiClient.Data;

public class AppDbContext : DbContext
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    private static string GetDatabasePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(baseDir, "KiotaUiClient");
        Directory.CreateDirectory(appDir);
        var dbPath = Path.Combine(appDir, "kiotauiclient.sqlite");
        return dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = GetDatabasePath();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>()
            .HasIndex(x => x.Key)
            .IsUnique();
        modelBuilder.Entity<AppSetting>()
            .Property(x => x.Key)
            .HasMaxLength(128)
            .IsRequired();
        modelBuilder.Entity<AppSetting>()
            .Property(x => x.Value)
            .HasMaxLength(250)
            .IsRequired();
    }

    public static void EnsureCreated()
    {
        using var ctx = new AppDbContext();
        // Apply pending migrations (creates DB if it doesn't exist)
        ctx.Database.Migrate();
    }
}
