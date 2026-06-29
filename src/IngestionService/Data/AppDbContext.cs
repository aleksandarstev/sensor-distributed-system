using IngestionService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IngestionService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SensorReadingEntity> SensorReadings { get; set; }
    public DbSet<SensorRegistryEntity> SensorRegistry { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SensorReadings tabela
        modelBuilder.Entity<SensorReadingEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SensorId).IsRequired();
            entity.Property(e => e.Quality).IsRequired();
            entity.HasIndex(e => e.SensorId);
            entity.HasIndex(e => e.ReceivedAt);
        });

        // SensorRegistry tabela
        modelBuilder.Entity<SensorRegistryEntity>(entity =>
        {
            entity.HasKey(e => e.SensorId);
            entity.Property(e => e.Quality).IsRequired();
        });
    }
}