using ConsensusService.Data;
using ConsensusService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConsensusService.Workers;

public class ConsensusWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;    // create a scope for each execution cycle to get a new DbContext instance
    private readonly ILogger<ConsensusWorker> _logger;      
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);    // run the consensus calculation every minute

    public ConsensusWorker(IServiceScopeFactory scopeFactory, ILogger<ConsensusWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsensusCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during consensus calculation cycle.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    // runs a single consensus calculation cycle,
    // fetching readings from the last minute
    // and updating the database with the consensus result and any malicious sensors
    private async Task RunConsensusCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var windowStart = DateTime.UtcNow - Interval;

        var readings = await db.SensorReadings
            .Where(r => r.ReceivedAt >= windowStart && !r.IsConsensus)
            .ToListAsync(ct);

        if (readings.Count == 0)
        {
            _logger.LogInformation("No data to calculate consensus for the last minute.");  
            return;
        }

        var result = BftConsensus.Calculate(readings);

        db.SensorReadings.Add(new SensorReadingEntity
        {
            SensorId = "CONSENSUS",
            Temperature = result.ConsensusValue,
            Quality = "GOOD",
            AlarmPriority = 0,
            ReceivedAt = DateTime.UtcNow,
            IsConsensus = true
        });

        foreach (var sensorId in result.MaliciousSensorIds)
        {
            var registryEntry = await db.SensorRegistry
                .FirstOrDefaultAsync(s => s.SensorId == sensorId, ct);

            if (registryEntry is not null && registryEntry.Quality != "BAD")
            {
                registryEntry.Quality = "BAD";
                _logger.LogWarning("Sensor {SensorId} marked as malicious.", sensorId);
            }
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Consensus calculated: Value={ConsensusValue}, SampleCount={SampleCount}, MaliciousSensors={MaliciousCount}",
            result.ConsensusValue, result.SampleCount, result.MaliciousSensorIds.Count);
    }
}