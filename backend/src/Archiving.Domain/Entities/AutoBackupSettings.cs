using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>Singleton row configuring the scheduled database backup job.</summary>
public class AutoBackupSettings : BaseEntity
{
    public bool Enabled { get; set; }
    public string? TargetPath { get; set; }
    public int IntervalHours { get; set; } = 24;
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }   // "Success" | "Failed"
    public string? LastRunError { get; set; }
}
