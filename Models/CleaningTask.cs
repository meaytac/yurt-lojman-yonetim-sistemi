using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class CleaningTask
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string TaskType { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [MaxLength(800)] public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class PeriodicMaintenance
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string SystemName { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    public int IntervalDays { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class StaffAssignment
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string AssignedRole { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }
    [Required, MaxLength(20)] public string Priority { get; set; } = "Normal";
    public bool IsMaintenanceRequest { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class FaultReport
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Category { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
