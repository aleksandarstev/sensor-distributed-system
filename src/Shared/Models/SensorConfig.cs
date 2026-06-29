using Shared.Contracts;

namespace Shared.Models;

/// <summary>
/// Konfiguracija senzora koja se dodjeljuje pri pokretanju simulacije.
/// </summary>
public record SensorConfig
{
    public string SensorId { get; init; } = default!;
    public double TempMin { get; init; }
    public double TempMax { get; init; }
    public DataQuality InitialQuality { get; init; }
    public AlarmThresholds Alarms { get; init; } = default!;
}

public record AlarmThresholds
{
    public double Priority1 { get; init; }
    public double Priority2 { get; init; }
    public double Priority3 { get; init; }
}