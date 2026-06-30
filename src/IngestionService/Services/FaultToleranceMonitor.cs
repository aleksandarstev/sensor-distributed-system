using IngestionService.Data;
using Microsoft.EntityFrameworkCore;

namespace IngestionService.Services;

/// <summary>
/// Background service that monitors the health of sensors and manages their active status based on heartbeat signals and quality.
/// 1. detects inactive sensors that haven't sent a heartbeat within a specified timeout and deactivates them.
/// 2. unblocks sensors whose block period has expired.
/// 3. ensures that at least a minimum number of sensors are active, activating reserve sensors if necessary.
/// </summary>
public class FaultToleranceMonitor : BackgroundService
{
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(10); // if a sensor hasn't sent a heartbeat in 10 seconds, it is considered inactive
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);   // run the health check every 2 seconds
    private const int RequiredActiveSensors = 5;

    private readonly IServiceScopeFactory _scopeFactory;    // create a scope for each execution cycle to get a new DbContext instance
    private readonly ILogger<FaultToleranceMonitor> _logger;

    public FaultToleranceMonitor(IServiceScopeFactory scopeFactory, ILogger<FaultToleranceMonitor> logger)
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
                await CheckSensorsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sensor health check cycle.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckSensorsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var sensors = await db.SensorRegistry.ToListAsync(ct);

        // Deativate sensors that have not sent a heartbeat within the timeout period, but only if they have ever sent data
        // (i.e., their LastSeenAt is not older than 5 minutes).
        // Reserve sensors that have never sent data are skipped.
        foreach (var sensor in sensors.Where(s => s.IsActive && !s.IsBlocked))
        {
            bool hasEverSentData = now - sensor.LastSeenAt < TimeSpan.FromHours(1);

            if (!hasEverSentData)
            {
                // Reserve sensors that have never sent data are skipped from deactivation.
                continue;
            }

            if (now - sensor.LastSeenAt > HeartbeatTimeout)
            {
                sensor.IsActive = false;
                _logger.LogWarning(
                    "Sensor {SensorId} deactivated - last seen: {LastSeenAt}, timeout: {TimeoutSeconds}s.",
                    sensor.SensorId, sensor.LastSeenAt, HeartbeatTimeout.TotalSeconds);
            }
        }

        // unblock sensors whose block period has expired
        foreach (var sensor in sensors.Where(s => s.IsBlocked))
        {
            if (sensor.BlockedUntil.HasValue && sensor.BlockedUntil.Value <= now)
            {
                sensor.IsBlocked = false;
                sensor.BlockedUntil = null;
                _logger.LogInformation(
                    "Sensor {SensorId} unblocked - block period expired.",
                    sensor.SensorId);
            }
        }

        // activate reserve sensors if the number of active sensors is below the required threshold
        var activeCount = sensors.Count(s => s.IsActive && !s.IsBlocked);

        if (activeCount < RequiredActiveSensors)
        {
            int needed = RequiredActiveSensors - activeCount;

            var candidates = sensors
                .Where(s => !s.IsActive && !s.IsBlocked && s.Quality == "GOOD")
                .OrderByDescending(s => s.LastSeenAt)
                .Take(needed)
                .ToList();

            foreach (var candidate in candidates)
            {
                candidate.IsActive = true;
                _logger.LogInformation(
                    "Senzor {SensorId} aktiviran kao zamjena (aktivnih bilo: {ActiveCount}).",
                    candidate.SensorId, activeCount);
            }

            if (candidates.Count < needed)
            {
                _logger.LogWarning(
                    "Nedovoljno rezervnih senzora! Aktivnih: {ActiveCount}, potrebno: {RequiredCount}.",
                    activeCount + candidates.Count, RequiredActiveSensors);
            }
        }

        await db.SaveChangesAsync(CancellationToken.None); 
    }
}