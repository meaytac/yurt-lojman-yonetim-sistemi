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
    public Task<List<StaffAssignmentResponse>> Get() => db.StaffAssignments.AsNoTracking().OrderBy(x => x.IsCompleted).ThenByDescending(x => x.CreatedAt)
        .Select(x => new StaffAssignmentResponse(x.Id, x.AssignedRole, x.Title, x.Location, x.Details, x.Priority, x.IsMaintenanceRequest, x.IsCompleted, x.DueDate, x.CreatedAt, x.CompletedAt)).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<StaffAssignmentResponse>> Create(StaffAssignmentCreateRequest request)
    {
        if (request.AssignedRole is not (AppRoles.TeknikPersonel or AppRoles.TemizlikPersoneli)) return BadRequest("Görev yalnızca teknik veya temizlik personeline atanabilir.");
        var item = new StaffAssignment { AssignedRole = request.AssignedRole, Title = request.Title, Location = request.Location, Details = request.Details, Priority = request.Priority, IsMaintenanceRequest = request.IsMaintenanceRequest, DueDate = request.DueDate?.Date };
        db.StaffAssignments.Add(item);
        await db.SaveChangesAsync();
        return Ok(new StaffAssignmentResponse(item.Id, item.AssignedRole, item.Title, item.Location, item.Details, item.Priority, item.IsMaintenanceRequest, item.IsCompleted, item.DueDate, item.CreatedAt, item.CompletedAt));
    }
}
