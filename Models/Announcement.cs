using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class Announcement
{
    public int Id { get; set; }

    [Required, MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    public AnnouncementTargetRole TargetRole { get; set; } = AnnouncementTargetRole.All;

    public int? TargetFacilityId { get; set; }

    [MaxLength(180)]
    public string TargetFacilityName { get; set; } = "Tüm Tesisler";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
