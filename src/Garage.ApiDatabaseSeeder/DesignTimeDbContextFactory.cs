using Garage.ApiModel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Garage.ApiDatabaseSeeder;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GarageDbContext>
{
    public GarageDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GarageDbContext>();

        // Use a dummy connection string for design-time operations (migrations)
        // The actual connection string will be provided at runtime by Aspire
        optionsBuilder.UseNpgsql("Host=localhost;Database=garage;Username=postgres;Password=postgres",
            b => b.MigrationsAssembly("Garage.ApiDatabaseSeeder"));

        return new GarageDbContext(optionsBuilder.Options);
    }
}
