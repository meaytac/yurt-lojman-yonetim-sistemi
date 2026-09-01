using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/bot")]
public class BotController(AppDbContext db, IConfiguration configuration, ILogger<BotController> logger) : ControllerBase
{
    [HttpGet("webhook")]
    public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.verify_token")] string verifyToken, [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var expectedToken = configuration["WhatsApp:VerifyToken"] ?? "mtu-webhook-token";
        return mode == "subscribe" && verifyToken == expectedToken ? Content(challenge) : Forbid();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement payload)
    {
        logger.LogInformation("WhatsApp webhook received: {Payload}", payload.ToString());
        await Task.CompletedTask;
        return Ok(new { received = true });
    }

    [HttpGet("check-application")]
    public async Task<IActionResult> CheckApplication([FromQuery] string tcNo)
    {
        var application = await db.Applications.AsNoTracking()
            .Include(x => x.User)
            .Where(x => (x.User != null && x.User.TcNo == tcNo) || x.ApplicantTcNo == tcNo)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Status, x.AccommodationType, x.CreatedAt, x.UpdatedAt })
            .FirstOrDefaultAsync();

        return application is null ? NotFound("Basvuru bulunamadi.") : Ok(application);
    }

    [HttpGet("check-debt")]
    public async Task<IActionResult> CheckDebt([FromQuery] string tcNo)
    {
        var debts = await db.Payments.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.User.TcNo == tcNo)
            .OrderByDescending(x => x.DueDate)
            .Select(x => new { x.Amount, x.DueDate, x.PaidDate, Status = x.Status == PaymentStatus.Unpaid && x.DueDate < DateTime.UtcNow ? PaymentStatus.Overdue : x.Status, x.Description })
            .ToListAsync();

        return Ok(new { TotalDebt = debts.Where(x => x.Status != PaymentStatus.Paid).Sum(x => x.Amount), Items = debts });
    }

    [HttpPost("create-request")]
    public async Task<IActionResult> CreateRequest(BotCreateRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.TcNo == request.TcNo);
        if (user is null) return NotFound("Kullanici bulunamadi.");

        var entity = new MaintenanceRequest
        {
            UserId = user.Id,
            RoomId = request.RoomId,
            Category = request.Category,
            Description = request.Description,
            PhotoUrl = request.PhotoUrl,
            Status = RequestStatus.Open
        };

        db.Requests.Add(entity);
        await db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Status });
    }

    [HttpGet("announcements")]
    public Task<List<AnnouncementResponse>> Announcements()
    {
        return db.Announcements.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new AnnouncementResponse(x.Id, x.Title, x.Content, x.TargetRole, x.CreatedAt, x.IsActive))
            .ToListAsync();
    }
}
