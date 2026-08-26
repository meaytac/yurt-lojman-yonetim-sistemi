using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    [Required, MaxLength(150)] string FullName,
    [Required, MinLength(11), MaxLength(11)] string TcNo,
    string? StudentStaffNo,
    string? PhoneNumber,
    [Required] string Role);

public record LoginRequest([Required] string Email, [Required] string Password);

public record AuthResponse(Guid UserId, string FullName, string Email, string Role, string Token, bool MustChangePassword, string? PhoneNumber = null);

public record ChangePasswordRequest([Required, MinLength(6)] string CurrentPassword, [Required, MinLength(6)] string NewPassword);

public record UpdatePhoneRequest([Required, MaxLength(20)] string PhoneNumber);
