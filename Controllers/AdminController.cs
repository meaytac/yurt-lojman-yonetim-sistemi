using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
public class AdminController(
    AppDbContext db,
    IAdminService adminService,
    IAccommodationService accommodationService) : ControllerBase
{
    private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

    // Admin -> null (tum tesisler); Yetkili -> yalnizca atandigi tesisler
    private async Task<FacilityScope?> GetFacilityScopeAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole(AppRoles.Admin)) return null;

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var assignments = await db.UserFacilityAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync(cancellationToken);

        return new FacilityScope(
            assignments.Where(x => x.DormitoryId != null).Select(x => x.DormitoryId!.Value).ToList(),
            assignments.Where(x => x.HousingUnitId != null).Select(x => x.HousingUnitId!.Value).ToList());
    }

    [HttpGet("dashboard-stats")]
    public async Task<AdminDashboardStatsDto> DashboardStats(CancellationToken cancellationToken)
        => await adminService.GetDashboardStatsAsync(await GetFacilityScopeAsync(cancellationToken), cancellationToken);

    [HttpGet("facilities")]
    public async Task<IReadOnlyList<AdminFacilityListItemDto>> Facilities(CancellationToken cancellationToken)
        => await adminService.GetFacilitiesAsync(await GetFacilityScopeAsync(cancellationToken), cancellationToken);

    [HttpPatch("dormitories/{id:int}/active")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> ToggleDormitory(int id, AdminActiveToggleRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Dormitories.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("housing-units/{id:int}/active")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> ToggleHousingUnit(int id, AdminActiveToggleRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.HousingUnits.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("rooms-detail")]
    public async Task<IReadOnlyList<AdminRoomListItemDto>> RoomsDetail(CancellationToken cancellationToken)
        => await adminService.GetRoomsAsync(await GetFacilityScopeAsync(cancellationToken), cancellationToken);

    [HttpGet("rooms/{id:int}/occupants")]
    public async Task<ActionResult<AdminRoomOccupantsResponse>> RoomOccupants(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await adminService.GetRoomOccupantsAsync(id, await GetFacilityScopeAsync(cancellationToken), cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("users")]
    public async Task<AdminPagedResponse<AdminUserListItemDto>> Users([FromQuery] AdminUserQuery query, CancellationToken cancellationToken)
        => await adminService.GetUsersAsync(query, await GetFacilityScopeAsync(cancellationToken), cancellationToken);

    [HttpPatch("users/{id:guid}/role")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetUserRole(Guid id, AdminUserRoleUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await adminService.SetUserRoleAsync(id, request.Role, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("users/{id:guid}/status")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetUserStatus(Guid id, AdminUserStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await adminService.SetUserStatusAsync(id, request.IsActive, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("placements")]
    public async Task<IReadOnlyList<AdminPlacementListItemDto>> Placements([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
        => await adminService.GetPlacementsAsync(activeOnly, await GetFacilityScopeAsync(cancellationToken), cancellationToken);

    [HttpGet("applications")]
    public async Task<List<AdminApplicationListItemDto>> Applications([FromQuery] ApplicationStatus? status, CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        var query = db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => ApplicantRoles.Contains(x.User.Role))
            .AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (scope != null)
        {
            var types = new List<AccommodationType>();
            if (scope.DormitoryIds.Count > 0) types.Add(AccommodationType.Yurt);
            if (scope.HousingUnitIds.Count > 0) types.Add(AccommodationType.Lojman);
            query = query.Where(x => types.Contains(x.AccommodationType));
        }

        return await query.OrderByDescending(x => x.CreatedAt)
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

    [HttpPost("applications/{id:int}/assign")]
    public async Task<IActionResult> AssignApplication(int id, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application is null) return NotFound("Basvuru bulunamadi.");
        if (!ApplicantRoles.Contains(application.User.Role)) return BadRequest("Yonetici ve yetkili profilleri basvuru akışına dahil edilemez.");

        var scope = await GetFacilityScopeAsync(cancellationToken);
        IReadOnlyList<int>? dormIds = scope?.DormitoryIds;
        IReadOnlyList<int>? unitIds = scope?.HousingUnitIds;

        if (request.RoomId.HasValue)
        {
            var roomInScope = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == request.RoomId.Value)
                .AnyAsync(x => (x.BlockFloor.Building.DormitoryId != null && (dormIds == null || dormIds.Contains(x.BlockFloor.Building.DormitoryId.Value))) ||
                               (x.BlockFloor.Building.HousingUnitId != null && (unitIds == null || unitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value))), cancellationToken);
            if (!roomInScope) return BadRequest("Secilen oda yetkili oldugunuz tesiste bulunmuyor.");
        }

        try
        {
            application.Status = ApplicationStatus.Approved;
            application.UpdatedAt = DateTime.UtcNow;
            await accommodationService.PlaceUserAsync(application.UserId, application.AccommodationType, request.RoomId, cancellationToken, dormIds, unitIds);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("requests")]
    public async Task<List<AdminRequestListItemDto>> Requests([FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        var query = db.Requests.AsNoTracking()
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

        if (openOnly)
        {
            query = query.Where(x => x.Status == RequestStatus.Open || x.Status == RequestStatus.InProgress);
        }

        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminRequestListItemDto(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.RoomId,
                x.Room.RoomNumber,
                x.Category,
                x.Description,
                x.PhotoUrl,
                x.Status,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    [HttpPatch("requests/{id:int}/status")]
    public async Task<IActionResult> SetRequestStatus(int id, MaintenanceStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        var entity = await db.Requests.Include(x => x.Room).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NotFound("Talep bulunamadi.");

        var inScope = await db.Requests.AsNoTracking()
            .Where(x => x.Id == id)
            .AnyAsync(x => (x.Room.BlockFloor.Building.DormitoryId != null && (scope == null || scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))) ||
                           (x.Room.BlockFloor.Building.HousingUnitId != null && (scope == null || scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value))), cancellationToken);
        if (!inScope) return NotFound("Talep bulunamadi.");

        entity.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("fault-reports")]
    public Task<List<AdminFaultReportListItemDto>> FaultReports(CancellationToken cancellationToken)
        => db.FaultReports.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminFaultReportListItemDto(x.Id, x.Category, x.Location, x.Description, x.CreatedAt))
            .ToListAsync(cancellationToken);

    [HttpGet("payments")]
    public Task<List<AdminPaymentListItemDto>> Payments([FromQuery] bool unpaidOnly = false, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = db.Payments.AsNoTracking().Include(x => x.User).AsQueryable();
        query = query.Where(x => ApplicantRoles.Contains(x.User.Role));
        if (unpaidOnly)
        {
            query = query.Where(x => x.Status == PaymentStatus.Unpaid || x.Status == PaymentStatus.Overdue);
        }

        return query.OrderByDescending(x => x.DueDate)
            .Select(x => new AdminPaymentListItemDto(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.User.TcNo,
                x.Amount,
                x.DueDate,
                x.PaidDate,
                x.Status == PaymentStatus.Unpaid && x.DueDate < now ? PaymentStatus.Overdue : x.Status,
                x.Description))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("payments/{id:int}/paid")]
    public async Task<IActionResult> MarkPaymentPaid(int id, PaymentMarkPaidRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Payments.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound("Odeme kaydi bulunamadi.");
        entity.Status = PaymentStatus.Paid;
        entity.PaidDate = request.PaidDate ?? DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("placements/assign")]
    public async Task<ActionResult<AdminPlacementListItemDto>> Assign(AdminPlacementAssignRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var scope = await GetFacilityScopeAsync(cancellationToken);
        IReadOnlyList<int>? dormIds = scope?.DormitoryIds;
        IReadOnlyList<int>? unitIds = scope?.HousingUnitIds;

        var userRole = await db.Users.AsNoTracking()
            .Where(x => x.Id == request.UserId)
            .Select(x => x.Role)
            .FirstOrDefaultAsync(cancellationToken);
        if (userRole is null) return NotFound("Kullanici bulunamadi.");
        if (!ApplicantRoles.Contains(userRole)) return BadRequest("Yonetici ve yetkili profilleri yerlestirme akışına dahil edilemez.");

        if (request.RoomId != 0)
        {
            var roomInScope = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == request.RoomId)
                .AnyAsync(x => (x.BlockFloor.Building.DormitoryId != null && (dormIds == null || dormIds.Contains(x.BlockFloor.Building.DormitoryId.Value))) ||
                               (x.BlockFloor.Building.HousingUnitId != null && (unitIds == null || unitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value))), cancellationToken);
            if (!roomInScope) return BadRequest("Secilen oda yetkili oldugunuz tesiste bulunmuyor.");
        }

        try
        {
            var placement = await accommodationService.PlaceUserAsync(request.UserId, request.AccommodationType, request.RoomId, cancellationToken, dormIds, unitIds);
            var result = await db.Placements.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Room)
                .Where(x => x.Id == placement.Id)
                .Select(x => new AdminPlacementListItemDto(x.Id, x.UserId, x.User.FullName, x.RoomId, x.Room.RoomNumber, x.CheckInDate, x.CheckOutDate, x.IsActive))
                .FirstAsync(cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPost("placements/{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        var inScope = await db.Placements.AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .AnyAsync(x => (x.Room.BlockFloor.Building.DormitoryId != null && (scope == null || scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value))) ||
                           (x.Room.BlockFloor.Building.HousingUnitId != null && (scope == null || scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value))), cancellationToken);
        if (!inScope) return NotFound("Yerlestirme bulunamadi.");

        try
        {
            await accommodationService.CheckoutAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("user-facility-assignments")]
    [Authorize(Roles = AppRoles.Admin)]
    public Task<IReadOnlyList<UserFacilityAssignmentDto>> UserFacilityAssignments(CancellationToken cancellationToken)
        => adminService.GetUserFacilityAssignmentsAsync(cancellationToken);

    [HttpPost("user-facility-assignments")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<UserFacilityAssignmentDto>> CreateUserFacilityAssignment(UserFacilityAssignmentCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var assignedById = Guid.Parse(userId!);
            var result = await adminService.CreateUserFacilityAssignmentAsync(request, assignedById, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("user-facility-assignments/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<UserFacilityAssignmentDto>> UpdateUserFacilityAssignment(int id, UserFacilityAssignmentUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await adminService.UpdateUserFacilityAssignmentAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("user-facility-assignments/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteUserFacilityAssignment(int id, CancellationToken cancellationToken)
    {
        try
        {
            await adminService.DeleteUserFacilityAssignmentAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("users-by-role/{role}")]
    public Task<IReadOnlyList<AdminUserListItemDto>> UsersByRole(string role, CancellationToken cancellationToken)
        => adminService.GetUsersByRoleAsync(role, cancellationToken);

    [HttpPost("users")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<AdminUserListItemDto>> CreateUser(AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await adminService.CreateUserAsync(request, cancellationToken);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
