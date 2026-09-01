using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
public class AdminFacilitiesController(AppDbContext db, IAccommodationService accommodationService) : ControllerBase
{
    // Admin -> null (tum tesisler); Yetkili -> yalnizca atandigi tesisler
    private async Task<FacilityScope?> GetFacilityScopeAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole(AppRoles.Admin)) return null;

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var assignments = await db.UserFacilityAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync(cancellationToken);

        return new FacilityScope(
            assignments.Where(x => x.DormitoryId != null).Select(x => x.DormitoryId!.Value).ToList(),
            assignments.Where(x => x.HousingUnitId != null).Select(x => x.HousingUnitId!.Value).ToList());
    }

    [HttpGet("dormitories")]
    public async Task<List<Dormitory>> GetDormitories(CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        return await db.Dormitories.AsNoTracking()
            .Where(x => scope == null || scope.DormitoryIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("dormitories")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Dormitory>> CreateDormitory(FacilityRequest request)
    {
        var entity = new Dormitory { Name = request.Name, Type = AccommodationType.Yurt, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.Dormitories.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetDormitories), new { id = entity.Id }, entity);
    }

    [HttpPut("dormitories/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateDormitory(int id, FacilityRequest request)
    {
        var entity = await db.Dormitories.FindAsync(id);
        if (entity is null) return NotFound();
        entity.Name = request.Name;
        entity.CampusLocation = request.CampusLocation;
        entity.TotalCapacity = request.TotalCapacity;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("dormitories/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteDormitory(int id)
    {
        var entity = await db.Dormitories.FindAsync(id);
        if (entity is null) return NotFound();
        db.Dormitories.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("housing-units")]
    public async Task<List<HousingUnit>> GetHousingUnits(CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        return await db.HousingUnits.AsNoTracking()
            .Where(x => scope == null || scope.HousingUnitIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("housing-units")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<HousingUnit>> CreateHousingUnit(FacilityRequest request)
    {
        var entity = new HousingUnit { Name = request.Name, Type = AccommodationType.Lojman, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.HousingUnits.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetHousingUnits), new { id = entity.Id }, entity);
    }

    [HttpPut("housing-units/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateHousingUnit(int id, FacilityRequest request)
    {
        var entity = await db.HousingUnits.FindAsync(id);
        if (entity is null) return NotFound();
        entity.Name = request.Name;
        entity.CampusLocation = request.CampusLocation;
        entity.TotalCapacity = request.TotalCapacity;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("housing-units/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteHousingUnit(int id)
    {
        var entity = await db.HousingUnits.FindAsync(id);
        if (entity is null) return NotFound();
        db.HousingUnits.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("buildings")]
    public async Task<List<Building>> GetBuildings(CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        return await db.Buildings.AsNoTracking().Include(x => x.Floors)
            .Where(x => scope == null ||
                (x.DormitoryId != null && scope.DormitoryIds.Contains(x.DormitoryId.Value)) ||
                (x.HousingUnitId != null && scope.HousingUnitIds.Contains(x.HousingUnitId.Value)))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("buildings")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Building>> CreateBuilding(BuildingRequest request)
    {
        if ((request.DormitoryId is null) == (request.HousingUnitId is null))
        {
            return BadRequest("Bir bina yalnizca bir yurt veya bir lojmana baglanmalidir.");
        }

        var entity = new Building { DormitoryId = request.DormitoryId, HousingUnitId = request.HousingUnitId, BlockName = request.BlockName };
        db.Buildings.Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("buildings/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateBuilding(int id, BuildingRequest request)
    {
        if ((request.DormitoryId is null) == (request.HousingUnitId is null)) return BadRequest("Bir bina yalnizca bir tesise baglanmalidir.");
        var entity = await db.Buildings.FindAsync(id);
        if (entity is null) return NotFound();
        entity.DormitoryId = request.DormitoryId;
        entity.HousingUnitId = request.HousingUnitId;
        entity.BlockName = request.BlockName;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("buildings/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteBuilding(int id)
    {
        var entity = await db.Buildings.FindAsync(id);
        if (entity is null) return NotFound();
        db.Buildings.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("floors")]
    public async Task<List<Floor>> GetFloors(CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        return await db.Floors.AsNoTracking().Include(x => x.Rooms)
            .Where(x => scope == null ||
                (x.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.Building.DormitoryId.Value)) ||
                (x.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.Building.HousingUnitId.Value)))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("floors")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Floor>> CreateFloor(FloorRequest request)
    {
        var entity = new Floor { BuildingId = request.BuildingId, FloorNumber = request.FloorNumber };
        db.Floors.Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("floors/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteFloor(int id)
    {
        var entity = await db.Floors.FindAsync(id);
        if (entity is null) return NotFound();
        db.Floors.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("rooms")]
    public async Task<List<RoomResponse>> GetRooms(CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);
        return await db.Rooms.AsNoTracking()
            .Where(x => scope == null ||
                (x.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(x.BlockFloor.Building.DormitoryId.Value)) ||
                (x.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(x.BlockFloor.Building.HousingUnitId.Value)))
            .Select(x => new RoomResponse(x.Id, x.BlockFloorId, x.RoomNumber, x.Capacity, x.CurrentOccupancy, x.Status, x.Price))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("rooms")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<RoomResponse>> CreateRoom(RoomRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var blockFloorExists = await db.Floors.AnyAsync(x => x.Id == request.BlockFloorId);
            if (!blockFloorExists)
            {
                return BadRequest("Geçersiz kat / blok bilgisi. Önce bina ve kat oluşturulmalıdır.");
            }

            var roomExists = await db.Rooms.AnyAsync(x => x.BlockFloorId == request.BlockFloorId && x.RoomNumber == request.RoomNumber);
            if (roomExists)
            {
                return Conflict("Aynı kat içinde aynı oda numarası mevcut.");
            }

            var room = new Room
            {
                BlockFloorId = request.BlockFloorId,
                RoomNumber = request.RoomNumber,
                Capacity = request.Capacity,
                Status = request.Status,
                Price = request.Price
            };

            accommodationService.RefreshRoomStatus(room);
            db.Rooms.Add(room);
            await db.SaveChangesAsync();

            return Ok(new RoomResponse(room.Id, room.BlockFloorId, room.RoomNumber, room.Capacity, room.CurrentOccupancy, room.Status, room.Price));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPut("rooms/{id:int}")]
    public async Task<IActionResult> UpdateRoom(int id, RoomRequest request, CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);

        var room = await db.Rooms.Include(x => x.BlockFloor).ThenInclude(x => x.Building).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (room is null) return NotFound();

        var inScope = scope == null ||
            (room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(room.BlockFloor.Building.DormitoryId.Value)) ||
            (room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(room.BlockFloor.Building.HousingUnitId.Value));
        if (!inScope) return NotFound("Oda bulunamadi.");

        if (request.Capacity < room.CurrentOccupancy) return BadRequest("Kapasite mevcut doluluktan dusuk olamaz.");

        var roomExists = await db.Rooms.AnyAsync(x => x.Id != id && x.BlockFloorId == request.BlockFloorId && x.RoomNumber == request.RoomNumber, cancellationToken);
        if (roomExists) return Conflict("Aynı kat içinde aynı oda numarası mevcut.");

        var priceChanged = room.Price != request.Price;

        room.BlockFloorId = request.BlockFloorId;
        room.RoomNumber = request.RoomNumber;
        room.Capacity = request.Capacity;
        room.Status = request.Status;
        room.Price = request.Price;
        accommodationService.RefreshRoomStatus(room);
        await db.SaveChangesAsync(cancellationToken);

        if (priceChanged)
        {
            await UpdateFuturePaymentsForRoomAsync(id, request.Price, cancellationToken);
        }

        return NoContent();
    }

    private async Task UpdateFuturePaymentsForRoomAsync(int roomId, decimal newPrice, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var threshold = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var userIds = await db.Placements.AsNoTracking()
            .Where(p => p.RoomId == roomId && p.IsActive)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0) return;

        var payments = await db.Payments
            .Where(p => userIds.Contains(p.UserId) && p.DueDate >= threshold && p.Status != PaymentStatus.Paid)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0) return;

        foreach (var payment in payments)
        {
            payment.Amount = newPrice;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    [HttpDelete("rooms/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var entity = await db.Rooms.FindAsync(id);
        if (entity is null) return NotFound();
        db.Rooms.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}