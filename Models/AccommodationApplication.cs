using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class AccommodationApplication
{
    public int Id { get; set; }

    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public AccommodationType AccommodationType { get; set; }

    public ApplicationSource Source { get; set; } = ApplicationSource.RegisteredUser;

    [MaxLength(150)]
    public string? ApplicantFullName { get; set; }

    [MaxLength(256)]
    public string? ApplicantEmail { get; set; }

    [MaxLength(11)]
    public string? ApplicantTcNo { get; set; }

    [MaxLength(30)]
    public string? ApplicantPhoneNumber { get; set; }

    [MaxLength(30)]
    public string? ApplicantStudentStaffNo { get; set; }

    [MaxLength(30)]
    public string? ApplicantRole { get; set; }

    [MaxLength(1000)]
    public string? ApplicantNote { get; set; }

    public int? RequestedDormitoryId { get; set; }
    public Dormitory? RequestedDormitory { get; set; }

    public int? RequestedHousingUnitId { get; set; }
    public HousingUnit? RequestedHousingUnit { get; set; }

    [MaxLength(500)]
    public string? DocumentUrl { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    [MaxLength(32)]
    public string ReferenceCode { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? IdempotencyKeyHash { get; set; }

    [MaxLength(128)]
    public string? IdempotencyPayloadHash { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? DecisionAt { get; set; }
    public Guid? DecidedById { get; set; }
    public AppUser? DecidedBy { get; set; }

    [MaxLength(1000)]
    public string? DecisionReason { get; set; }

    public int? ApprovedRoomId { get; set; }
    public Room? ApprovedRoom { get; set; }

    public DateTime? ActivationSentAt { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? Version { get; set; }

    public ICollection<ApplicationAccessToken> AccessTokens { get; set; } = [];
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = [];
}
