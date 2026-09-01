using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public AccommodationApplication Application { get; set; } = null!;

    public ApplicationStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public Guid? ChangedById { get; set; }
    public AppUser? ChangedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
