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
[Authorize(Roles = AppRoles.Admin)]
public class AdminController(
    AppDbContext db,
    IAdminService adminService,
    IAccommodationService accommodationService,
    IApplicationWorkflowService workflowService) : ControllerBase
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
            .Where(x => (x.User != null && ApplicantRoles.Contains(x.User.Role)) || x.Source == ApplicationSource.ExternalApplicant)
            .Where(x => x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.UnderReview)
            .AsQueryable();

        if (scope != null)
        {
            var types = new List<AccommodationType>();
            if (scope.DormitoryIds.Count > 0) types.Add(AccommodationType.Yurt);
            if (scope.HousingUnitIds.Count > 0) types.Add(AccommodationType.Lojman);
            query = query.Where(x => types.Contains(x.AccommodationType)
                && (x.Source == ApplicationSource.RegisteredUser
                    || (x.RequestedDormitoryId.HasValue && scope.DormitoryIds.Contains(x.RequestedDormitoryId.Value))
                    || (x.RequestedHousingUnitId.HasValue && scope.HousingUnitIds.Contains(x.RequestedHousingUnitId.Value))));
        }

        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminApplicationListItemDto(
                x.Id,
                x.UserId,
                x.User != null ? x.User.FullName : x.ApplicantFullName!,
                x.User != null ? x.User.TcNo : x.ApplicantTcNo!,
                x.User != null ? x.User.StudentStaffNo : x.ApplicantStudentStaffNo,
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
        if (application is null) return NotFound(new { success = false, message = "Başvuru bulunamadı." });
        if (application.User != null && !ApplicantRoles.Contains(application.User.Role)) return BadRequest(new { success = false, message = "Yönetici ve yetkili profilleri başvuru akışına dahil edilemez." });
        if (application.Status != ApplicationStatus.Pending && application.Status != ApplicationStatus.UnderReview) return BadRequest(new { success = false, message = "Yalnızca inceleme bekleyen başvurular onaylanabilir." });

        var scope = await GetFacilityScopeAsync(cancellationToken);
        IReadOnlyList<int>? dormIds = scope?.DormitoryIds;
        IReadOnlyList<int>? unitIds = scope?.HousingUnitIds;

        if (request.AutoPlace)
        {
            var scopedResult = await ResolveAssignmentScopeAsync(application.AccommodationType, request, dormIds, unitIds, cancellationToken);
            if (scopedResult.Result != null) return scopedResult.Result;
            dormIds = scopedResult.DormitoryIds;
            unitIds = scopedResult.HousingUnitIds;
        }
        else if (!request.RoomId.HasValue)
        {
            return BadRequest(new { success = false, message = "Manuel atama için oda seçilmelidir." });
        }

        if (!request.AutoPlace && request.RoomId.HasValue)
        {
            var roomInScope = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == request.RoomId.Value)
                .AnyAsync(x => (x.BlockFloor.Building.DormitoryId != null && (dormIds == null || dormIds.Contains(x.BlockFloor.Building.DormitoryId.Value))) ||
                               (x.BlockFloor.Building.HousingUnitId != null && (unitIds == null || unitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value))), cancellationToken);
            if (!roomInScope) return BadRequest(new { success = false, message = "Seçilen oda yetkili olduğunuz tesiste bulunmuyor." });
        }

        try
        {
            var actorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var placement = await workflowService.ApproveAsync(actorId, id, request with { RoomId = request.AutoPlace ? null : request.RoomId }, dormIds, unitIds, cancellationToken)
                ?? throw new InvalidOperationException("Yerleştirme oluşturulamadı.");
            var roomNumber = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == placement.RoomId)
                .Select(x => x.RoomNumber)
                .FirstAsync(cancellationToken);
            var message = application.Source == ApplicationSource.ExternalApplicant
                ? $"Başvuru onaylandı, {roomNumber} numaralı odaya yerleştirildi ve aktivasyon e-postası kuyruğa alındı."
                : $"Başvuru başarıyla onaylandı ve {roomNumber} numaralı odaya yerleştirildi.";
            return Ok(new { success = true, message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/reject")]
    public async Task<IActionResult> RejectApplication(int id, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application is null) return NotFound(new { success = false, message = "Başvuru bulunamadı." });
        if (application.User != null && !ApplicantRoles.Contains(application.User.Role)) return BadRequest(new { success = false, message = "Yönetici ve yetkili profilleri başvuru akışına dahil edilemez." });
        if (application.Status != ApplicationStatus.Pending && application.Status != ApplicationStatus.UnderReview) return BadRequest(new { success = false, message = "Yalnızca inceleme bekleyen başvurular reddedilebilir." });

        try
        {
            var actorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var scope = await GetFacilityScopeAsync(cancellationToken);
            await workflowService.RejectAsync(actorId, id, request, scope?.DormitoryIds, scope?.HousingUnitIds, cancellationToken);
            return Ok(new { success = true, message = "Başvuru reddedildi ve bekleyen başvurular listesinden kaldırıldı." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/under-review")]
    public async Task<IActionResult> MarkApplicationUnderReview(int id, CancellationToken cancellationToken)
    {
        try
        {
            var actorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var scope = await GetFacilityScopeAsync(cancellationToken);
            await workflowService.MarkUnderReviewAsync(actorId, id, scope?.DormitoryIds, scope?.HousingUnitIds, cancellationToken);
            return Ok(new { success = true, message = "Başvuru incelemeye alındı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/missing-information")]
    public async Task<IActionResult> RequestMissingInformation(int id, MissingInformationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var actorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var scope = await GetFacilityScopeAsync(cancellationToken);
            await workflowService.RequestMissingInformationAsync(actorId, id, request, scope?.DormitoryIds, scope?.HousingUnitIds, cancellationToken);
            return Ok(new { success = true, message = "Ek bilgi talebi başvuru sahibine iletildi." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/resend-activation")]
    public async Task<IActionResult> ResendActivation(int id, CancellationToken cancellationToken)
    {
        try
        {
            var actorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var scope = await GetFacilityScopeAsync(cancellationToken);
            await workflowService.ResendActivationAsync(actorId, id, scope?.DormitoryIds, scope?.HousingUnitIds, cancellationToken);
            return Ok(new { success = true, message = "Aktivasyon e-postası yeniden kuyruğa alındı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private async Task<(IReadOnlyList<int>? DormitoryIds, IReadOnlyList<int>? HousingUnitIds, IActionResult? Result)> ResolveAssignmentScopeAsync(
        AccommodationType type,
        ApplicationDecisionRequest request,
        IReadOnlyList<int>? scopeDormitoryIds,
        IReadOnlyList<int>? scopeHousingUnitIds,
        CancellationToken cancellationToken)
    {
        if (type == AccommodationType.Yurt)
        {
            if (!request.DormitoryId.HasValue) return (scopeDormitoryIds, scopeHousingUnitIds, BadRequest(new { success = false, message = "Otomatik atama için yurt seçilmelidir." }));
            if (scopeDormitoryIds != null && !scopeDormitoryIds.Contains(request.DormitoryId.Value)) return (scopeDormitoryIds, scopeHousingUnitIds, BadRequest(new { success = false, message = "Seçilen yurt yetkili olduğunuz tesisler arasında bulunmuyor." }));
            var exists = await db.Dormitories.AnyAsync(x => x.Id == request.DormitoryId.Value && x.IsActive, cancellationToken);
            if (!exists) return (scopeDormitoryIds, scopeHousingUnitIds, BadRequest(new { success = false, message = "Seçilen yurt bulunamadı veya aktif değil." }));
            return ([request.DormitoryId.Value], null, null);
        }

        if (!request.HousingUnitId.HasValue) return (scopeDormitoryIds, scopeHousingUnitIds, BadRequest(new { success = false, message = "Otomatik atama için lojman seçilmelidir." }));
        if (scopeHousingUnitIds != null && !scopeHousingUnitIds.Contains(request.HousingUnitId.Value)) return (scopeDormitoryIds, scopeHousingUnitIds, BadRequest(new { success = false, message = "Seçilen lojman yetkili olduğunuz tesisler arasında bulunmuyor." }));
        var unitExists = await db.HousingUnits.AnyAsync(x => x.Id == request.HousingUnitId.Value && x.IsActive, cancellationToken);
        if (!unitExists) return (scopeDormitoryIds, scopeHousingUnitIds, BadRequest(new { success = false, message = "Seçilen lojman bulunamadı veya aktif değil." }));
        return (null, [request.HousingUnitId.Value], null);
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
    public async Task<IActionResult> Assign(AdminPlacementAssignRequest request, CancellationToken cancellationToken)
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
        if (userRole is null) return NotFound(new { success = false, message = "Kullanıcı bulunamadı." });
        if (!ApplicantRoles.Contains(userRole)) return BadRequest(new { success = false, message = "Yönetici ve yetkili profilleri yerleştirme akışına dahil edilemez." });

        if (request.AutoPlace)
        {
            var scopedResult = await ResolveAssignmentScopeAsync(request.AccommodationType, new ApplicationDecisionRequest(
                true,
                null,
                null,
                true,
                request.DormitoryId,
                request.HousingUnitId), dormIds, unitIds, cancellationToken);
            if (scopedResult.Result != null) return scopedResult.Result;
            dormIds = scopedResult.DormitoryIds;
            unitIds = scopedResult.HousingUnitIds;
        }
        else if (!request.RoomId.HasValue)
        {
            return BadRequest(new { success = false, message = "Manuel atama için oda seçilmelidir." });
        }

        if (!request.AutoPlace && request.RoomId.HasValue)
        {
            var roomInScope = await db.Rooms.AsNoTracking()
                .Where(x => x.Id == request.RoomId.Value)
                .AnyAsync(x => (x.BlockFloor.Building.DormitoryId != null && (dormIds == null || dormIds.Contains(x.BlockFloor.Building.DormitoryId.Value))) ||
                               (x.BlockFloor.Building.HousingUnitId != null && (unitIds == null || unitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value))), cancellationToken);
            if (!roomInScope) return BadRequest(new { success = false, message = "Seçilen oda yetkili olduğunuz tesiste bulunmuyor." });
        }

        try
        {
            var placement = await accommodationService.PlaceUserAsync(request.UserId, request.AccommodationType, request.AutoPlace ? null : request.RoomId, cancellationToken, dormIds, unitIds);
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
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
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
