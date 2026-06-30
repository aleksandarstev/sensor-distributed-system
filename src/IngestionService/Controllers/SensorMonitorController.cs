using IngestionService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IngestionService.Controllers;

[ApiController]
[Route("api/sensors")]
public class SensorMonitorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SensorMonitorController> _logger;

    public SensorMonitorController(AppDbContext db, ILogger<SensorMonitorController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Temporarily blocks a sensor for 30 seconds. During this time, the sensor will not be able to send data to the ingestion service.
    /// </summary>
    [HttpPost("{sensorId}/block")]
    public async Task<IActionResult> BlockSensor(string sensorId)
    {
        var sensor = await _db.SensorRegistry
            .FirstOrDefaultAsync(s => s.SensorId == sensorId);

        if (sensor is null)
            return NotFound($"Sensor '{sensorId}' not found in the registry.");

        if (sensor.IsBlocked)
            return BadRequest($"Sensor '{sensorId}' is already blocked until {sensor.BlockedUntil}.");

        sensor.IsBlocked = true;
        sensor.IsActive = false;
        sensor.BlockedUntil = DateTime.UtcNow.AddSeconds(30);

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Sensor '{SensorId}' has been blocked until {BlockedUntil}.",
            sensorId, sensor.BlockedUntil);

        return Ok(new
        {
            sensorId,
            blockedUntil = sensor.BlockedUntil,
            message = $"Sensor '{sensorId}' has been blocked for 30 seconds."
        });
    }

    /// <summary>
    /// Returns the current status of all sensors in the registry, including their active state, blocked state, quality, and last seen timestamp.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var sensors = await _db.SensorRegistry
            .OrderBy(s => s.SensorId)
            .Select(s => new
            {
                s.SensorId,
                s.IsActive,
                s.IsBlocked,
                s.BlockedUntil,
                s.Quality,
                s.LastSeenAt
            })
            .ToListAsync();

        return Ok(sensors);
    }
}