using Microsoft.EntityFrameworkCore;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Infrastructure.Data;

public class MineWatchDbContext : DbContext
{
    public MineWatchDbContext(DbContextOptions<MineWatchDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        base.OnModelCreating(modelBuilder);
    }
}