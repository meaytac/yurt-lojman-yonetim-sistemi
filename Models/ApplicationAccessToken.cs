using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class ApplicationAccessToken
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public AccommodationApplication Application { get; set; } = null!;

    public ApplicationTokenPurpose Purpose { get; set; }

    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    [MaxLength(64)]
    public string? RequestIpHash { get; set; }

    [MaxLength(256)]
    public string? UserAgent { get; set; }
}
