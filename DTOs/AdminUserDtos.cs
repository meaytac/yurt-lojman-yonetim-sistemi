using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public class AdminUserQuery
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record AdminUserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string TcNo,
    string? StudentStaffNo,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

public record AdminUserRoleUpdateRequest([Required] string Role);

public record AdminUserStatusUpdateRequest(bool IsActive);

public record AdminPagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public class AdminCreateUserRequest
{
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required, MinLength(11), MaxLength(11)] public string TcNo { get; set; } = string.Empty;
    [MaxLength(30)] public string? StudentStaffNo { get; set; }
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    [Required] public string Role { get; set; } = string.Empty;
}

public record YetkiliStudentListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string TcNo,
    string? StudentStaffNo,
    string? PhoneNumber,
    DateTime CheckInDate,
    string RoomNumber,
    string BlockName,
    string FacilityName);

public class YetkiliRoomUpdateRequest
{
    [Required, MaxLength(10)] public string RoomNumber { get; set; } = string.Empty;
    [Range(1, 50)] public int Capacity { get; set; }
    [Range(0, 1_000_000)] public decimal Price { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Empty;
}

public record UserFacilityAssignmentDto(
    int Id,
    Guid UserId,
    string UserFullName,
    string UserRole,
    int? DormitoryId,
    string? DormitoryName,
    int? HousingUnitId,
    string? HousingUnitName,
    Guid AssignedById,
    string AssignedByName,
    DateTime AssignedAt,
    DateTime? UnassignedAt,
    bool IsActive);

public class UserFacilityAssignmentCreateRequest
{
    [Required] public Guid UserId { get; set; }
    public int? DormitoryId { get; set; }
    public int? HousingUnitId { get; set; }
}

public class UserFacilityAssignmentUpdateRequest
{
    public int? DormitoryId { get; set; }
    public int? HousingUnitId { get; set; }
    public bool? IsActive { get; set; }
}
