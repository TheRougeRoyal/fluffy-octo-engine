using Microsoft.EntityFrameworkCore;
using TradingEngine.Data.Models;

namespace TradingEngine.Data;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options)
        : base(options) { }

    public DbSet<TradeEntity> Trades { get; set; } = null!;
    public DbSet<PortfolioSnapshotEntity> PortfolioSnapshots { get; set; } = null!;
    public DbSet<PositionSnapshotEntity> PositionSnapshots { get; set; } = null!;
    public DbSet<PerformanceMetricsEntity> PerformanceMetrics { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Trade table
        modelBuilder.Entity<TradeEntity>()
            .HasKey(t => t.Id);

        modelBuilder.Entity<TradeEntity>()
            .Property(t => t.OrderId)
            .IsRequired()
            .HasMaxLength(50);

        modelBuilder.Entity<TradeEntity>()
            .Property(t => t.ExecutedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Configure PortfolioSnapshot
        modelBuilder.Entity<PortfolioSnapshotEntity>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<PortfolioSnapshotEntity>()
            .HasMany(p => p.Positions)
            .WithOne()
            .HasForeignKey(pos => pos.PortfolioSnapshotId);

        base.OnModelCreating(modelBuilder);
    }
}
