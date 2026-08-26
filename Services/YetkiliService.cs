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
    Task<AdminPagedResponse<AdminUserListItemDto>> GetStudentsAsync(Guid yetkiliId, AdminUserQuery query, CancellationToken cancellationToken);
    Task<AdminUserListItemDto> CreateStudentAsync(Guid yetkiliId, YetkiliCreateStudentRequest request, CancellationToken cancellationToken);
    Task<AdminUserListItemDto> UpdateStudentAsync(Guid yetkiliId, Guid studentId, YetkiliUpdateStudentRequest request, CancellationToken cancellationToken);
    Task DeleteStudentAsync(Guid yetkiliId, Guid studentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoomListItemDto>> GetAvailableRoomsAsync(Guid yetkiliId, AccommodationType type, CancellationToken cancellationToken);
    Task<IReadOnlyList<YetkiliStudentListItemDto>> GetStudentsWithRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoomListItemDto>> GetAssignedRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken);
    Task<AdminRoomListItemDto> UpdateRoomAsync(Guid yetkiliId, int roomId, YetkiliRoomUpdateRequest request, CancellationToken cancellationToken);
}

public class YetkiliService(AppDbContext db, UserManager<AppUser> userManager, IAccommodationService accommodationService) : IYetkiliService
{
    public async Task<IReadOnlyList<int>> GetAssignedDormitoryIdsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        return await db.UserFacilityAssignments.AsNoTracking()
            .Where(x => x.UserId == yetkiliId && x.IsActive && x.DormitoryId != null)
            .Select(x => x.DormitoryId!.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminFacilityListItemDto>> GetAssignedFacilitiesAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);

        return await db.Dormitories.AsNoTracking()
            .Where(x => dormitoryIds.Contains(x.Id))
            .Select(x => new AdminFacilityListItemDto(x.Id, x.Name, x.Type, x.CampusLocation, x.TotalCapacity, x.IsActive, x.Buildings.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminPagedResponse<AdminUserListItemDto>> GetStudentsAsync(Guid yetkiliId, AdminUserQuery query, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);

        var students = db.Users.AsNoTracking().Where(x => x.Role == AppRoles.Ogrenci).AsQueryable();

        if (dormitoryIds.Count > 0)
        {
            var placedStudentIds = await db.Placements.AsNoTracking()
                .Include(x => x.Room)
                .ThenInclude(x => x.BlockFloor)
                .ThenInclude(x => x.Building)
                .Where(x => x.IsActive && x.Room.BlockFloor.Building.DormitoryId.HasValue && dormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))
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

    public async Task<IReadOnlyList<AdminRoomListItemDto>> GetAvailableRoomsAsync(Guid yetkiliId, AccommodationType type, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);

        return await db.Rooms.AsNoTracking()
            .Include(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .ThenInclude(x => x.Dormitory)
            .Where(x => x.Status != RoomStatus.Maintenance && x.CurrentOccupancy < x.Capacity &&
                       x.BlockFloor.Building.DormitoryId != null && dormitoryIds.Contains(x.BlockFloor.Building.DormitoryId.Value))
            .OrderBy(x => x.BlockFloor.Building.BlockName)
            .ThenBy(x => x.BlockFloor.FloorNumber)
            .ThenBy(x => x.RoomNumber)
            .Select(x => new AdminRoomListItemDto(
                x.Id,
                x.BlockFloorId,
                x.BlockFloor.Building.Dormitory!.Name,
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
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);
        if (dormitoryIds.Count == 0)
        {
            return [];
        }

        return await db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Where(x => x.IsActive
                && x.User.Role == AppRoles.Ogrenci
                && x.Room.BlockFloor.Building.DormitoryId != null
                && dormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))
            .OrderBy(x => x.Room.BlockFloor.Building.BlockName)
            .ThenBy(x => x.Room.RoomNumber)
            .Select(x => new YetkiliStudentListItemDto(
                x.User.Id,
                x.User.FullName,
                x.User.Email ?? string.Empty,
                x.User.TcNo,
                x.User.StudentStaffNo,
                x.User.PhoneNumber,
                x.CheckInDate,
                x.Room.RoomNumber,
                x.Room.BlockFloor.Building.BlockName,
                x.Room.BlockFloor.Building.Dormitory!.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminRoomListItemDto>> GetAssignedRoomsAsync(Guid yetkiliId, CancellationToken cancellationToken)
    {
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);
        if (dormitoryIds.Count == 0)
        {
            return [];
        }

        return await db.Rooms.AsNoTracking()
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Where(x => x.BlockFloor.Building.DormitoryId != null && dormitoryIds.Contains(x.BlockFloor.Building.DormitoryId.Value))
            .OrderBy(x => x.BlockFloor.Building.BlockName)
            .ThenBy(x => x.BlockFloor.FloorNumber)
            .ThenBy(x => x.RoomNumber)
            .Select(x => new AdminRoomListItemDto(
                x.Id,
                x.BlockFloorId,
                x.BlockFloor.Building.Dormitory!.Name,
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
        var dormitoryIds = await GetAssignedDormitoryIdsAsync(yetkiliId, cancellationToken);

        var room = await db.Rooms
            .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new KeyNotFoundException("Oda bulunamadi.");

        if (room.BlockFloor.Building.DormitoryId == null || !dormitoryIds.Contains(room.BlockFloor.Building.DormitoryId.Value))
        {
            throw new InvalidOperationException("Bu oda size atanmis yurtta bulunmuyor.");
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

        return new AdminRoomListItemDto(
            room.Id,
            room.BlockFloorId,
            room.BlockFloor.Building.Dormitory!.Name,
            room.BlockFloor.Building.BlockName,
            room.BlockFloor.FloorNumber,
            room.RoomNumber,
            room.Capacity,
            room.CurrentOccupancy,
            room.Status,
            room.Price);
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