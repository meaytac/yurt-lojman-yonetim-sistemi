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
[Route("api/applications")]
[Authorize]
public class ApplicationsController(AppDbContext db, IFileStorageService fileStorage, IAccommodationService accommodationService) : ControllerBase
{
    private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public Task<List<ApplicationResponse>> GetAll()
    {
        return db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => ApplicantRoles.Contains(x.User.Role))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApplicationResponse(x.Id, x.UserId, x.User.FullName, x.AccommodationType, x.DocumentUrl, x.Status, x.CreatedAt, x.UpdatedAt))
            .ToListAsync();
    }

    [HttpGet("mine")]
    public Task<List<ApplicationResponse>> Mine()
    {
        var userId = CurrentUserId();
        return db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApplicationResponse(x.Id, x.UserId, x.User.FullName, x.AccommodationType, x.DocumentUrl, x.Status, x.CreatedAt, x.UpdatedAt))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationResponse>> Create([FromForm] ApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null) return Unauthorized();
        if (!ApplicantRoles.Contains(user.Role)) return BadRequest("Yonetici ve yetkili profilleri basvuru olusturamaz.");

        var documentUrl = await fileStorage.SaveAsync(request.Document, "documents", cancellationToken) ?? request.DocumentUrl;
        var application = new AccommodationApplication
        {
            UserId = userId,
            AccommodationType = request.AccommodationType,
            DocumentUrl = documentUrl,
            Status = ApplicationStatus.Pending
        };

        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ApplicationResponse(application.Id, application.UserId, user!.FullName, application.AccommodationType, application.DocumentUrl, application.Status, application.CreatedAt, application.UpdatedAt));
    }

    [HttpPost("{id:int}/decision")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<IActionResult> Decide(int id, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application is null) return NotFound();
        if (!ApplicantRoles.Contains(application.User.Role)) return BadRequest("Yonetici ve yetkili profilleri basvuru akışına dahil edilemez.");

        application.Status = request.Approved ? ApplicationStatus.Approved : ApplicationStatus.Rejected;
        application.UpdatedAt = DateTime.UtcNow;

        if (request.Approved && (request.AutoPlace || request.RoomId.HasValue))
        {
            await accommodationService.PlaceUserAsync(application.UserId, application.AccommodationType, request.RoomId, cancellationToken);
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
