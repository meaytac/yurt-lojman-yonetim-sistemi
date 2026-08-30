using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IYetkiliService
{
    Task<IReadOnlyList<int>> GetAssignedDormitoryIdsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminFacilityListItemDto>> GetAssignedFacilitiesAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<AdminDashboardStatsDto> GetDashboardStatsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<AdminPagedResponse<AdminUserListItemDto>> GetStudentsAsync(Guid yetkiliId, AdminUserQuery query, CancellationToken cancellationToken);
    Task<AdminUserListItemDto> CreateStudentAsync(Guid yetkiliId, YetkiliCreateStudentRequest request, CancellationToken cancellationToken);
    Task<AdminUserListItemDto> UpdateStudentAsync(Guid yetkiliId, Guid studentId, YetkiliUpdateStudentRequest request, CancellationToken cancellationToken);
    Task DeleteStudentAsync(Guid yetkiliId, Guid studentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminApplicationListItemDto>> GetApplicationsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<Placement> AssignApplicationAsync(Guid yetkiliId, int applicationId, ApplicationDecisionRequest request, CancellationToken cancellationToken);
    Task RejectApplicationAsync(Guid yetkiliId, int applicationId, ApplicationDecisionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoomListItemDto>> GetAvailableRoomsAsync(Guid yetkiliId, AccommodationType type, CancellationToken cancellationToken);
    Task<IReadOnlyList<YetkiliStudentListItemDto>> GetStudentsWithRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoomListItemDto>> GetAssignedRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<AdminRoomListItemDto> UpdateRoomAsync(Guid yetkiliId, int roomId, YetkiliRoomUpdateRequest request, CancellationToken cancellationToken);
    Task<YetkiliStudentListItemDto> ChangeRoomAsync(Guid yetkiliId, int placementId, YetkiliPlacementMoveRequest request, CancellationToken cancellationToken);
    Task CheckoutAsync(Guid yetkiliId, int placementId, CancellationToken cancellationToken);
}

public class YetkiliService(AppDbContext db, UserManager<AppUser> userManager, IAccommodationService accommodationService) : IYetkiliService
{
    private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

    private async Task<FacilityScope> GetAssignedFacilityScopeAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var assignments = await db.UserFacilityAssignments.AsNoTracking()
            .Where(x => x.UserId == yetkiliId && x.IsActive)
            .ToListAsync(cancellationToken);

        return new FacilityScope(
            assignments.Where(x => x.DormitoryId.HasValue).Select(x => x.DormitoryId!.Value).Distinct().ToList(),
            assignments.Where(x => x.HousingUnitId.HasValue).Select(x => x.HousingUnitId!.Value).Distinct().ToList());
    }

    private IQueryable<Room> ScopedRooms(FacilityScope scope, bool tracking = false)
    {
        var query = tracking ? db.Rooms.AsQueryable() : db.Rooms.AsNoTracking();
        return query
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .Where(x =>
                (x.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.BlockFloor.Building.DormitoryId.Value)) ||
                (x.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value)));
    }

    private static bool RoomInScope(Room room, FacilityScope scope)
        => (room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(room.BlockFloor.Building.DormitoryId.Value))
            || (room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(room.BlockFloor.Building.HousingUnitId.Value));

    private static bool RoomMatchesType(Room room, AccommodationType type)
        => type == AccommodationType.Yurt
            ? room.BlockFloor.Building.DormitoryId != null
            : room.BlockFloor.Building.HousingUnitId != null;

    private static AdminRoomListItemDto ToRoomDto(Room room)
    {
        var building = room.BlockFloor.Building;
        var isDormitory = building.DormitoryId.HasValue;
        return new AdminRoomListItemDto(
            room.Id,
            room.BlockFloorId,
            isDormitory ? building.DormitoryId!.Value : building.HousingUnitId!.Value,
            isDormitory ? AccommodationType.Yurt : AccommodationType.Lojman,
            isDormitory ? building.Dormitory!.Name : building.HousingUnit!.Name,
            building.BlockName,
            room.BlockFloor.FloorNumber,
            room.RoomNumber,
            room.Capacity,
            room.CurrentOccupancy,
            room.Status,
            room.Price);
    }

    public async Task<IReadOnlyList<int>> GetAssignedDormitoryIdsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        return scope.DormitoryIds;
    }

    public async Task<IReadOnlyList<AdminFacilityListItemDto>> GetAssignedFacilitiesAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);

        var dormitories = await db.Dormitories.AsNoTracking()
            .Where(x => scope.DormitoryIds.Contains(x.Id))
            .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
            .ToListAsync(cancellationToken);

        var housingUnits = await db.HousingUnits.AsNoTracking()
            .Where(x => scope.HousingUnitIds.Contains(x.Id))
            .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
            .ToListAsync(cancellationToken);

        return dormitories.Concat(housingUnits).OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
    }

    public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        var rooms = ScopedRooms(scope);
        var totalCapacity = await rooms.SumAsync(x => (int?)x.Capacity, cancellationToken) ?? 0;
        var currentOccupancy = await rooms.SumAsync(x => (int?)x.CurrentOccupancy, cancellationToken) ?? 0;
        var assignedTypes = new List<AccommodationType>();
        if (scope.DormitoryIds.Count > 0) assignedTypes.Add(AccommodationType.Yurt);
        if (scope.HousingUnitIds.Count > 0) assignedTypes.Add(AccommodationType.Lojman);

        var recentApplications = await db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => ApplicantRoles.Contains(x.User.Role))
            .Where(x => assignedTypes.Contains(x.AccommodationType))
            .Where(x => x.Status == ApplicationStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new AdminRecentApplicationDto(x.Id, x.UserId, x.User.FullName, x.User.TcNo, x.AccommodationType, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminDashboardStatsDto(
            DormitoryCount: scope.DormitoryIds.Count,
            HousingUnitCount: scope.HousingUnitIds.Count,
            TotalRoomCount: await rooms.CountAsync(cancellationToken),
            TotalCapacity: totalCapacity,
            CurrentOccupancy: currentOccupancy,
            EmptyRoomCount: await rooms.CountAsync(x => x.Status == RoomStatus.Empty, cancellationToken),
            OccupiedRoomCount: await rooms.CountAsync(x => x.Status == RoomStatus.PartiallyFull || x.Status == RoomStatus.Full, cancellationToken),
            MaintenanceRoomCount: await rooms.CountAsync(x => x.Status == RoomStatus.Maintenance, cancellationToken),
            OccupancyRate: totalCapacity == 0 ? 0 : Math.Round((decimal)currentOccupancy / totalCapacity * 100, 2),
            PendingApplicationCount: await db.Applications.AsNoTracking()
                .Include(x => x.User)
                .Where(x => ApplicantRoles.Contains(x.User.Role))
                .Where(x => assignedTypes.Contains(x.AccommodationType))
                .CountAsync(x => x.Status == ApplicationStatus.Pending, cancellationToken),
            OpenRequestCount: await db.Requests.AsNoTracking()
                .Include(x => x.User)
                .Where(x => ApplicantRoles.Contains(x.User.Role))
                .Where(x => (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value)) ||
                            (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value)))
                .CountAsync(x => x.Status == RequestStatus.Open || x.Status == RequestStatus.InProgress, cancellationToken),
            TotalUnpaidAndOverdueDebt: 0,
            RecentApplications: recentApplications,
            RecentRequests: []);
    }

    public async Task<AdminPagedResponse<AdminUserListItemDto>> GetStudentsAsync(Guid yetkiliId, AdminUserQuery query, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);

        var students = db.Users.AsNoTracking().Where(x => ApplicantRoles.Contains(x.Role)).AsQueryable();

        if (scope.DormitoryIds.Count > 0 || scope.HousingUnitIds.Count > 0)
        {
            var placedStudentIds = await db.Placements.AsNoTracking()
                .Include(x => x.Room)
                .ThenInclude(x => x.BlockFloor)
                .ThenInclude(x => x.Building)
                .Where(x => x.IsActive &&
                    ((x.Room.BlockFloor.Building.DormitoryId.HasValue && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value)) ||
                     (x.Room.BlockFloor.Building.HousingUnitId.HasValue && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value))))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);

            students = students.Where(x => placedStudentIds.Contains(x.Id));
        }
        else
        {
            return new AdminPagedResponse<AdminUserListItemDto>([], page, pageSize, 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            students = students.Where(x => x.FullName.Contains(term) || x.Email!.Contains(term) || x.TcNo.Contains(term) || (x.StudentStaffNo != null && x.StudentStaffNo.Contains(term)));
        }

        var total = await students.CountAsync(cancellationToken);
        var items = await students.OrderBy(x => x.FullName)
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

    public async Task<AdminUserListItemDto> CreateStudentAsync(Guid yetkiliId, YetkiliCreateStudentRequest request, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);
        if (dormitoryIds.Count == 0)
        {
            throw new InvalidOperationException("Hiç yurta atanmamışsınız. Öğrenci ekleyemezsiniz.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TcNo = request.TcNo,
            StudentStaffNo = request.StudentStaffNo,
            PhoneNumber = request.PhoneNumber,
            Role = AppRoles.Ogrenci,
            MustChangePassword = true
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Ogrenci);
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

    public async Task<AdminUserListItemDto> UpdateStudentAsync(Guid yetkiliId, Guid studentId, YetkiliUpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);
        if (dormitoryIds.Count == 0)
        {
            throw new InvalidOperationException("Hiç yurta atanmamışsınız.");
        }

        var student = await db.Users.FirstOrDefaultAsync(x => x.Id == studentId && x.Role == AppRoles.Ogrenci, cancellationToken)
            ?? throw new KeyNotFoundException("Öğrenci bulunamadı.");

        var placedInAssignedDorm = await db.Placements.AsNoTracking()
            .Include(x => x.Room)
            .ThenInclude(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .AnyAsync(x => x.UserId == studentId && x.IsActive && x.Room.BlockFloor.Building.DormitoryId.HasValue && dormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value), cancellationToken);

        if (!placedInAssignedDorm)
        {
            throw new InvalidOperationException("Bu öğrenci size atanan yurtlarda konaklamıyor.");
        }

        student.FullName = request.FullName;
        student.TcNo = request.TcNo;
        student.StudentStaffNo = request.StudentStaffNo;
        student.PhoneNumber = request.PhoneNumber;
        student.Email = request.Email;
        student.UserName = request.Email;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(student);
            var resetResult = await userManager.ResetPasswordAsync(student, token, request.Password);
            if (!resetResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", resetResult.Errors.Select(x => x.Description)));
            }
            student.MustChangePassword = true;
        }

        await userManager.UpdateAsync(student);
        await db.SaveChangesAsync(cancellationToken);

        return new AdminUserListItemDto(
            student.Id,
            student.FullName,
            student.Email!,
            student.PhoneNumber,
            student.TcNo,
            student.StudentStaffNo,
            student.Role,
            !student.LockoutEnd.HasValue || student.LockoutEnd <= DateTimeOffset.UtcNow,
            student.CreatedAt);
    }

    public async Task DeleteStudentAsync(Guid yetkiliId, Guid studentId, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);
        if (dormitoryIds.Count == 0)
        {
            throw new InvalidOperationException("Hiç yurta atanmamışsınız.");
        }

        var student = await db.Users.FirstOrDefaultAsync(x => x.Id == studentId && x.Role == AppRoles.Ogrenci, cancellationToken)
            ?? throw new KeyNotFoundException("Öğrenci bulunamadı.");

        var placedInAssignedDorm = await db.Placements.AsNoTracking()
            .Include(x => x.Room)
            .ThenInclude(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .AnyAsync(x => x.UserId == studentId && x.IsActive && x.Room.BlockFloor.Building.DormitoryId.HasValue && dormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value), cancellationToken);

        if (!placedInAssignedDorm)
        {
            throw new InvalidOperationException("Bu öğrenci size atanan yurtlarda konaklamıyor.");
        }

        var result = await userManager.DeleteAsync(student);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
    }

    public async Task<IReadOnlyList<AdminApplicationListItemDto>> GetApplicationsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        var assignedTypes = new List<AccommodationType>();
        if (scope.DormitoryIds.Count > 0) assignedTypes.Add(AccommodationType.Yurt);
        if (scope.HousingUnitIds.Count > 0) assignedTypes.Add(AccommodationType.Lojman);

        if (assignedTypes.Count == 0)
        {
            return [];
        }

        return await db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => ApplicantRoles.Contains(x.User.Role))
            .Where(x => x.Status == ApplicationStatus.Pending)
            .Where(x => assignedTypes.Contains(x.AccommodationType))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminApplicationListItemDto(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.User.TcNo,
                x.User.StudentStaffNo,
                x.AccommodationType,
                x.DocumentUrl,
                x.Status,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Placement> AssignApplicationAsync(Guid yetkiliId, int applicationId, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");
        if (!ApplicantRoles.Contains(application.User.Role))
        {
            throw new InvalidOperationException("Yönetici ve yetkili profilleri başvuru akışına dahil edilemez.");
        }

        if (application.Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Yalnızca beklemedeki başvurular onaylanabilir.");
        }

        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        var (roomId, dormitoryIds, housingUnitIds) = await ResolvePlacementTargetAsync(scope, application.AccommodationType, request, cancellationToken);

        application.Status = ApplicationStatus.Approved;
        application.UpdatedAt = DateTime.UtcNow;
        return await accommodationService.PlaceUserAsync(application.UserId, application.AccommodationType, roomId, cancellationToken, dormitoryIds, housingUnitIds);
    }

    public async Task RejectApplicationAsync(Guid yetkiliId, int applicationId, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");
        if (!ApplicantRoles.Contains(application.User.Role))
        {
            throw new InvalidOperationException("Yönetici ve yetkili profilleri başvuru akışına dahil edilemez.");
        }

        if (application.Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Yalnızca beklemedeki başvurular reddedilebilir.");
        }

        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        if (!ApplicationTypeInScope(scope, application.AccommodationType))
        {
            throw new InvalidOperationException("Bu başvuru size atanmış tesis kapsamına girmiyor.");
        }

        application.Status = ApplicationStatus.Rejected;
        application.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(int? RoomId, IReadOnlyList<int>? DormitoryIds, IReadOnlyList<int>? HousingUnitIds)> ResolvePlacementTargetAsync(
        FacilityScope scope,
        AccommodationType type,
        ApplicationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApplicationTypeInScope(scope, type))
        {
            throw new InvalidOperationException("Bu başvuru size atanmış tesis kapsamına girmiyor.");
        }

        if (request.AutoPlace)
        {
            if (type == AccommodationType.Yurt)
            {
                if (!request.DormitoryId.HasValue)
                {
                    throw new InvalidOperationException("Otomatik atama için yurt seçilmelidir.");
                }

                if (!scope.DormitoryIds.Contains(request.DormitoryId.Value))
                {
                    throw new InvalidOperationException("Seçilen yurt size atanmış tesisler arasında bulunmuyor.");
                }

                var exists = await db.Dormitories.AnyAsync(x => x.Id == request.DormitoryId.Value && x.IsActive, cancellationToken);
                if (!exists)
                {
                    throw new InvalidOperationException("Seçilen yurt bulunamadı veya aktif değil.");
                }

                return (null, [request.DormitoryId.Value], null);
            }

            if (!request.HousingUnitId.HasValue)
            {
                throw new InvalidOperationException("Otomatik atama için lojman seçilmelidir.");
            }

            if (!scope.HousingUnitIds.Contains(request.HousingUnitId.Value))
            {
                throw new InvalidOperationException("Seçilen lojman size atanmış tesisler arasında bulunmuyor.");
            }

            var unitExists = await db.HousingUnits.AnyAsync(x => x.Id == request.HousingUnitId.Value && x.IsActive, cancellationToken);
            if (!unitExists)
            {
                throw new InvalidOperationException("Seçilen lojman bulunamadı veya aktif değil.");
            }

            return (null, null, [request.HousingUnitId.Value]);
        }

        if (!request.RoomId.HasValue)
        {
            throw new InvalidOperationException("Manuel atama için oda seçilmelidir.");
        }

        var room = await ScopedRooms(scope)
            .FirstOrDefaultAsync(x => x.Id == request.RoomId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Seçilen oda size atanmış tesislerde bulunmuyor.");
        if (!RoomMatchesType(room, type))
        {
            throw new InvalidOperationException("Seçilen oda başvuru türüyle uyumlu değil.");
        }

        return type == AccommodationType.Yurt
            ? (request.RoomId.Value, scope.DormitoryIds, null)
            : (request.RoomId.Value, null, scope.HousingUnitIds);
    }

    private static bool ApplicationTypeInScope(FacilityScope scope, AccommodationType type)
        => type == AccommodationType.Yurt ? scope.DormitoryIds.Count > 0 : scope.HousingUnitIds.Count > 0;

    public async Task<IReadOnlyList<AdminRoomListItemDto>> GetAvailableRoomsAsync(Guid yetkiliId, AccommodationType type, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);

        return await ScopedRooms(scope)
            .Where(x => x.Status != RoomStatus.Maintenance && x.CurrentOccupancy < x.Capacity)
            .Where(x => type == AccommodationType.Yurt
                ? x.BlockFloor.Building.DormitoryId != null
                : x.BlockFloor.Building.HousingUnitId != null)
            .OrderBy(x => x.BlockFloor.Building.BlockName)
            .ThenBy(x => x.BlockFloor.FloorNumber)
            .ThenBy(x => x.RoomNumber)
            .Select(x => new AdminRoomListItemDto(
                x.Id,
                x.BlockFloorId,
                x.BlockFloor.Building.DormitoryId ?? x.BlockFloor.Building.HousingUnitId!.Value,
                x.BlockFloor.Building.Dormitory != null ? AccommodationType.Yurt : AccommodationType.Lojman,
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

    public async Task<IReadOnlyList<YetkiliStudentListItemDto>> GetStudentsWithRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        if (scope.DormitoryIds.Count == 0 && scope.HousingUnitIds.Count == 0)
        {
            return [];
        }

        return await db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .Where(x => x.IsActive
                && ApplicantRoles.Contains(x.User.Role)
                && ((x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value)) ||
                    (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value))))
            .OrderBy(x => x.Room.BlockFloor.Building.BlockName)
            .ThenBy(x => x.Room.RoomNumber)
            .Select(x => new YetkiliStudentListItemDto(
                x.Id,
                x.User.Id,
                x.User.FullName,
                x.User.Email ?? string.Empty,
                x.User.TcNo,
                x.User.StudentStaffNo,
                x.User.PhoneNumber,
                x.CheckInDate,
                x.RoomId,
                x.Room.RoomNumber,
                x.Room.BlockFloor.Building.BlockName,
                x.Room.BlockFloor.FloorNumber,
                x.Room.BlockFloor.Building.DormitoryId ?? x.Room.BlockFloor.Building.HousingUnitId!.Value,
                x.Room.BlockFloor.Building.Dormitory != null ? AccommodationType.Yurt : AccommodationType.Lojman,
                x.Room.BlockFloor.Building.Dormitory != null ? x.Room.BlockFloor.Building.Dormitory.Name : x.Room.BlockFloor.Building.HousingUnit!.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminRoomListItemDto>> GetAssignedRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        if (scope.DormitoryIds.Count == 0 && scope.HousingUnitIds.Count == 0)
        {
            return [];
        }

        return await ScopedRooms(scope)
            .OrderBy(x => x.BlockFloor.Building.BlockName)
            .ThenBy(x => x.BlockFloor.FloorNumber)
            .ThenBy(x => x.RoomNumber)
            .Select(x => new AdminRoomListItemDto(
                x.Id,
                x.BlockFloorId,
                x.BlockFloor.Building.DormitoryId ?? x.BlockFloor.Building.HousingUnitId!.Value,
                x.BlockFloor.Building.Dormitory != null ? AccommodationType.Yurt : AccommodationType.Lojman,
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

    public async Task<AdminRoomListItemDto> UpdateRoomAsync(Guid yetkiliId, int roomId, YetkiliRoomUpdateRequest request, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);

        var room = await db.Rooms
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new KeyNotFoundException("Oda bulunamadi.");

        if (!RoomInScope(room, scope))
        {
            throw new InvalidOperationException("Bu oda size atanmış tesislerde bulunmuyor.");
        }

        if (request.Capacity < room.CurrentOccupancy)
        {
            throw new InvalidOperationException("Kapasite mevcut doluluktan dusuk olamaz.");
        }

        var roomNumberExists = await db.Rooms.AnyAsync(
            x => x.Id != roomId && x.BlockFloorId == room.BlockFloorId && x.RoomNumber == request.RoomNumber, cancellationToken);
        if (roomNumberExists)
        {
            throw new InvalidOperationException("Ayni katta bu numarada baska bir oda var.");
        }

        room.RoomNumber = request.RoomNumber;
        room.Capacity = request.Capacity;
        room.Price = request.Price;
        room.Status = request.Status;
        accommodationService.RefreshRoomStatus(room);

        await db.SaveChangesAsync(cancellationToken);

        return ToRoomDto(room);
    }

    public async Task<YetkiliStudentListItemDto> ChangeRoomAsync(Guid yetkiliId, int placementId, YetkiliPlacementMoveRequest request, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var placement = await db.Placements
            .Include(x => x.User)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .FirstOrDefaultAsync(x => x.Id == placementId && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Aktif yerleşim bulunamadı.");

        if (!ApplicantRoles.Contains(placement.User.Role) || !RoomInScope(placement.Room, scope))
        {
            throw new InvalidOperationException("Bu yerleşim size atanmış tesislerde bulunmuyor.");
        }

        var newRoom = await db.Rooms
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .FirstOrDefaultAsync(x => x.Id == request.RoomId, cancellationToken)
            ?? throw new KeyNotFoundException("Yeni oda bulunamadı.");

        if (!RoomInScope(newRoom, scope))
        {
            throw new InvalidOperationException("Yeni oda size atanmış tesislerde bulunmuyor.");
        }

        var expectedType = placement.User.Role == AppRoles.Personel ? AccommodationType.Lojman : AccommodationType.Yurt;
        if (!RoomMatchesType(newRoom, expectedType))
        {
            throw new InvalidOperationException("Yeni oda kullanıcının başvuru türüyle uyumlu değil.");
        }

        if (newRoom.Id == placement.RoomId)
        {
            return await GetResidentByPlacementIdAsync(placementId, cancellationToken);
        }

        if (newRoom.Status == RoomStatus.Maintenance || newRoom.CurrentOccupancy >= newRoom.Capacity)
        {
            throw new InvalidOperationException("Yeni oda yerleştirmeye uygun değil.");
        }

        placement.Room.CurrentOccupancy = Math.Max(0, placement.Room.CurrentOccupancy - 1);
        accommodationService.RefreshRoomStatus(placement.Room);
        newRoom.CurrentOccupancy++;
        accommodationService.RefreshRoomStatus(newRoom);
        placement.RoomId = newRoom.Id;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetResidentByPlacementIdAsync(placementId, cancellationToken);
    }

    public async Task CheckoutAsync(Guid yetkiliId, int placementId, CancellationToken cancellationToken)
    {
        var scope = await GetAssignedFacilityScopeAsync(yetkiliId, cancellationToken);
        var inScope = await db.Placements.AsNoTracking()
            .Where(x => x.Id == placementId && x.IsActive)
            .AnyAsync(x =>
                (x.Room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value)) ||
                (x.Room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value)), cancellationToken);
        if (!inScope)
        {
            throw new InvalidOperationException("Bu yerleşim size atanmış tesislerde bulunmuyor.");
        }

        await accommodationService.CheckoutAsync(placementId, cancellationToken);
    }

    private async Task<YetkiliStudentListItemDto> GetResidentByPlacementIdAsync(int placementId, CancellationToken cancellationToken)
    {
        return await db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .Where(x => x.Id == placementId)
            .Select(x => new YetkiliStudentListItemDto(
                x.Id,
                x.User.Id,
                x.User.FullName,
                x.User.Email ?? string.Empty,
                x.User.TcNo,
                x.User.StudentStaffNo,
                x.User.PhoneNumber,
                x.CheckInDate,
                x.RoomId,
                x.Room.RoomNumber,
                x.Room.BlockFloor.Building.BlockName,
                x.Room.BlockFloor.FloorNumber,
                x.Room.BlockFloor.Building.DormitoryId ?? x.Room.BlockFloor.Building.HousingUnitId!.Value,
                x.Room.BlockFloor.Building.Dormitory != null ? AccommodationType.Yurt : AccommodationType.Lojman,
                x.Room.BlockFloor.Building.Dormitory != null ? x.Room.BlockFloor.Building.Dormitory.Name : x.Room.BlockFloor.Building.HousingUnit!.Name))
            .FirstAsync(cancellationToken);
    }
}

public class YetkiliCreateStudentRequest
{
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(11), MinLength(11)] public string TcNo { get; set; } = string.Empty;
    [MaxLength(30)] public string? StudentStaffNo { get; set; }
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
}

public class YetkiliUpdateStudentRequest
{
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(11), MinLength(11)] public string TcNo { get; set; } = string.Empty;
    [MaxLength(30)] public string? StudentStaffNo { get; set; }
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    [MinLength(6)] public string? Password { get; set; }
}
