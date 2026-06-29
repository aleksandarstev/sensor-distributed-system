namespace Shared.Contracts;

/// <summary>
/// Alarm koji IngestionService salje NotificationServiceu
/// kada detektuje da je temperatura presla granicu.
/// NotificationService ga prosleđuje klijentima preko SignalR.
/// </summary>
public record AlarmNotification
{
    public string SensorId { get; init; } = default!;
    public double Temperature { get; init; }
    public int AlarmPriority { get; init; }   // 1, 2 ili 3
    public DateTime Timestamp { get; init; }
}