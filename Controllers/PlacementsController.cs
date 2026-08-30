using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/placements")]
[Authorize]
public class PlacementsController(AppDbContext db, IAccommodationService accommodationService) : ControllerBase
{
    private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<MyPlacementResponse>> Mine(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var placement = await db.Placements.AsNoTracking()
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
            .Include(x => x.Room).ThenInclude(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.CheckInDate)
            .Select(x => new MyPlacementResponse(
                x.Id,
                x.CheckInDate,
                x.Room.BlockFloor.Building.Dormitory != null ? x.Room.BlockFloor.Building.Dormitory.Name : x.Room.BlockFloor.Building.HousingUnit!.Name,
                x.Room.BlockFloor.Building.Dormitory != null ? "Yurt" : "Lojman",
                x.Room.BlockFloor.Building.BlockName,
                x.Room.BlockFloor.FloorNumber,
                x.Room.RoomNumber))
            .FirstOrDefaultAsync(cancellationToken);

        return placement is null ? NotFound("Aktif yerlestirme bulunamadi.") : Ok(placement);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public Task<List<Placement>> GetAll()
    {
        return db.Placements.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Room)
            .Where(x => ApplicantRoles.Contains(x.User.Role))
            .OrderByDescending(x => x.CheckInDate)
            .ToListAsync();
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<ActionResult<Placement>> Create(Guid userId, AccommodationType accommodationType, int? roomId, CancellationToken cancellationToken)
    {
        var placement = await accommodationService.PlaceUserAsync(userId, accommodationType, roomId, cancellationToken);
        return Ok(placement);
    }

    [HttpPost("{id:int}/checkout")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
    public async Task<IActionResult> Checkout(int id, CancellationToken cancellationToken)
    {
        await accommodationService.CheckoutAsync(id, cancellationToken);
        return NoContent();
    }
}
