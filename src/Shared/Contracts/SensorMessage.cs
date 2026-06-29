namespace Shared.Contracts;

public record SensorMessage
{
    public Guid MessageId { get; init; }
    public string SensorId { get; init; } = default!;
    public long MessageSequence { get; init; }
    public DateTime SentAt { get; init; }
    public double Temperature { get; init; }
    public int AlarmPriority { get; init; }
    public string EncryptedPayload { get; init; } = default!;
    public string Signature { get; init; } = default!;
}

public record SensorPayload
{
    public double Temperature { get; init; }
    public DataQuality Quality { get; init; }
    public int AlarmPriority { get; init; }
}

public enum DataQuality
{
    GOOD,
    BAD,
    UNCERTAIN
}