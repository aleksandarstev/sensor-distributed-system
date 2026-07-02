using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using Shared.Contracts;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/notify")]
public class NotifyController : ControllerBase
{
    private readonly IHubContext<AlarmHub> _hub;
    private readonly ILogger<NotifyController> _logger;

    public NotifyController(IHubContext<AlarmHub> hub, ILogger<NotifyController> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Notify([FromBody] AlarmNotification alarm)
    {
        if (alarm is null)
            return BadRequest("Alarm je prazan");

        await _hub.Clients.All.SendAsync("ReceiveAlarm", alarm);

        var logLevel = alarm.AlarmPriority switch
        {
            1 => LogLevel.Information,
            2 => LogLevel.Warning,
            3 => LogLevel.Error,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "Alarm prosledjen klijentima: senzor {SensorId}, temperatura {Temperature:F2}°C, prioritet {Priority}",
            alarm.SensorId, alarm.Temperature, alarm.AlarmPriority);

        return Ok();
    }
}
