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

public record ApplicationDecisionRequest(
    bool Approved,
    string? Reason,
    int? RoomId,
    bool AutoPlace,
    int? DormitoryId,
    int? HousingUnitId);

public record ApplicationResponse(
    int Id,
    Guid? UserId,
    string FullName,
    AccommodationType AccommodationType,
    string? DocumentUrl,
    ApplicationStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record PublicFacilityResponse(
    int Id,
    AccommodationType Type,
    string Name,
    string CampusLocation,
    int TotalCapacity,
    int AvailableCapacity,
    string? PublicDescription,
    string? Amenities,
    string? ImageUrl,
    string? ApplicationConditions,
    bool IsApplicationOpen);

public class PublicApplicationCreateRequest
{
    [Required(ErrorMessage = "İşlem anahtarı eksik."), MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ad soyad alanı zorunludur."), MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta adresi zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin."), MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "T.C. Kimlik Numarası zorunludur."), RegularExpression(@"^\d{11}$", ErrorMessage = "T.C. Kimlik Numarası 11 rakam olmalıdır.")]
    public string TcNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon numarası zorunludur."), MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Öğrenci/Personel numarası zorunludur."), MaxLength(30)]
    public string StudentStaffNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başvuru türü zorunludur."), MaxLength(30)]
    public string ApplicantRole { get; set; } = AppRoles.Ogrenci;

    [Required(ErrorMessage = "Konaklama türü zorunludur.")]
    public AccommodationType AccommodationType { get; set; }

    public int? DormitoryId { get; set; }
    public int? HousingUnitId { get; set; }

    [MaxLength(1000)]
    public string? ApplicantNote { get; set; }

    public IFormFile? Document { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Başvuru bilgilerinin doğruluğunu onaylayın.")]
    public bool Consent { get; set; }
}

public record PublicApplicationCreatedResponse(
    string ReferenceCode,
    ApplicationStatus Status,
    string Message);

public class PublicTokenRequest
{
    [Required, MaxLength(32)]
    public string ReferenceCode { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}

public class PublicTrackRequest : PublicTokenRequest;

public class ActivateAccountRequest : PublicTokenRequest
{
    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class PublicApplicationUpdateRequest : PublicTokenRequest
{
    [MaxLength(1000)]
    public string? Note { get; set; }

    public IFormFile? Document { get; set; }
}

public record MissingInformationRequest([Required, MaxLength(1000)] string Reason);

public record PublicApplicationHistoryDto(
    ApplicationStatus Status,
    string? Note,
    DateTime CreatedAt);

public record PublicTrackResponse(
    string ReferenceCode,
    ApplicationStatus Status,
    AccommodationType AccommodationType,
    string ApplicantFullName,
    string MaskedEmail,
    string ApplicantRole,
    string? FacilityName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<PublicApplicationHistoryDto> History);

public record PublicMessageResponse(string Message);

public record ApplicationEligibilityResponse(
    bool CanApply,
    string ReasonCode,
    string Message);
