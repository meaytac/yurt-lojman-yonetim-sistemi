using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record StaffMaintenanceRequestResponse(int Id, string RoomNumber, string Category, string Description, string Status, DateTime CreatedAt, int? RepairPeriodDays, DateTime? TargetRepairDate, bool IsManagerAssignment = false, string? Priority = null);
public record CleaningTaskResponse(int Id, string TaskType, string Location, string? Notes, bool IsCompleted, DateTime CreatedAt, DateTime? CompletedAt);
public record PeriodicMaintenanceResponse(int Id, string SystemName, string Location, int IntervalDays, DateTime NextMaintenanceDate, DateTime? LastMaintenanceDate, string? Notes);
public record StaffAssignmentResponse(int Id, string AssignedRole, string Title, string Location, string? Details, string Priority, bool IsMaintenanceRequest, bool IsCompleted, DateTime? DueDate, DateTime CreatedAt, DateTime? CompletedAt);

public class PeriodicMaintenanceCreateRequest
{
    [Required, MaxLength(100)] public string SystemName { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [Range(1, 365)] public int IntervalDays { get; set; }
    [Required] public DateTime NextMaintenanceDate { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class StaffAssignmentCreateRequest
{
    [Required] public string AssignedRole { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }
    [Required] public string Priority { get; set; } = "Normal";
    public bool IsMaintenanceRequest { get; set; }
    public DateTime? DueDate { get; set; }
}

public class FaultReportCreateRequest
{
    [Required, MaxLength(100)] public string Category { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Location { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
}
