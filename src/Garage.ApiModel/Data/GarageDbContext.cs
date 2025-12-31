using System.Text.Json;
using Garage.ApiModel.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Garage.ApiModel.Data;

public class GarageDbContext : DbContext
{
    public GarageDbContext(DbContextOptions<GarageDbContext> options) : base(options)
    {
    }

    public DbSet<Winner> Winners => Set<Winner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Winner>(entity =>
        {
            entity.HasKey(w => w.Year);
            entity.Property(w => w.Year).ValueGeneratedNever();
            entity.Property(w => w.Manufacturer).IsRequired().HasMaxLength(100);
            entity.Property(w => w.Model).IsRequired().HasMaxLength(100);
            entity.Property(w => w.Engine).IsRequired().HasMaxLength(200);
            entity.Property(w => w.Class).IsRequired().HasMaxLength(50);
            entity.Property(w => w.Drivers)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>()
                )
                .HasColumnType("TEXT");
            entity.Property(w => w.IsOwned).HasDefaultValue(false);
        });
    }
}
