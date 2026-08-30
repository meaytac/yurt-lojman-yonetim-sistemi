using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

// null -> tum tesisler (admin); dolu -> yalnizca atanan tesisler (yetkili)
public sealed record FacilityScope(IReadOnlyList<int> DormitoryIds, IReadOnlyList<int> HousingUnitIds);

public interface IAdminService
{
    Task<AdminDashboardStatsDto> GetDashboardStatsAsync(FacilityScope? scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminFacilityListItemDto>> GetFacilitiesAsync(FacilityScope? scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoomListItemDto>> GetRoomsAsync(FacilityScope? scope, CancellationToken cancellationToken);
    Task<AdminRoomOccupantsResponse> GetRoomOccupantsAsync(int roomId, FacilityScope? scope, CancellationToken cancellationToken);
    Task<AdminPagedResponse<AdminUserListItemDto>> GetUsersAsync(AdminUserQuery query, FacilityScope? scope, CancellationToken cancellationToken);
    Task SetUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
    Task SetUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminPlacementListItemDto>> GetPlacementsAsync(bool activeOnly, FacilityScope? scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFacilityAssignmentDto>> GetUserFacilityAssignmentsAsync(CancellationToken cancellationToken);
    Task<UserFacilityAssignmentDto> CreateUserFacilityAssignmentAsync(UserFacilityAssignmentCreateRequest request, Guid assignedById, CancellationToken cancellationToken);
    Task<UserFacilityAssignmentDto> UpdateUserFacilityAssignmentAsync(int id, UserFacilityAssignmentUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteUserFacilityAssignmentAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminUserListItemDto>> GetUsersByRoleAsync(string role, CancellationToken cancellationToken);
    Task<AdminUserListItemDto> CreateUserAsync(AdminCreateUserRequest request, CancellationToken cancellationToken);
}

    public class AdminService(AppDbContext db, UserManager<AppUser> userManager) : IAdminService
    {
        private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

        private IQueryable<Room> ScopedRooms(FacilityScope? scope)
        {
            var query = db.Rooms.AsNoTracking()
                .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
                .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit);
            if (scope == null) return query;
            return query.Where(x =>
                (x.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.BlockFloor.Building.DormitoryId.Value)) ||
                (x.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value)));
        }

        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync(FacilityScope? scope, CancellationToken cancellationToken)
        {
            var rooms = ScopedRooms(scope);
            var totalCapacity = await rooms.SumAsync(x => (int?)x.Capacity, cancellationToken) ?? 0;
            var currentOccupancy = await rooms.SumAsync(x => (int?)x.CurrentOccupancy, cancellationToken) ?? 0;
            var now = DateTime.UtcNow;

            var recentApplications = await db.Applications.AsNoTracking()
                .Include(x => x.User)
                .Where(x => ApplicantRoles.Contains(x.User.Role))
                .Where(x => scope == null
                    || (scope.DormitoryIds.Count > 0 && x.AccommodationType == AccommodationType.Yurt)
                    || (scope.HousingUnitIds.Count > 0 && x.AccommodationType == AccommodationType.Lojman))
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new AdminRecentApplicationDto(x.Id, x.UserId, x.User.FullName, x.User.TcNo, x.AccommodationType, x.Status, x.CreatedAt))
                .ToListAsync(cancellationToken);

            var recentRequests = await db.Requests.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Room)
                .Where(x => ApplicantRoles.Contains(x.User.Role))
                .Where(x => scope == null
                    || (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))
                    || (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value)))
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new AdminRecentRequestDto(x.Id, x.UserId, x.User.FullName, x.Room.RoomNumber, x.Category, x.Status, x.CreatedAt))
                .ToListAsync(cancellationToken);

            var scopedUserIds = db.Placements.AsNoTracking()
                .Where(x => ApplicantRoles.Contains(x.User.Role))
                .Where(x => x.IsActive && (scope == null
                    || (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))
                    || (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value))))
                .Select(x => x.UserId);

            var unpaidDebt = await db.Payments
                .Where(x => scopedUserIds.Contains(x.UserId))
                .Where(x => x.Status == PaymentStatus.Unpaid || x.Status == PaymentStatus.Overdue || (x.Status == PaymentStatus.Unpaid && x.DueDate < now))
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

            return new AdminDashboardStatsDto(
                DormitoryCount: scope == null ? await db.Dormitories.CountAsync(cancellationToken) : scope.DormitoryIds.Count,
                HousingUnitCount: scope == null ? await db.HousingUnits.CountAsync(cancellationToken) : scope.HousingUnitIds.Count,
                TotalRoomCount: await rooms.CountAsync(cancellationToken),
                TotalCapacity: totalCapacity,
                CurrentOccupancy: currentOccupancy,
                EmptyRoomCount: await rooms.CountAsync(x => x.Status == RoomStatus.Empty, cancellationToken),
                OccupiedRoomCount: await rooms.CountAsync(x => x.Status == RoomStatus.PartiallyFull || x.Status == RoomStatus.Full, cancellationToken),
                MaintenanceRoomCount: await rooms.CountAsync(x => x.Status == RoomStatus.Maintenance, cancellationToken),
                OccupancyRate: totalCapacity == 0 ? 0 : Math.Round((decimal)currentOccupancy / totalCapacity * 100, 2),
                PendingApplicationCount: await db.Applications.AsNoTracking()
                    .Where(x => ApplicantRoles.Contains(x.User.Role))
                    .Where(x => scope == null
                        || (scope.DormitoryIds.Count > 0 && x.AccommodationType == AccommodationType.Yurt)
                        || (scope.HousingUnitIds.Count > 0 && x.AccommodationType == AccommodationType.Lojman))
                    .CountAsync(x => x.Status == ApplicationStatus.Pending, cancellationToken),
                OpenRequestCount: await db.Requests.AsNoTracking()
                    .Where(x => ApplicantRoles.Contains(x.User.Role))
                    .Where(x => scope == null
                        || (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))
                        || (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value)))
                    .CountAsync(x => x.Status == RequestStatus.Open || x.Status == RequestStatus.InProgress, cancellationToken),
                TotalUnpaidAndOverdueDebt: unpaidDebt,
                RecentApplications: recentApplications,
                RecentRequests: recentRequests);
        }

        public async Task<IReadOnlyList<AdminFacilityListItemDto>> GetFacilitiesAsync(FacilityScope? scope, CancellationToken cancellationToken)
        {
            var dormitories = await db.Dormitories.AsNoTracking()
                .Where(x => scope == null || scope.DormitoryIds.Contains(x.Id))
                .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
                .ToListAsync(cancellationToken);

            var housingUnits = await db.HousingUnits.AsNoTracking()
                .Where(x => scope == null || scope.HousingUnitIds.Contains(x.Id))
                .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
                .ToListAsync(cancellationToken);

            return dormitories.Concat(housingUnits).OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
        }

        public async Task<IReadOnlyList<AdminRoomListItemDto>> GetRoomsAsync(FacilityScope? scope, CancellationToken cancellationToken)
        {
            return await ScopedRooms(scope)
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

        public async Task<AdminRoomOccupantsResponse> GetRoomOccupantsAsync(int roomId, FacilityScope? scope, CancellationToken cancellationToken)
        {
            var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
                ?? throw new KeyNotFoundException("Oda bulunamadi.");

            var inScope = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == roomId)
                .AnyAsync(x => scope == null
                    || (x.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.BlockFloor.Building.DormitoryId.Value))
                    || (x.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value)), cancellationToken);
            if (!inScope)
            {
                throw new KeyNotFoundException("Oda bulunamadi.");
            }

            var occupants = await db.Placements.AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.RoomId == roomId && x.IsActive)
                .OrderBy(x => x.CheckInDate)
                .Select(x => new AdminRoomOccupantDto(x.Id, x.UserId, x.User.FullName, x.User.TcNo, x.User.StudentStaffNo, x.User.Role, x.CheckInDate))
                .ToListAsync(cancellationToken);

            return new AdminRoomOccupantsResponse(room.Id, room.RoomNumber, room.Capacity, room.CurrentOccupancy, room.Status, occupants);
        }

        public async Task<AdminPagedResponse<AdminUserListItemDto>> GetUsersAsync(AdminUserQuery query, FacilityScope? scope, CancellationToken cancellationToken)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 5, 100);
            var users = db.Users.AsNoTracking().AsQueryable();

            if (scope != null)
            {
                var scopedUserIds = db.Placements.AsNoTracking()
                    .Where(x => x.IsActive && (scope == null
                        || (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))
                        || (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value))))
                    .Select(x => x.UserId);
                users = users.Where(x => scopedUserIds.Contains(x.Id));
            }

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

    public async Task<IReadOnlyList<AdminPlacementListItemDto>> GetPlacementsAsync(bool activeOnly, FacilityScope? scope, CancellationToken cancellationToken)
        {
            var query = db.Placements.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Room)
                .Where(x => ApplicantRoles.Contains(x.User.Role))
                .AsQueryable();

            if (scope != null)
            {
                query = query.Where(x =>
                    (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value)) ||
                    (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value)));
            }

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderByDescending(x => x.CheckInDate)
            .Select(x => new AdminPlacementListItemDto(x.Id, x.UserId, x.User.FullName, x.RoomId, x.Room.RoomNumber, x.CheckInDate, x.CheckOutDate, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserFacilityAssignmentDto>> GetUserFacilityAssignmentsAsync(CancellationToken cancellationToken)
    {
        return await db.UserFacilityAssignments.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Dormitory)
            .Include(x => x.HousingUnit)
            .Include(x => x.AssignedBy)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => new UserFacilityAssignmentDto(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.User.Role,
                x.DormitoryId,
                x.Dormitory != null ? x.Dormitory.Name : null,
                x.HousingUnitId,
                x.HousingUnit != null ? x.HousingUnit.Name : null,
                x.AssignedById,
                x.AssignedBy.FullName,
                x.AssignedAt,
                x.UnassignedAt,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserFacilityAssignmentDto> CreateUserFacilityAssignmentAsync(UserFacilityAssignmentCreateRequest request, Guid assignedById, CancellationToken cancellationToken)
    {
        if (!request.DormitoryId.HasValue && !request.HousingUnitId.HasValue)
        {
            throw new InvalidOperationException("Yurt veya Lojman seçilmelidir.");
        }

        if (request.DormitoryId.HasValue && request.HousingUnitId.HasValue)
        {
            throw new InvalidOperationException("Yalnızca yurt veya lojmandan biri seçilebilir.");
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        if (user.Role != AppRoles.Yetkili && user.Role != AppRoles.Personel && user.Role != AppRoles.TeknikPersonel && user.Role != AppRoles.TemizlikPersoneli)
        {
            throw new InvalidOperationException("Sadece Yetkili veya Personel rolleri atanabilir.");
        }

        var existing = await db.UserFacilityAssignments
            .Where(x => x.UserId == request.UserId && x.IsActive &&
                       ((x.DormitoryId == request.DormitoryId && x.DormitoryId != null) ||
                        (x.HousingUnitId == request.HousingUnitId && x.HousingUnitId != null)))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException("Bu kullanıcı zaten bu tesise atanmış.");
        }

        var assignedBy = await userManager.FindByIdAsync(assignedById.ToString())
            ?? throw new KeyNotFoundException("Atayan kullanıcı bulunamadı.");

        var assignment = new UserFacilityAssignment
        {
            UserId = request.UserId,
            DormitoryId = request.DormitoryId,
            HousingUnitId = request.HousingUnitId,
            AssignedById = assignedById,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.UserFacilityAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        // Load related entities for DTO
        if (assignment.DormitoryId.HasValue)
        {
            var dorm = await db.Dormitories.FindAsync([assignment.DormitoryId.Value], cancellationToken);
            assignment.Dormitory = dorm;
        }
        if (assignment.HousingUnitId.HasValue)
        {
            var hu = await db.HousingUnits.FindAsync([assignment.HousingUnitId.Value], cancellationToken);
            assignment.HousingUnit = hu;
        }

        return new UserFacilityAssignmentDto(
            assignment.Id,
            assignment.UserId,
            user.FullName,
            user.Role,
            assignment.DormitoryId,
            assignment.Dormitory?.Name,
            assignment.HousingUnitId,
            assignment.HousingUnit?.Name,
            assignment.AssignedById,
            assignedBy.FullName,
            assignment.AssignedAt,
            assignment.UnassignedAt,
            assignment.IsActive);
    }

    public async Task<UserFacilityAssignmentDto> UpdateUserFacilityAssignmentAsync(int id, UserFacilityAssignmentUpdateRequest request, CancellationToken cancellationToken)
    {
        var assignment = await db.UserFacilityAssignments
            .Include(x => x.User)
            .Include(x => x.Dormitory)
            .Include(x => x.HousingUnit)
            .Include(x => x.AssignedBy)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        if (request.DormitoryId.HasValue && request.HousingUnitId.HasValue)
        {
            throw new InvalidOperationException("Yalnızca yurt veya lojmandan biri seçilebilir.");
        }

        if (request.DormitoryId.HasValue)
        {
            assignment.DormitoryId = request.DormitoryId;
            assignment.HousingUnitId = null;
        }
        else if (request.HousingUnitId.HasValue)
        {
            assignment.HousingUnitId = request.HousingUnitId;
            assignment.DormitoryId = null;
        }

        if (request.IsActive.HasValue)
        {
            assignment.IsActive = request.IsActive.Value;
            if (!request.IsActive.Value)
            {
                assignment.UnassignedAt = DateTime.UtcNow;
            }
            else
            {
                assignment.UnassignedAt = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new UserFacilityAssignmentDto(
            assignment.Id,
            assignment.UserId,
            assignment.User.FullName,
            assignment.User.Role,
            assignment.DormitoryId,
            assignment.Dormitory?.Name,
            assignment.HousingUnitId,
            assignment.HousingUnit?.Name,
            assignment.AssignedById,
            assignment.AssignedBy.FullName,
            assignment.AssignedAt,
            assignment.UnassignedAt,
            assignment.IsActive);
    }

    public async Task DeleteUserFacilityAssignmentAsync(int id, CancellationToken cancellationToken)
    {
        var assignment = await db.UserFacilityAssignments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException("Atama bulunamadı.");

        assignment.IsActive = false;
        assignment.UnassignedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUserListItemDto>> GetUsersByRoleAsync(string role, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(x => x.Role == role)
            .OrderBy(x => x.FullName)
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

        return users;
    }

    public async Task<AdminUserListItemDto> CreateUserAsync(AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!AppRoles.All.Contains(request.Role))
        {
            throw new InvalidOperationException("Gecersiz rol.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TcNo = request.TcNo,
            StudentStaffNo = request.StudentStaffNo,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(x => x.Description)));
        }

        return new AdminUserListItemDto(
            user.Id,
            user.FullName,
            user.Email!,
            user.PhoneNumber,
            user.TcNo,
            user.StudentStaffNo,
            user.Role,
            true,
            user.CreatedAt);
    }
}
