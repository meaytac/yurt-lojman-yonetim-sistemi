using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController(AppDbContext db, IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public Task<List<MaintenanceRequestResponse>> GetAll() => Query().ToListAsync();

    [HttpGet("mine")]
    public Task<List<MaintenanceRequestResponse>> Mine()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Query().Where(x => x.UserId == userId).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceRequestResponse>> Create([FromForm] MaintenanceRequestCreateDto request, CancellationToken cancellationToken)
    {
        var photoUrl = await fileStorage.SaveAsync(request.Photo, "requests", cancellationToken) ?? request.PhotoUrl;
        var entity = new MaintenanceRequest
        {
            UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            RoomId = request.RoomId,
            Category = request.Category,
            Description = request.Description,
            PhotoUrl = photoUrl
        };

        db.Requests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await Query().FirstAsync(x => x.Id == entity.Id, cancellationToken));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<IActionResult> UpdateStatus(int id, MaintenanceStatusUpdateRequest request)
    {
        var entity = await db.Requests.FindAsync(id);
        if (entity is null) return NotFound();
        entity.Status = request.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<MaintenanceRequestResponse> Query()
    {
        return db.Requests.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MaintenanceRequestResponse(x.Id, x.UserId, x.User.FullName, x.RoomId, x.Room.RoomNumber, x.Category, x.Description, x.PhotoUrl, x.Status, x.CreatedAt));
    }

}
