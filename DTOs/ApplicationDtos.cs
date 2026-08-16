using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public class ApplicationCreateRequest
{
    [Required]
    public AccommodationType AccommodationType { get; set; }

    [MaxLength(500)]
    public string? DocumentUrl { get; set; }

    public IFormFile? Document { get; set; }
}

public record ApplicationDecisionRequest(bool Approved, string? Reason, int? RoomId, bool AutoPlace);

public record ApplicationResponse(
    int Id,
    Guid UserId,
    string FullName,
    AccommodationType AccommodationType,
    string? DocumentUrl,
    ApplicationStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
