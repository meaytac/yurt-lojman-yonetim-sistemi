using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsController(AppDbContext db, IFileStorageService fileStorage, IApplicationWorkflowService workflowService) : ControllerBase
{
    private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public Task<List<ApplicationResponse>> GetAll()
    {
        return db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.User != null && ApplicantRoles.Contains(x.User.Role))
            .Where(x => x.Status == ApplicationStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApplicationResponse(x.Id, x.UserId, x.User!.FullName, x.AccommodationType, x.DocumentUrl, x.Status, x.CreatedAt, x.UpdatedAt))
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
            .Select(x => new ApplicationResponse(x.Id, x.UserId, x.User!.FullName, x.AccommodationType, x.DocumentUrl, x.Status, x.CreatedAt, x.UpdatedAt))
            .ToListAsync();
    }

    [HttpGet("eligibility")]
    public async Task<ApplicationEligibilityResponse> Eligibility(CancellationToken cancellationToken)
        => await GetEligibilityAsync(CurrentUserId(), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ApplicationResponse>> Create([FromForm] ApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null) return Unauthorized();
        if (!ApplicantRoles.Contains(user.Role)) return BadRequest("Yönetici ve yetkili profilleri başvuru oluşturamaz.");
        var eligibility = await GetEligibilityAsync(userId, cancellationToken);
        if (!eligibility.CanApply) return Conflict(eligibility);

        var documentUrl = await fileStorage.SaveAsync(request.Document, "documents", cancellationToken) ?? request.DocumentUrl;
        var application = new AccommodationApplication
        {
            UserId = userId,
            User = user,
            Source = ApplicationSource.RegisteredUser,
            AccommodationType = request.AccommodationType,
            DocumentUrl = documentUrl,
            Status = ApplicationStatus.Pending,
            ReferenceCode = $"RG{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}"
        };
        application.StatusHistory.Add(new ApplicationStatusHistory { Status = ApplicationStatus.Pending, Note = "Kayıtlı kullanıcı başvurusu oluşturuldu." });

        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ApplicationResponse(application.Id, application.UserId, user.FullName, application.AccommodationType, application.DocumentUrl, application.Status, application.CreatedAt, application.UpdatedAt));
    }

    [HttpPost("{id:int}/decision")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<IActionResult> Decide(int id, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application is null) return NotFound();
        if (application.User is null || !ApplicantRoles.Contains(application.User.Role)) return BadRequest("Yönetici ve yetkili profilleri başvuru akışına dahil edilemez.");

        var actorId = CurrentUserId();
        if (request.Approved)
        {
            await workflowService.ApproveAsync(actorId, id, request, null, null, cancellationToken);
        }
        else
        {
            await workflowService.RejectAsync(actorId, id, request, null, null, cancellationToken);
        }

        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : throw new UnauthorizedAccessException();
    }

    private async Task<ApplicationEligibilityResponse> GetEligibilityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var blockedStatuses = new[]
        {
            ApplicationStatus.EmailVerificationPending,
            ApplicationStatus.Pending,
            ApplicationStatus.UnderReview,
            ApplicationStatus.MissingInformation,
            ApplicationStatus.ApprovedAwaitingActivation,
            ApplicationStatus.Approved
        };

        if (await db.Applications.AsNoTracking().AnyAsync(x => x.UserId == userId && blockedStatuses.Contains(x.Status), cancellationToken))
        {
            return new(false, "active_application_exists", "Devam eden veya onaylanmış bir başvurunuz bulunuyor.");
        }

        if (await db.Placements.AsNoTracking().AnyAsync(x => x.UserId == userId && x.IsActive, cancellationToken))
        {
            return new(false, "active_placement_exists", "Aktif yerleşiminiz bulunduğu için yeni konaklama başvurusu oluşturamazsınız.");
        }

        return new(true, "eligible", "Yeni başvuru oluşturabilirsiniz.");
    }
}
