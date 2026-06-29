namespace IngestionService.Data.Entities;

/// <summary>
/// Svaka izmjerena vrijednost senzora koja se cuva u bazi.
/// IsConsensus = true znaci da je to konsenzus vrijednost koju je upisao ConsensusService.
/// </summary>
public class SensorReadingEntity
{
    public long Id { get; set; }
    public string SensorId { get; set; } = default!;
    public double Temperature { get; set; }
    public string Quality { get; set; } = default!;   // GOOD / BAD / UNCERTAIN
    public int AlarmPriority { get; set; }            // 0-3
    public DateTime ReceivedAt { get; set; }
    public bool IsConsensus { get; set; }             // false = sirovi podatak, true = konsenzus
}