using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record AnnouncementCreateRequest(
    [Required, MaxLength(180)] string Title,
    [Required, MaxLength(4000)] string Content,
    AnnouncementTargetRole TargetRole = AnnouncementTargetRole.All,
    int? TargetFacilityId = null,
    string? TargetFacilityName = null,
    bool IsActive = true);

public record AnnouncementResponse(int Id, string Title, string Content, AnnouncementTargetRole TargetRole, int? TargetFacilityId, string? TargetFacilityName, DateTime CreatedAt, bool IsActive);
