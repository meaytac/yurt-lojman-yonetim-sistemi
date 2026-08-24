using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record StaffMaintenanceRequestResponse(int Id, string RoomNumber, string Category, string Description, string Status, DateTime CreatedAt, int? RepairPeriodDays, DateTime? TargetRepairDate);
public record CleaningTaskResponse(int Id, string TaskType, string Location, string? Notes, bool IsCompleted, DateTime CreatedAt, DateTime? CompletedAt);
public record PeriodicMaintenanceResponse(int Id, string SystemName, string Location, int IntervalDays, DateTime NextMaintenanceDate, DateTime? LastMaintenanceDate, string? Notes);

public class CleaningTaskCreateRequest
{
    [Required, MaxLength(80)] public string TaskType { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [MaxLength(800)] public string? Notes { get; set; }
}

public class PeriodicMaintenanceCreateRequest
{
    [Required, MaxLength(100)] public string SystemName { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [Range(1, 365)] public int IntervalDays { get; set; }
    [Required] public DateTime NextMaintenanceDate { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}
