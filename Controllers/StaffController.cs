using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Roles = $"{AppRoles.TeknikPersonel},{AppRoles.TemizlikPersoneli}")]
public class StaffController(AppDbContext db) : ControllerBase
{
    [HttpGet("maintenance-requests")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public Task<List<StaffMaintenanceRequestResponse>> GetMaintenanceRequests() => db.Requests.AsNoTracking()
        .Include(x => x.Room)
        .OrderBy(x => x.Status == RequestStatus.Resolved).ThenByDescending(x => x.CreatedAt)
        .Select(x => new StaffMaintenanceRequestResponse(x.Id, x.Room.RoomNumber, x.Category, x.Description, x.Status.ToString(), x.CreatedAt, x.RepairPeriodDays, x.TargetRepairDate))
        .ToListAsync();

    [HttpPatch("maintenance-requests/{id:int}/schedule")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<IActionResult> ScheduleRepair(int id, RepairScheduleUpdateRequest request)
    {
        var item = await db.Requests.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Status == RequestStatus.Resolved) return BadRequest("Çözülmüş arıza için süre atanamaz.");
        item.RepairPeriodDays = request.RepairPeriodDays;
        item.TargetRepairDate = DateTime.UtcNow.Date.AddDays(request.RepairPeriodDays);
        item.Status = RequestStatus.InProgress;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("maintenance-requests/{id:int}/resolve")]
    [Authorize(Roles = AppRoles.TeknikPersonel)]
    public async Task<IActionResult> ResolveRepair(int id)
    {
        var item = await db.Requests.FindAsync(id);
        if (item is null) return NotFound();
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

    [HttpPost("cleaning-tasks")]
    [Authorize(Roles = AppRoles.TemizlikPersoneli)]
    public async Task<ActionResult<CleaningTaskResponse>> CreateCleaningTask(CleaningTaskCreateRequest request)
    {
        var item = new CleaningTask { TaskType = request.TaskType, Location = request.Location, Notes = request.Notes };
        db.CleaningTasks.Add(item);
        await db.SaveChangesAsync();
        return Ok(new CleaningTaskResponse(item.Id, item.TaskType, item.Location, item.Notes, item.IsCompleted, item.CreatedAt, item.CompletedAt));
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
