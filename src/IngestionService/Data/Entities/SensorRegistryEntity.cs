namespace IngestionService.Data.Entities;

/// <summary>
/// Evidencija svakog senzora koji je ikad bio dio sistema.
/// Koristi se za fault tolerance - pracenje aktivnosti senzora.
/// </summary>
public class SensorRegistryEntity
{
    public string SensorId { get; set; } = default!;
    public DateTime LastSeenAt { get; set; }          // zadnji put kad je primljena poruka
    public bool IsActive { get; set; }                // neaktivan ako nema poruke 10 sekundi
    public bool IsBlocked { get; set; }               // privremeno blokiran
    public DateTime? BlockedUntil { get; set; }       // do kada je blokiran
    public string Quality { get; set; } = "GOOD";     // GOOD / BAD / UNCERTAIN
    public long LastMessageSequence { get; set; }     // za replay zastitu
}