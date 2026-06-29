namespace ConsensusService.Data.Entities;

public class SensorReadingEntity
{
    public long Id { get; set; }
    public string SensorId { get; set; } = default!;
    public double Temperature { get; set; }
    public string Quality { get; set; } = default!;
    public int AlarmPriority { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool IsConsensus { get; set; }
}