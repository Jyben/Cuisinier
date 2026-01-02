using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cuisinier.Infrastructure.Data;

public class CuisinierDbContextFactory : IDesignTimeDbContextFactory<CuisinierDbContext>
{
    public CuisinierDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CuisinierDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CuisinierDb;Trusted_Connection=true;TrustServerCertificate=true");

        return new CuisinierDbContext(optionsBuilder.Options);
    }
}

