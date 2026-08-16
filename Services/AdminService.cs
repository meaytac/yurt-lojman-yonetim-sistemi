using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IAdminService
{
    Task<AdminDashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminFacilityListItemDto>> GetFacilitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoomListItemDto>> GetRoomsAsync(CancellationToken cancellationToken);
    Task<AdminRoomOccupantsResponse> GetRoomOccupantsAsync(int roomId, CancellationToken cancellationToken);
    Task<AdminPagedResponse<AdminUserListItemDto>> GetUsersAsync(AdminUserQuery query, CancellationToken cancellationToken);
    Task SetUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
    Task SetUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminPlacementListItemDto>> GetPlacementsAsync(bool activeOnly, CancellationToken cancellationToken);
}

public class AdminService(AppDbContext db, UserManager<AppUser> userManager) : IAdminService
{
    public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        var totalCapacity = await db.Rooms.SumAsync(x => (int?)x.Capacity, cancellationToken) ?? 0;
        var currentOccupancy = await db.Rooms.SumAsync(x => (int?)x.CurrentOccupancy, cancellationToken) ?? 0;
        var now = DateTime.UtcNow;

        var recentApplications = await db.Applications.AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminRecentApplicationDto(x.Id, x.UserId, x.User.FullName, x.User.TcNo, x.AccommodationType, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var recentRequests = await db.Requests.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminRecentRequestDto(x.Id, x.UserId, x.User.FullName, x.Room.RoomNumber, x.Category, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var unpaidDebt = await db.Payments
            .Where(x => x.Status == PaymentStatus.Unpaid || x.Status == PaymentStatus.Overdue || (x.Status == PaymentStatus.Unpaid && x.DueDate < now))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        return new AdminDashboardStatsDto(
            DormitoryCount: await db.Dormitories.CountAsync(cancellationToken),
            HousingUnitCount: await db.HousingUnits.CountAsync(cancellationToken),
            TotalRoomCount: await db.Rooms.CountAsync(cancellationToken),
            TotalCapacity: totalCapacity,
            CurrentOccupancy: currentOccupancy,
            EmptyRoomCount: await db.Rooms.CountAsync(x => x.Status == RoomStatus.Empty, cancellationToken),
            OccupiedRoomCount: await db.Rooms.CountAsync(x => x.Status == RoomStatus.PartiallyFull || x.Status == RoomStatus.Full, cancellationToken),
            MaintenanceRoomCount: await db.Rooms.CountAsync(x => x.Status == RoomStatus.Maintenance, cancellationToken),
            OccupancyRate: totalCapacity == 0 ? 0 : Math.Round((decimal)currentOccupancy / totalCapacity * 100, 2),
            PendingApplicationCount: await db.Applications.CountAsync(x => x.Status == ApplicationStatus.Pending, cancellationToken),
            OpenRequestCount: await db.Requests.CountAsync(x => x.Status == RequestStatus.Open || x.Status == RequestStatus.InProgress, cancellationToken),
            TotalUnpaidAndOverdueDebt: unpaidDebt,
            RecentApplications: recentApplications,
            RecentRequests: recentRequests);
    }

    public async Task<IReadOnlyList<AdminFacilityListItemDto>> GetFacilitiesAsync(CancellationToken cancellationToken)
    {
        var dormitories = await db.Dormitories.AsNoTracking()
            .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
            .ToListAsync(cancellationToken);

        var housingUnits = await db.HousingUnits.AsNoTracking()
            .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
            .ToListAsync(cancellationToken);

        return dormitories.Concat(housingUnits).OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
    }

    public async Task<IReadOnlyList<AdminRoomListItemDto>> GetRoomsAsync(CancellationToken cancellationToken)
    {
        return await db.Rooms.AsNoTracking()
            .Include(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .ThenInclude(x => x.Dormitory)
            .Include(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .ThenInclude(x => x.HousingUnit)
            .OrderBy(x => x.BlockFloor.Building.BlockName)
            .ThenBy(x => x.BlockFloor.FloorNumber)
            .ThenBy(x => x.RoomNumber)
            .Select(x => new AdminRoomListItemDto(
                x.Id,
                x.BlockFloorId,
                x.BlockFloor.Building.Dormitory != null ? x.BlockFloor.Building.Dormitory.Name : x.BlockFloor.Building.HousingUnit!.Name,
                x.BlockFloor.Building.BlockName,
                x.BlockFloor.FloorNumber,
                x.RoomNumber,
                x.Capacity,
                x.CurrentOccupancy,
                x.Status,
                x.Price))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminRoomOccupantsResponse> GetRoomOccupantsAsync(int roomId, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new KeyNotFoundException("Oda bulunamadi.");

        var occupants = await db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.RoomId == roomId && x.IsActive)
            .OrderBy(x => x.CheckInDate)
            .Select(x => new AdminRoomOccupantDto(x.Id, x.UserId, x.User.FullName, x.User.TcNo, x.User.StudentStaffNo, x.User.Role, x.CheckInDate))
            .ToListAsync(cancellationToken);

        return new AdminRoomOccupantsResponse(room.Id, room.RoomNumber, room.Capacity, room.CurrentOccupancy, room.Status, occupants);
    }

    public async Task<AdminPagedResponse<AdminUserListItemDto>> GetUsersAsync(AdminUserQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var users = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(x => x.FullName.Contains(term) || x.Email!.Contains(term) || x.TcNo.Contains(term) || (x.StudentStaffNo != null && x.StudentStaffNo.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            users = users.Where(x => x.Role == query.Role);
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users.OrderBy(x => x.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminUserListItemDto(
                x.Id,
                x.FullName,
                x.Email ?? string.Empty,
                x.PhoneNumber,
                x.TcNo,
                x.StudentStaffNo,
                x.Role,
                !x.LockoutEnd.HasValue || x.LockoutEnd <= DateTimeOffset.UtcNow,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminPagedResponse<AdminUserListItemDto>(items, page, pageSize, total);
    }

    public async Task SetUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!AppRoles.All.Contains(role))
        {
            throw new InvalidOperationException("Gecersiz rol.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException("Kullanici bulunamadi.");

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", removeResult.Errors.Select(x => x.Description)));
            }
        }

        var addResult = await userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", addResult.Errors.Select(x => x.Description)));
        }

        user.Role = role;
        await userManager.UpdateAsync(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException("Kullanici bulunamadi.");

        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.UtcNow.AddYears(100);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPlacementListItemDto>> GetPlacementsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderByDescending(x => x.CheckInDate)
            .Select(x => new AdminPlacementListItemDto(x.Id, x.UserId, x.User.FullName, x.RoomId, x.Room.RoomNumber, x.CheckInDate, x.CheckOutDate, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
