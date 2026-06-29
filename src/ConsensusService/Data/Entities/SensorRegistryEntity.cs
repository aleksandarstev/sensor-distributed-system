namespace ConsensusService.Data.Entities;

public class SensorRegistryEntity
{
    public string SensorId { get; set; } = default!;
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime? BlockedUntil { get; set; }
    public string Quality { get; set; } = "GOOD";
    public long LastMessageSequence { get; set; }
}