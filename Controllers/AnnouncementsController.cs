using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<AnnouncementResponse>> GetActive(AnnouncementTargetRole? targetRole)
    {
        return db.Announcements.AsNoTracking()
            .Where(x => x.IsActive && (!targetRole.HasValue || x.TargetRole == AnnouncementTargetRole.All || x.TargetRole == targetRole.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AnnouncementResponse(x.Id, x.Title, x.Content, x.TargetRole, x.CreatedAt, x.IsActive))
            .ToListAsync();
    }

    [HttpGet("admin")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public Task<List<AnnouncementResponse>> GetAllForAdmin()
    {
        return db.Announcements.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AnnouncementResponse(x.Id, x.Title, x.Content, x.TargetRole, x.CreatedAt, x.IsActive))
            .ToListAsync();
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<ActionResult<AnnouncementResponse>> Create(AnnouncementCreateRequest request)
    {
        var entity = new Announcement { Title = request.Title, Content = request.Content, TargetRole = request.TargetRole, IsActive = request.IsActive };
        db.Announcements.Add(entity);
        await db.SaveChangesAsync();
        return Ok(new AnnouncementResponse(entity.Id, entity.Title, entity.Content, entity.TargetRole, entity.CreatedAt, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<IActionResult> Update(int id, AnnouncementCreateRequest request)
    {
        var entity = await db.Announcements.FindAsync(id);
        if (entity is null) return NotFound();
        entity.Title = request.Title;
        entity.Content = request.Content;
        entity.TargetRole = request.TargetRole;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
