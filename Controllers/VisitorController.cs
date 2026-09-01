using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/visitor")]
public class VisitorController(AppDbContext db, UserManager<AppUser> userManager, IFileStorageService fileStorage) : ControllerBase
{
    [HttpPost("applications")]
    public async Task<IActionResult> CreateApplication([FromForm] VisitorApplicationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Ad soyad ve e-posta zorunludur.");

        if (request.Document == null || request.Document.Length == 0)
            return BadRequest("Öğrenci belgesi zorunludur.");

        var existingUser = await userManager.FindByEmailAsync(request.Email);
        AppUser user;
        if (existingUser != null)
        {
            user = existingUser;
        }
        else
        {
            user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                TcNo = $"9{Random.Shared.Next(100000000, 999999999)}1",
                Role = AppRoles.Ogrenci,
                EmailConfirmed = true,
                MustChangePassword = true
            };
            var createResult = await userManager.CreateAsync(user, "Ogrenci123");
            if (!createResult.Succeeded)
                return BadRequest(string.Join(" ", createResult.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, AppRoles.Ogrenci);
        }

        var documentUrl = await fileStorage.SaveAsync(request.Document, "documents", cancellationToken);

        var application = new AccommodationApplication
        {
            UserId = user.Id,
            AccommodationType = request.AccommodationType,
            DocumentUrl = documentUrl,
            Status = ApplicationStatus.Pending
        };
        if (!string.IsNullOrWhiteSpace(request.OptionalText))
        {
            // Optional text is appended to document URL as note — or could be stored elsewhere
            application.DocumentUrl = documentUrl + (string.IsNullOrEmpty(documentUrl) ? "" : " ") + $"[Not: {request.OptionalText}]";
        }

        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { application.Id, application.Status });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("E-posta gerekli.");
        var user = await userManager.FindByEmailAsync(email);
        if (user == null) return Ok(new { hasNotification = false });
        var latestApp = await db.Applications.AsNoTracking().Where(x => x.UserId == user.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (latestApp == null || latestApp.Status != ApplicationStatus.Approved) return Ok(new { hasNotification = false });
        return Ok(new
        {
            hasNotification = true,
            message = "Başvurunuz onaylandı! Giriş için \"Ogrenci123\" şifresi atandı. Artık bu hesabınızla kayıtlı hesap girişi yapabilirsiniz. Bu mesaj onaylandıktan sonra e-postanıza bağlı ziyaretçi hesabı silinecektir.",
            applicationId = latestApp.Id,
            email = user.Email
        });
    }

    public class VisitorApplicationRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public AccommodationType AccommodationType { get; set; }
        public IFormFile? Document { get; set; }
        public string? OptionalText { get; set; }
    }
}
