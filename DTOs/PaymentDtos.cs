using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record PaymentCreateRequest(
    [Required] Guid UserId,
    [Range(0.01, 999999)] decimal Amount,
    DateTime DueDate,
    [Required, MaxLength(300)] string Description);

public record PaymentMarkPaidRequest(DateTime? PaidDate);

public record PaymentResponse(int Id, Guid UserId, decimal Amount, DateTime DueDate, DateTime? PaidDate, PaymentStatus Status, string Description);
