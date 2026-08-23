using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(AppDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public Task<List<PaymentResponse>> Mine()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Query(userId).ToListAsync();
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public Task<List<PaymentResponse>> GetAll() => Query().ToListAsync();

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<ActionResult<PaymentResponse>> Create(PaymentCreateRequest request)
    {
        var payment = new Payment { UserId = request.UserId, Amount = request.Amount, DueDate = request.DueDate, Description = request.Description, Status = PaymentStatus.Unpaid };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return Ok(ToResponse(payment));
    }

    [HttpPost("{id:int}/paid")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<IActionResult> MarkPaid(int id, PaymentMarkPaidRequest request)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment is null) return NotFound();
        payment.PaidDate = request.PaidDate ?? DateTime.UtcNow;
        payment.Status = PaymentStatus.Paid;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("mine/pay-latest-due")]
    public async Task<ActionResult<PaymentResponse>> PayLatestDue()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTime.UtcNow;
        var payment = await db.Payments
            .Where(x => x.UserId == userId && x.DueDate <= now && x.Status != PaymentStatus.Paid)
            .OrderByDescending(x => x.DueDate)
            .FirstOrDefaultAsync();

        if (payment is null)
        {
            return NotFound("Vadesi gelmiş ödenmemiş borç bulunmuyor.");
        }

        payment.PaidDate = now;
        payment.Status = PaymentStatus.Paid;
        await db.SaveChangesAsync();
        return Ok(ToResponse(payment));
    }

    private IQueryable<PaymentResponse> Query(Guid? userId = null)
    {
        var now = DateTime.UtcNow;
        var payments = db.Payments.AsNoTracking();
        if (userId.HasValue)
        {
            payments = payments.Where(x => x.UserId == userId.Value);
        }

        return payments
            .OrderByDescending(x => x.DueDate)
            .Select(x => new PaymentResponse(x.Id, x.UserId, x.Amount, x.DueDate, x.PaidDate,
                x.Status == PaymentStatus.Unpaid && x.DueDate < now ? PaymentStatus.Overdue : x.Status,
                x.Description));
    }

    private static PaymentResponse ToResponse(Payment x) => new(x.Id, x.UserId, x.Amount, x.DueDate, x.PaidDate, x.Status, x.Description);
}
