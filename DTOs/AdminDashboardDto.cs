using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record AdminDashboardStatsDto(
    int DormitoryCount,
    int HousingUnitCount,
    int TotalRoomCount,
    int TotalCapacity,
    int CurrentOccupancy,
    int EmptyRoomCount,
    int OccupiedRoomCount,
    int MaintenanceRoomCount,
    decimal OccupancyRate,
    int PendingApplicationCount,
    int OpenRequestCount,
    decimal TotalUnpaidAndOverdueDebt,
    IReadOnlyList<AdminRecentApplicationDto> RecentApplications,
    IReadOnlyList<AdminRecentRequestDto> RecentRequests);

public record AdminRecentApplicationDto(
    int Id,
    Guid? UserId,
    string FullName,
    string TcNo,
    AccommodationType AccommodationType,
    ApplicationStatus Status,
    DateTime CreatedAt);

public record AdminRecentRequestDto(
    int Id,
    Guid UserId,
    string FullName,
    string RoomNumber,
    string Category,
    RequestStatus Status,
    DateTime CreatedAt);
