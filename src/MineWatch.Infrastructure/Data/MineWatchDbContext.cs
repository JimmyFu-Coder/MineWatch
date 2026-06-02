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
    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<TelemetryReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VehicleNo).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.DeviceId);
            entity.HasOne(e => e.Device).WithMany().HasForeignKey(e => e.DeviceId);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleType).HasConversion<string>();
            entity.Property(e => e.Severity).HasConversion<string>();
            entity.HasIndex(e => e.IsEnabled);
            entity.Property(e => e.SpeedThreshold).HasDefaultValue(null);
            entity.Property(e => e.IdleSpeedThreshold).HasDefaultValue(null);
            entity.Property(e => e.IdleDurationSeconds).HasDefaultValue(null);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.TriggeredAt);
            entity.HasOne(e => e.Rule).WithMany().HasForeignKey(e => e.RuleId);
            entity.HasOne(e => e.Device).WithMany().HasForeignKey(e => e.DeviceId);
            entity.HasOne(e => e.TelemetryReading).WithMany().HasForeignKey(e => e.TelemetryReadingId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
