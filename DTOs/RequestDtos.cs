using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public class MaintenanceRequestCreateDto
{
    [Required]
    public int RoomId { get; set; }

    [Required, MaxLength(80)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    public IFormFile? Photo { get; set; }
}

public record MaintenanceStatusUpdateRequest(RequestStatus Status);

public record RepairScheduleUpdateRequest([Range(1, 365)] int RepairPeriodDays);

public record MaintenanceRequestResponse(
    int Id,
    Guid UserId,
    int RoomId,
    string Category,
    string Description,
    string? PhotoUrl,
    RequestStatus Status,
    DateTime CreatedAt);
