using System.ComponentModel.DataAnnotations;

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
