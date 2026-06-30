using ConsensusService.Data;
using ConsensusService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConsensusService.Workers;

public class ConsensusWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;    // create a scope for each execution cycle to get a new DbContext instance
    private readonly ILogger<ConsensusWorker> _logger;
    private readonly Dictionary<string, int> _maliciousStreak = new();  // track how many consecutive cycles a sensor has been flagged as malicious
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

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

    // runs a single consensus calculation cycle, fetching the last minute of sensor readings,
    // calculating the consensus value, and updating the database accordingly
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

        // Update the malicious streak counts and mark sensors as BAD if they have been malicious for 2 consecutive cycles
        foreach (var sensorId in result.MaliciousSensorIds)
        {
            _maliciousStreak[sensorId] = _maliciousStreak.GetValueOrDefault(sensorId, 0) + 1;

            if (_maliciousStreak[sensorId] >= 2)    // sensor is considered malicious if it has been flagged for 2 consecutive cycles
            {
                var registryEntry = await db.SensorRegistry
                    .FirstOrDefaultAsync(s => s.SensorId == sensorId, ct);

                if (registryEntry is not null && registryEntry.Quality != "BAD")
                {
                    registryEntry.Quality = "BAD";
                    _logger.LogWarning(
                        "Sensor {SensorId} marked as BAD after {MaliciousCount} consecutive malicious cycles.",
                        sensorId, _maliciousStreak[sensorId]);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Sensor {SensorId} flagged as malicious for {MaliciousCount} consecutive cycles.",
                    sensorId, _maliciousStreak[sensorId]);
            }
        }

        foreach (var sensorId in result.TrustedSensorIds)
        {
            _maliciousStreak[sensorId] = 0;
        }

        await db.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation(
            "Consensus calculated: Value={ConsensusValue}, SampleCount={SampleCount}, MaliciousSensors={MaliciousCount}",
            result.ConsensusValue, result.SampleCount, result.MaliciousSensorIds.Count);
    }
}