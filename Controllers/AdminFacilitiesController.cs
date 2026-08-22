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
    private static readonly string[] AllowedDormitoryNames =
    [
        "MTÜ Erkek Öğrenci Yurdu",
        "MTÜ Kız Öğrenci Yurdu"
    ];

    private const string AllowedHousingUnitName = "MTÜ Akademik Personel Lojmanı";

    [HttpGet("dormitories")]
    public Task<List<Dormitory>> GetDormitories() => db.Dormitories.AsNoTracking().ToListAsync();

    [HttpPost("dormitories")]
    public async Task<ActionResult<Dormitory>> CreateDormitory(FacilityRequest request)
    {
        if (!AllowedDormitoryNames.Contains(request.Name) ||
            await db.Dormitories.AnyAsync(x => x.Name == request.Name) ||
            await db.Dormitories.CountAsync() >= AllowedDormitoryNames.Length)
        {
            return Conflict("Sistemde yalnızca tanımlı iki öğrenci yurdu bulunabilir.");
        }

        var entity = new Dormitory { Name = request.Name, Type = AccommodationType.Yurt, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.Dormitories.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetDormitories), new { id = entity.Id }, entity);
    }

    [HttpPut("dormitories/{id:int}")]
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
    public async Task<IActionResult> DeleteDormitory(int id)
    {
        return Conflict("Tanımlı tesis yapısı korunmalıdır; tesis silinemez.");
    }

    [HttpGet("housing-units")]
    public Task<List<HousingUnit>> GetHousingUnits() => db.HousingUnits.AsNoTracking().ToListAsync();

    [HttpPost("housing-units")]
    public async Task<ActionResult<HousingUnit>> CreateHousingUnit(FacilityRequest request)
    {
        if (request.Name != AllowedHousingUnitName || await db.HousingUnits.AnyAsync())
        {
            return Conflict("Sistemde yalnızca tanımlı akademik personel lojmanı bulunabilir.");
        }

        var entity = new HousingUnit { Name = request.Name, Type = AccommodationType.Lojman, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.HousingUnits.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetHousingUnits), new { id = entity.Id }, entity);
    }

    [HttpPut("housing-units/{id:int}")]
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
    public async Task<IActionResult> DeleteHousingUnit(int id)
    {
        return Conflict("Tanımlı tesis yapısı korunmalıdır; tesis silinemez.");
    }

    [HttpGet("buildings")]
    public Task<List<Building>> GetBuildings() => db.Buildings.AsNoTracking().Include(x => x.Floors).ToListAsync();

    [HttpPost("buildings")]
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
    public async Task<IActionResult> DeleteBuilding(int id)
    {
        var entity = await db.Buildings.FindAsync(id);
        if (entity is null) return NotFound();
        db.Buildings.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("floors")]
    public Task<List<Floor>> GetFloors() => db.Floors.AsNoTracking().Include(x => x.Rooms).ToListAsync();

    [HttpPost("floors")]
    public async Task<ActionResult<Floor>> CreateFloor(FloorRequest request)
    {
        var entity = new Floor { BuildingId = request.BuildingId, FloorNumber = request.FloorNumber };
        db.Floors.Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("floors/{id:int}")]
    public async Task<IActionResult> DeleteFloor(int id)
    {
        var entity = await db.Floors.FindAsync(id);
        if (entity is null) return NotFound();
        db.Floors.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("rooms")]
    public async Task<List<RoomResponse>> GetRooms()
    {
        return await db.Rooms.AsNoTracking()
            .Select(x => new RoomResponse(x.Id, x.BlockFloorId, x.RoomNumber, x.Capacity, x.CurrentOccupancy, x.Status, x.Price))
            .ToListAsync();
    }

    [HttpPost("rooms")]
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
    public async Task<IActionResult> UpdateRoom(int id, RoomRequest request)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        if (request.Capacity < room.CurrentOccupancy) return BadRequest("Kapasite mevcut doluluktan dusuk olamaz.");
        room.BlockFloorId = request.BlockFloorId;
        room.RoomNumber = request.RoomNumber;
        room.Capacity = request.Capacity;
        room.Status = request.Status;
        room.Price = request.Price;
        accommodationService.RefreshRoomStatus(room);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("rooms/{id:int}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var entity = await db.Rooms.FindAsync(id);
        if (entity is null) return NotFound();
        db.Rooms.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
