using IngestionService.Data;
using IngestionService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;
using System.Net.Http.Json;

namespace IngestionService.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController : ControllerBase
{
    private static readonly object ConsoleLock = new();

    private readonly AppDbContext _db;
    private readonly ILogger<IngestController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public IngestController(AppDbContext db, ILogger<IngestController> logger, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] SensorMessage message)
    {
        if (message is null)
            return BadRequest("Poruka je prazna");

        try
        {
            var registry = await _db.SensorRegistry.FindAsync(message.SensorId);
            var currentQuality = registry?.Quality ?? "GOOD";

            var reading = new SensorReadingEntity
            {
                SensorId = message.SensorId,
                Temperature = message.Temperature,
                Quality = currentQuality,
                AlarmPriority = message.AlarmPriority,
                ReceivedAt = DateTime.UtcNow,
                IsConsensus = false
            };

            _db.SensorReadings.Add(reading);

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

            if (message.AlarmPriority == 0) return Ok();

            lock (ConsoleLock)
            {
                Console.ForegroundColor = message.AlarmPriority switch
                {
                    1 => ConsoleColor.Yellow,
                    2 => ConsoleColor.DarkYellow,
                    3 => ConsoleColor.Red,
                    _ => ConsoleColor.White
                };
                Console.WriteLine($"[{message.SensorId}] {message.Temperature:F2}°C @ {message.SentAt:HH:mm:ss} | Alarm: {message.AlarmPriority}");
                Console.ResetColor();
            }

            await NotifyAlarmAsync(message);

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

    private async Task NotifyAlarmAsync(SensorMessage message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            var alarm = new AlarmNotification
            {
                SensorId = message.SensorId,
                Temperature = message.Temperature,
                AlarmPriority = message.AlarmPriority,
                Timestamp = message.SentAt
            };

            var response = await client.PostAsJsonAsync("api/notify", alarm);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("NotificationService je odgovorio sa {StatusCode} za senzor {SensorId}", response.StatusCode, message.SensorId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Slanje alarma ka NotificationService-u nije uspelo za senzor {SensorId}: {Error}", message.SensorId, ex.Message);
        }
    }
}