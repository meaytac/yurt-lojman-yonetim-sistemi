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
    [HttpGet("dashboard-stats")]
    public Task<AdminDashboardStatsDto> DashboardStats(CancellationToken cancellationToken)
        => adminService.GetDashboardStatsAsync(cancellationToken);

    [HttpGet("facilities")]
    public Task<IReadOnlyList<AdminFacilityListItemDto>> Facilities(CancellationToken cancellationToken)
        => adminService.GetFacilitiesAsync(cancellationToken);

    [HttpPatch("dormitories/{id:int}/active")]
    public async Task<IActionResult> ToggleDormitory(int id, AdminActiveToggleRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Dormitories.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("housing-units/{id:int}/active")]
    public async Task<IActionResult> ToggleHousingUnit(int id, AdminActiveToggleRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.HousingUnits.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("rooms-detail")]
    public Task<IReadOnlyList<AdminRoomListItemDto>> RoomsDetail(CancellationToken cancellationToken)
        => adminService.GetRoomsAsync(cancellationToken);

    [HttpGet("rooms/{id:int}/occupants")]
    public async Task<ActionResult<AdminRoomOccupantsResponse>> RoomOccupants(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await adminService.GetRoomOccupantsAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("users")]
    public Task<AdminPagedResponse<AdminUserListItemDto>> Users([FromQuery] AdminUserQuery query, CancellationToken cancellationToken)
        => adminService.GetUsersAsync(query, cancellationToken);

    [HttpPatch("users/{id:guid}/role")]
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
    public Task<IReadOnlyList<AdminPlacementListItemDto>> Placements([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
        => adminService.GetPlacementsAsync(activeOnly, cancellationToken);

    [HttpGet("applications")]
    public Task<List<AdminApplicationListItemDto>> Applications([FromQuery] ApplicationStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Applications.AsNoTracking().Include(x => x.User).AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return query.OrderByDescending(x => x.CreatedAt)
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
        var application = await db.Applications.FindAsync([id], cancellationToken);
        if (application is null) return NotFound("Basvuru bulunamadi.");

        try
        {
            application.Status = ApplicationStatus.Approved;
            application.UpdatedAt = DateTime.UtcNow;
            await accommodationService.PlaceUserAsync(application.UserId, application.AccommodationType, request.RoomId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("requests")]
    public Task<List<AdminRequestListItemDto>> Requests([FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Requests.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room)
            .AsQueryable();

        if (openOnly)
        {
            query = query.Where(x => x.Status == RequestStatus.Open || x.Status == RequestStatus.InProgress);
        }

        return query.OrderByDescending(x => x.CreatedAt)
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
        var entity = await db.Requests.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound("Talep bulunamadi.");
        entity.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("payments")]
    public Task<List<AdminPaymentListItemDto>> Payments([FromQuery] bool unpaidOnly = false, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = db.Payments.AsNoTracking().Include(x => x.User).AsQueryable();
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

        try
        {
            var placement = await accommodationService.PlaceUserAsync(request.UserId, request.AccommodationType, request.RoomId, cancellationToken);
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
}
