using ConsensusService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConsensusService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SensorReadingEntity> SensorReadings { get; set; }
    public DbSet<SensorRegistryEntity> SensorRegistry { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorReadingEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SensorId).IsRequired();
            entity.Property(e => e.Quality).IsRequired();
        });

        modelBuilder.Entity<SensorRegistryEntity>(entity =>
        {
            entity.HasKey(e => e.SensorId);
            entity.Property(e => e.Quality).IsRequired();
        });
    }
}