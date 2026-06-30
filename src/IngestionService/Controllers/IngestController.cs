using IngestionService.Data;
using IngestionService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace IngestionService.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<IngestController> _logger;

    public IngestController(AppDbContext db, ILogger<IngestController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] SensorMessage message)
    {
        if (message is null)
            return BadRequest("Poruka je prazna");

        try
        {
            var reading = new SensorReadingEntity
            {
                SensorId = message.SensorId,
                Temperature = message.Temperature,
                Quality = "GOOD",
                AlarmPriority = message.AlarmPriority,
                ReceivedAt = DateTime.UtcNow,
                IsConsensus = false
            };

            _db.SensorReadings.Add(reading);

            var registry = await _db.SensorRegistry.FindAsync(message.SensorId);
            if (registry is null)
            {
                _db.SensorRegistry.Add(new SensorRegistryEntity
                {
                    SensorId = message.SensorId,
                    LastSeenAt = DateTime.UtcNow,
                    IsActive = true,
                    IsBlocked = false,
                    Quality = "GOOD",
                    LastMessageSequence = message.MessageSequence
                });
            }
            else
            {
                registry.LastSeenAt = DateTime.UtcNow;
                registry.LastMessageSequence = message.MessageSequence;

                if (!registry.IsBlocked)    // only activate the sensor if it is not blocked
                    registry.IsActive = true;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Primljena poruka od senzora {SensorId}", message.SensorId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError("Greska prilikom upisivanja senzora {SensorId}: {Error}", message.SensorId, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("block/{sensorId}")]
    public async Task<IActionResult> BlockSensor(string sensorId)
    {
        var registry = await _db.SensorRegistry.FindAsync(sensorId);
        if (registry is null)
            return NotFound();

        registry.IsBlocked = true;
        registry.BlockedUntil = DateTime.UtcNow.AddSeconds(30);
        await _db.SaveChangesAsync();

        _logger.LogWarning("Senzor {SensorId} je blokiran na 30 sekundi.", sensorId);
        return Ok();
    }
}