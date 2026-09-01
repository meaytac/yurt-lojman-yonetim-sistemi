using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record AdminApplicationListItemDto(
    int Id,
    Guid? UserId,
    string FullName,
    string TcNo,
    string? StudentStaffNo,
    AccommodationType AccommodationType,
    string? DocumentUrl,
    ApplicationStatus Status,
    DateTime CreatedAt);

public record AdminRequestListItemDto(
    int Id,
    Guid UserId,
    string FullName,
    int RoomId,
    string RoomNumber,
    string Category,
    string Description,
    string? PhotoUrl,
    RequestStatus Status,
    DateTime CreatedAt);

public record AdminPaymentListItemDto(
    int Id,
    Guid UserId,
    string FullName,
    string TcNo,
    decimal Amount,
    DateTime DueDate,
    DateTime? PaidDate,
    PaymentStatus Status,
    string Description);

public record AdminFaultReportListItemDto(
    int Id,
    string Category,
    string Location,
    string Description,
    DateTime CreatedAt,
    int? DormitoryId = null,
    string? DormitoryName = null,
    int? HousingUnitId = null,
    string? HousingUnitName = null);
