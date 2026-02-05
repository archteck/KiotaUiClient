using KiotaUiClient.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KiotaUiClient.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

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
}
