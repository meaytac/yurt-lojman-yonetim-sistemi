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
    public async Task<List<PaymentResponse>> Mine()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await EnsurePaymentPlanAsync(userId);
        return await Query(userId).ToListAsync();
    }

    private async Task EnsurePaymentPlanAsync(Guid userId)
    {
        var placement = await db.Placements.AsNoTracking()
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive);

        if (placement is null) return;

        var price = placement.Room.Price;
        var checkIn = placement.CheckInDate;
        int startYear;
        if (checkIn.Month >= 10) startYear = checkIn.Year;
        else if (checkIn.Month <= 6) startYear = checkIn.Year - 1;
        else startYear = checkIn.Year;

        var months = new (int Year, int Month, string Name)[]
        {
            (startYear, 10, "Ekim"), (startYear, 11, "Kasım"), (startYear, 12, "Aralık"),
            (startYear + 1, 1, "Ocak"), (startYear + 1, 2, "Şubat"), (startYear + 1, 3, "Mart"),
            (startYear + 1, 4, "Nisan"), (startYear + 1, 5, "Mayıs"), (startYear + 1, 6, "Haziran")
        };

        var facility = placement.Room.BlockFloor.Building.Dormitory != null
            ? placement.Room.BlockFloor.Building.Dormitory.Name
            : placement.Room.BlockFloor.Building.HousingUnit!.Name;
        var roomLabel = $"{facility} {placement.Room.BlockFloor.Building.BlockName} {placement.Room.RoomNumber}";

        var existing = await db.Payments.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.DueDate.Year, x.DueDate.Month })
            .ToListAsync();

        var toAdd = new List<Payment>();
        foreach (var (year, month, name) in months)
        {
            if (existing.Any(e => e.Year == year && e.Month == month)) continue;

            var dueDate = new DateTime(year, month, 15, 0, 0, 0, DateTimeKind.Utc);
            toAdd.Add(new Payment
            {
                UserId = userId,
                Amount = price,
                DueDate = dueDate,
                Description = $"{name} {year} konaklama ücreti - {roomLabel}",
                Status = PaymentStatus.Unpaid
            });
        }

        if (toAdd.Count > 0)
        {
            db.Payments.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
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
