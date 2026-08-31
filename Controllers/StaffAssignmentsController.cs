using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/admin/staff-assignments")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
public class StaffAssignmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<StaffAssignmentResponse>> Get() => db.StaffAssignments.AsNoTracking()
        .Include(x => x.Dormitory)
        .Include(x => x.HousingUnit)
        .OrderBy(x => x.IsCompleted).ThenByDescending(x => x.CreatedAt)
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

    [HttpPost]
    public async Task<ActionResult<StaffAssignmentResponse>> Create(StaffAssignmentCreateRequest request)
    {
        if (request.AssignedRole is not (AppRoles.TeknikPersonel or AppRoles.TemizlikPersoneli)) return BadRequest("Görev yalnızca teknik veya temizlik personeline atanabilir.");
        if (request.DormitoryId.HasValue && request.HousingUnitId.HasValue) return BadRequest("Yalnızca yurt veya lojmandan biri seçilebilir.");
        var item = new StaffAssignment { AssignedRole = request.AssignedRole, DormitoryId = request.DormitoryId, HousingUnitId = request.HousingUnitId, Title = request.Title, Location = request.Location, Details = request.Details, Priority = request.Priority, IsMaintenanceRequest = request.IsMaintenanceRequest, DueDate = request.DueDate?.Date };
        db.StaffAssignments.Add(item);
        await db.SaveChangesAsync();
        return Ok(new StaffAssignmentResponse(item.Id, item.AssignedRole, item.Title, item.Location, item.Details, item.Priority, item.IsMaintenanceRequest, item.IsCompleted, item.DueDate, item.CreatedAt, item.CompletedAt, item.DormitoryId, null, item.HousingUnitId, null));
    }
}
