using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MineWatch.Infrastructure.Data;

public class MineWatchDbContextFactory : IDesignTimeDbContextFactory<MineWatchDbContext>
{
    public MineWatchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MineWatchDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=minewatch;Username=postgres;Password=postgres");
        return new MineWatchDbContext(optionsBuilder.Options);
    }
}
