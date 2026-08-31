using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Roles = $"{AppRoles.TeknikPersonel},{AppRoles.TemizlikPersoneli},{AppRoles.Yetkili}")]
public class StaffController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId()
        => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    private async Task<FacilityScope> GetFacilityScopeAsync()
    {
        var userId = CurrentUserId();
        var assignments = await db.UserFacilityAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync();

        return new FacilityScope(
            assignments.Where(x => x.DormitoryId.HasValue).Select(x => x.DormitoryId!.Value).ToList(),
            assignments.Where(x => x.HousingUnitId.HasValue).Select(x => x.HousingUnitId!.Value).ToList());
    }

    private static bool StaffAssignmentInScope(StaffAssignment assignment, FacilityScope scope)
        => (assignment.DormitoryId.HasValue && scope.DormitoryIds.Contains(assignment.DormitoryId.Value))
            || (assignment.HousingUnitId.HasValue && scope.HousingUnitIds.Contains(assignment.HousingUnitId.Value));

    private IQueryable<StaffAssignment> ScopedStaffAssignments(FacilityScope scope)
        => db.StaffAssignments.AsNoTracking()
            .Include(x => x.Dormitory)
            .Include(x => x.HousingUnit)
            .Where(x => (x.DormitoryId.HasValue && scope.DormitoryIds.Contains(x.DormitoryId.Value)) ||
                        (x.HousingUnitId.HasValue && scope.HousingUnitIds.Contains(x.HousingUnitId.Value)));

    [HttpGet("duty-location")]
    [Authorize(Roles = $"{AppRoles.TeknikPersonel},{AppRoles.TemizlikPersoneli},{AppRoles.Yetkili}")]
    public async Task<ActionResult<UserFacilityAssignmentDto>> GetDutyLocation()
    {
        var userId = CurrentUserId();
        var assignment = await db.UserFacilityAssignments.AsNoTracking()
            .Include(x => x.Dormitory)
            .Include(x => x.HousingUnit)
            .Include(x => x.AssignedBy)
            .Where(x => x.UserId == userId && x.IsActive)
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
            .FirstOrDefaultAsync();

        if (assignment == null)
            return NotFound("Görev yeri atanmamış.");

        return Ok(assignment);
    }
    [HttpGet("maintenance-requests")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<List<StaffMaintenanceRequestResponse>> GetMaintenanceRequests()
    {
        var scope = await GetFacilityScopeAsync();
        var residentRequests = await db.Requests.AsNoTracking().Include(x => x.Room)
            .Where(x => (x.Room.BlockFloor.Building.DormitoryId.HasValue && scope.DormitoryIds.Contains(x.Room.BlockFloor.Building.DormitoryId.Value)) ||
                        (x.Room.BlockFloor.Building.HousingUnitId.HasValue && scope.HousingUnitIds.Contains(x.Room.BlockFloor.Building.HousingUnitId.Value)))
            .Select(x => new StaffMaintenanceRequestResponse(x.Id, x.Room.RoomNumber, x.Category, x.Description, x.Status.ToString(), x.CreatedAt, x.RepairPeriodDays, x.TargetRepairDate))
            .ToListAsync();
        var managerAssignments = await ScopedStaffAssignments(scope)
            .Where(x => x.AssignedRole == AppRoles.TeknikPersonel && x.IsMaintenanceRequest)
            .Select(x => new StaffMaintenanceRequestResponse(-x.Id, x.Location, x.Title, x.Details ?? string.Empty, x.IsCompleted ? "Resolved" : "Open", x.CreatedAt, null, x.DueDate, true, x.Priority))
            .ToListAsync();
        return residentRequests.Concat(managerAssignments).OrderBy(x => x.Status == "Resolved").ThenByDescending(x => x.CreatedAt).ToList();
    }

    [HttpPatch("maintenance-requests/{id:int}/schedule")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<IActionResult> ScheduleRepair(int id, RepairScheduleUpdateRequest request)
    {
        var scope = await GetFacilityScopeAsync();
        var item = await db.Requests.Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        var inScope = (item.Room.BlockFloor.Building.DormitoryId.HasValue && scope.DormitoryIds.Contains(item.Room.BlockFloor.Building.DormitoryId.Value)) ||
            (item.Room.BlockFloor.Building.HousingUnitId.HasValue && scope.HousingUnitIds.Contains(item.Room.BlockFloor.Building.HousingUnitId.Value));
        if (!inScope) return Forbid();
        if (item.Status == RequestStatus.Resolved) return BadRequest("Çözülmüş arıza için süre atanamaz.");
        item.RepairPeriodDays = request.RepairPeriodDays;
        item.TargetRepairDate = DateTime.UtcNow.Date.AddDays(request.RepairPeriodDays);
        item.Status = RequestStatus.InProgress;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("assignments/{id:int}/complete")]
    public async Task<IActionResult> CompleteAssignment(int id)
    {
        var scope = await GetFacilityScopeAsync();
        var item = await db.StaffAssignments.FindAsync(id);
        if (item is null) return NotFound();
        var currentRole = User.IsInRole(AppRoles.TeknikPersonel) ? AppRoles.TeknikPersonel : AppRoles.TemizlikPersoneli;
        if (item.AssignedRole != currentRole) return Forbid();
        if (!StaffAssignmentInScope(item, scope)) return Forbid();
        item.IsCompleted = true;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("assignments")]
    public async Task<List<StaffAssignmentResponse>> GetAssignments()
    {
        var scope = await GetFacilityScopeAsync();
        var currentRole = User.IsInRole(AppRoles.TeknikPersonel) ? AppRoles.TeknikPersonel : AppRoles.TemizlikPersoneli;
        return await ScopedStaffAssignments(scope)
            .Where(x => x.AssignedRole == currentRole)
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.DueDate)
            .Select(x => new StaffAssignmentResponse(
                x.Id,
                x.AssignedRole,
                x.Title,
                x.Location,
                x.Details,
                x.Priority,
                x.IsMaintenanceRequest,
                x.IsCompleted,
                x.DueDate,
                x.CreatedAt,
                x.CompletedAt,
                x.DormitoryId,
                x.Dormitory != null ? x.Dormitory.Name : null,
                x.HousingUnitId,
                x.HousingUnit != null ? x.HousingUnit.Name : null))
            .ToListAsync();
    }

    [HttpPatch("maintenance-requests/{id:int}/resolve")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<IActionResult> ResolveRepair(int id)
    {
        var scope = await GetFacilityScopeAsync();
        var item = await db.Requests.Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        var inScope = (item.Room.BlockFloor.Building.DormitoryId.HasValue && scope.DormitoryIds.Contains(item.Room.BlockFloor.Building.DormitoryId.Value)) ||
            (item.Room.BlockFloor.Building.HousingUnitId.HasValue && scope.HousingUnitIds.Contains(item.Room.BlockFloor.Building.HousingUnitId.Value));
        if (!inScope) return Forbid();
        item.Status = RequestStatus.Resolved;
        item.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("periodic-maintenance")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public Task<List<PeriodicMaintenanceResponse>> GetPeriodicMaintenance() => db.PeriodicMaintenances.AsNoTracking()
        .OrderBy(x => x.NextMaintenanceDate)
        .Select(x => new PeriodicMaintenanceResponse(x.Id, x.SystemName, x.Location, x.IntervalDays, x.NextMaintenanceDate, x.LastMaintenanceDate, x.Notes))
        .ToListAsync();

    [HttpPost("periodic-maintenance")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<ActionResult<PeriodicMaintenanceResponse>> CreatePeriodicMaintenance(PeriodicMaintenanceCreateRequest request)
    {
        var item = new PeriodicMaintenance { SystemName = request.SystemName, Location = request.Location, IntervalDays = request.IntervalDays, NextMaintenanceDate = request.NextMaintenanceDate.Date, Notes = request.Notes };
        db.PeriodicMaintenances.Add(item);
        await db.SaveChangesAsync();
        return Ok(new PeriodicMaintenanceResponse(item.Id, item.SystemName, item.Location, item.IntervalDays, item.NextMaintenanceDate, item.LastMaintenanceDate, item.Notes));
    }

    [HttpPatch("periodic-maintenance/{id:int}/complete")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<IActionResult> CompletePeriodicMaintenance(int id)
    {
        var item = await db.PeriodicMaintenances.FindAsync(id);
        if (item is null) return NotFound();
        item.LastMaintenanceDate = DateTime.UtcNow;
        item.NextMaintenanceDate = DateTime.UtcNow.Date.AddDays(item.IntervalDays);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("cleaning-tasks")]
    [Authorize(Roles = AppRoles.TemizlikPersoneli)]
    public Task<List<CleaningTaskResponse>> GetCleaningTasks() => db.CleaningTasks.AsNoTracking().OrderBy(x => x.IsCompleted).ThenByDescending(x => x.CreatedAt)
        .Select(x => new CleaningTaskResponse(x.Id, x.TaskType, x.Location, x.Notes, x.IsCompleted, x.CreatedAt, x.CompletedAt)).ToListAsync();

    [HttpPost("fault-reports")]
    [Authorize(Roles = AppRoles.TemizlikPersoneli)]
    public async Task<IActionResult> CreateFaultReport(FaultReportCreateRequest request)
    {
        if (request.DormitoryId.HasValue && request.HousingUnitId.HasValue) return BadRequest("Yalnızca yurt veya lojmandan biri seçilebilir.");
        var scope = await GetFacilityScopeAsync();
        var dormitoryId = request.DormitoryId ?? scope.DormitoryIds.FirstOrDefault();
        var housingUnitId = request.HousingUnitId ?? (dormitoryId == 0 ? scope.HousingUnitIds.FirstOrDefault() : 0);
        if (dormitoryId == 0 && housingUnitId == 0) return BadRequest("Görev yeri atanmamış.");
        if (request.DormitoryId.HasValue && !scope.DormitoryIds.Contains(request.DormitoryId.Value)) return Forbid();
        if (request.HousingUnitId.HasValue && !scope.HousingUnitIds.Contains(request.HousingUnitId.Value)) return Forbid();
        db.FaultReports.Add(new FaultReport { DormitoryId = dormitoryId == 0 ? null : dormitoryId, HousingUnitId = housingUnitId == 0 ? null : housingUnitId, Category = request.Category, Location = request.Location, Description = request.Description });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("cleaning-tasks/{id:int}/complete")]
    [Authorize(Roles = AppRoles.TemizlikPersoneli)]
    public async Task<IActionResult> CompleteCleaningTask(int id)
    {
        var item = await db.CleaningTasks.FindAsync(id);
        if (item is null) return NotFound();
        item.IsCompleted = true;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
