using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/placements")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
public class PlacementsController(AppDbContext db, IAccommodationService accommodationService) : ControllerBase
{
    [HttpGet]
    public Task<List<Placement>> GetAll()
    {
        return db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room)
            .OrderByDescending(x => x.CheckInDate)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Placement>> Create(Guid userId, AccommodationType accommodationType, int? roomId, CancellationToken cancellationToken)
    {
        var placement = await accommodationService.PlaceUserAsync(userId, accommodationType, roomId, cancellationToken);
        return Ok(placement);
    }

    [HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CancellationToken cancellationToken)
    {
        await accommodationService.CheckoutAsync(id, cancellationToken);
        return NoContent();
    }
}
