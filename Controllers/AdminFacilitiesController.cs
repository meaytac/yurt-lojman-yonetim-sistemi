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

        var rawUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return new FacilityScope([], []);
        }

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
    public async Task<ActionResult<Dormitory>> CreateDormitory(FacilityRequest request, CancellationToken cancellationToken)
    {
        if (await db.Dormitories.AnyAsync(x => x.Name == request.Name, cancellationToken))
        {
            return ConflictError("Bu ada sahip bir yurt zaten kayıtlı.");
        }

        var entity = new Dormitory { Name = request.Name, Type = AccommodationType.Yurt, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.Dormitories.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetDormitories), new { id = entity.Id }, entity);
    }

    [HttpPut("dormitories/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateDormitory(int id, FacilityRequest request)
    {
        var entity = await db.Dormitories.FindAsync(id);
        if (entity is null) return NotFoundError("Yurt bulunamadı.");
        if (await db.Dormitories.AnyAsync(x => x.Id != id && x.Name == request.Name)) return ConflictError("Bu ada sahip bir yurt zaten kayıtlı.");
        entity.Name = request.Name;
        entity.CampusLocation = request.CampusLocation;
        entity.TotalCapacity = request.TotalCapacity;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("dormitories/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public Task<IActionResult> DeleteDormitory(int id, CancellationToken cancellationToken)
        => DeleteFacilityHierarchyAsync(AccommodationType.Yurt, id, cancellationToken);

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
    public async Task<ActionResult<HousingUnit>> CreateHousingUnit(FacilityRequest request, CancellationToken cancellationToken)
    {
        if (await db.HousingUnits.AnyAsync(x => x.Name == request.Name, cancellationToken))
        {
            return ConflictError("Bu ada sahip bir lojman zaten kayıtlı.");
        }

        var entity = new HousingUnit { Name = request.Name, Type = AccommodationType.Lojman, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.HousingUnits.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetHousingUnits), new { id = entity.Id }, entity);
    }

    [HttpPut("housing-units/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateHousingUnit(int id, FacilityRequest request)
    {
        var entity = await db.HousingUnits.FindAsync(id);
        if (entity is null) return NotFoundError("Lojman bulunamadı.");
        if (await db.HousingUnits.AnyAsync(x => x.Id != id && x.Name == request.Name)) return ConflictError("Bu ada sahip bir lojman zaten kayıtlı.");
        entity.Name = request.Name;
        entity.CampusLocation = request.CampusLocation;
        entity.TotalCapacity = request.TotalCapacity;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("housing-units/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public Task<IActionResult> DeleteHousingUnit(int id, CancellationToken cancellationToken)
        => DeleteFacilityHierarchyAsync(AccommodationType.Lojman, id, cancellationToken);

    [HttpPost("facilities")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<AdminFacilityListItemDto>> CreateFacility(FacilityMutationRequest request, CancellationToken cancellationToken)
    {
        if (!IsSupportedFacilityType(request.Type)) return BadRequestError("Geçersiz tesis türü.");

        if (request.Type == AccommodationType.Yurt)
        {
            if (await db.Dormitories.AnyAsync(x => x.Name == request.Name, cancellationToken)) return ConflictError("Bu ada sahip bir yurt zaten kayıtlı.");
            var entity = new Dormitory { Name = request.Name, Type = AccommodationType.Yurt, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
            db.Dormitories.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            return Ok(ToFacilityDto(entity, 0));
        }

        if (await db.HousingUnits.AnyAsync(x => x.Name == request.Name, cancellationToken)) return ConflictError("Bu ada sahip bir lojman zaten kayıtlı.");
        var housing = new HousingUnit { Name = request.Name, Type = AccommodationType.Lojman, CampusLocation = request.CampusLocation, TotalCapacity = request.TotalCapacity, IsActive = request.IsActive };
        db.HousingUnits.Add(housing);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToFacilityDto(housing, 0));
    }

    [HttpPut("facilities/{type}/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<AdminFacilityListItemDto>> UpdateFacility(string type, int id, FacilityMutationRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseFacilityType(type, out var facilityType)) return BadRequestError("Geçersiz tesis türü.");
        if (facilityType != request.Type) return BadRequestError("Adres ve gövde içindeki tesis türü eşleşmiyor.");

        if (facilityType == AccommodationType.Yurt)
        {
            var entity = await db.Dormitories.FindAsync([id], cancellationToken);
            if (entity is null) return NotFoundError("Yurt bulunamadı.");
            if (await db.Dormitories.AnyAsync(x => x.Id != id && x.Name == request.Name, cancellationToken)) return ConflictError("Bu ada sahip bir yurt zaten kayıtlı.");
            entity.Name = request.Name;
            entity.CampusLocation = request.CampusLocation;
            entity.TotalCapacity = request.TotalCapacity;
            entity.IsActive = request.IsActive;
            await db.SaveChangesAsync(cancellationToken);
            var buildingCount = await db.Buildings.CountAsync(x => x.DormitoryId == id, cancellationToken);
            return Ok(ToFacilityDto(entity, buildingCount));
        }

        var housing = await db.HousingUnits.FindAsync([id], cancellationToken);
        if (housing is null) return NotFoundError("Lojman bulunamadı.");
        if (await db.HousingUnits.AnyAsync(x => x.Id != id && x.Name == request.Name, cancellationToken)) return ConflictError("Bu ada sahip bir lojman zaten kayıtlı.");
        housing.Name = request.Name;
        housing.CampusLocation = request.CampusLocation;
        housing.TotalCapacity = request.TotalCapacity;
        housing.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        var count = await db.Buildings.CountAsync(x => x.HousingUnitId == id, cancellationToken);
        return Ok(ToFacilityDto(housing, count));
    }

    [HttpDelete("facilities/{type}/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteFacility(string type, int id, CancellationToken cancellationToken)
    {
        if (!TryParseFacilityType(type, out var facilityType)) return BadRequestError("Geçersiz tesis türü.");

        return await DeleteFacilityHierarchyAsync(facilityType, id, cancellationToken);
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
            return BadRequestError("Bir blok yalnızca bir yurt veya bir lojmana bağlanmalıdır.");
        }

        if (request.DormitoryId.HasValue && !await db.Dormitories.AnyAsync(x => x.Id == request.DormitoryId.Value))
        {
            return BadRequestError("Seçilen yurt bulunamadı.");
        }

        if (request.HousingUnitId.HasValue && !await db.HousingUnits.AnyAsync(x => x.Id == request.HousingUnitId.Value))
        {
            return BadRequestError("Seçilen lojman bulunamadı.");
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
        if ((request.DormitoryId is null) == (request.HousingUnitId is null)) return BadRequestError("Bir blok yalnızca bir tesise bağlanmalıdır.");
        if (request.DormitoryId.HasValue && !await db.Dormitories.AnyAsync(x => x.Id == request.DormitoryId.Value)) return BadRequestError("Seçilen yurt bulunamadı.");
        if (request.HousingUnitId.HasValue && !await db.HousingUnits.AnyAsync(x => x.Id == request.HousingUnitId.Value)) return BadRequestError("Seçilen lojman bulunamadı.");
        var entity = await db.Buildings.FindAsync(id);
        if (entity is null) return NotFoundError("Blok bulunamadı.");
        entity.DormitoryId = request.DormitoryId;
        entity.HousingUnitId = request.HousingUnitId;
        entity.BlockName = request.BlockName;
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("buildings/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteBuilding(int id)
    {
        var entity = await db.Buildings.FindAsync(id);
        if (entity is null) return NotFoundError("Blok bulunamadı.");
        if (await db.Floors.AnyAsync(x => x.BuildingId == id)) return ConflictError("Bu bloğa bağlı katlar bulunduğu için önce katlar silinmelidir.");
        db.Buildings.Remove(entity);
        await db.SaveChangesAsync();
        return Ok(new { success = true, message = "Blok silindi." });
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
        if (!await db.Buildings.AnyAsync(x => x.Id == request.BuildingId))
        {
            return BadRequestError("Seçilen blok bulunamadı.");
        }

        if (await db.Floors.AnyAsync(x => x.BuildingId == request.BuildingId && x.FloorNumber == request.FloorNumber))
        {
            return ConflictError("Bu blok içinde aynı kat numarası zaten mevcut.");
        }

        var entity = new Floor { BuildingId = request.BuildingId, FloorNumber = request.FloorNumber };
        db.Floors.Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("floors/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateFloor(int id, FloorRequest request)
    {
        if (!await db.Buildings.AnyAsync(x => x.Id == request.BuildingId)) return BadRequestError("Seçilen blok bulunamadı.");
        if (await db.Floors.AnyAsync(x => x.Id != id && x.BuildingId == request.BuildingId && x.FloorNumber == request.FloorNumber)) return ConflictError("Bu blok içinde aynı kat numarası zaten mevcut.");
        var entity = await db.Floors.FindAsync(id);
        if (entity is null) return NotFoundError("Kat bulunamadı.");
        entity.BuildingId = request.BuildingId;
        entity.FloorNumber = request.FloorNumber;
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("floors/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteFloor(int id)
    {
        var entity = await db.Floors.FindAsync(id);
        if (entity is null) return NotFoundError("Kat bulunamadı.");
        if (await db.Rooms.AnyAsync(x => x.BlockFloorId == id)) return ConflictError("Bu kata bağlı odalar bulunduğu için önce odalar silinmelidir.");
        db.Floors.Remove(entity);
        await db.SaveChangesAsync();
        return Ok(new { success = true, message = "Kat silindi." });
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
    public async Task<ActionResult<AdminRoomListItemDto>> CreateRoom(RoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var blockFloorExists = await db.Floors.AnyAsync(x => x.Id == request.BlockFloorId, cancellationToken);
            if (!blockFloorExists)
            {
                return BadRequestError("Geçersiz kat / blok bilgisi. Önce tesis, blok ve kat oluşturulmalıdır.");
            }

            var roomExists = await db.Rooms.AnyAsync(x => x.BlockFloorId == request.BlockFloorId && x.RoomNumber == request.RoomNumber, cancellationToken);
            if (roomExists)
            {
                return ConflictError("Aynı kat içinde aynı oda numarası mevcut.");
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
            await db.SaveChangesAsync(cancellationToken);

            return Ok(await ToRoomDetailAsync(room.Id, cancellationToken));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
        }
    }

    [HttpPut("rooms/{id:int}")]
    public async Task<IActionResult> UpdateRoom(int id, RoomRequest request, CancellationToken cancellationToken)
    {
        var scope = await GetFacilityScopeAsync(cancellationToken);

        var room = await db.Rooms.Include(x => x.BlockFloor).ThenInclude(x => x.Building).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (room is null) return NotFoundError("Oda bulunamadı.");

        var inScope = scope == null ||
            (room.BlockFloor.Building.DormitoryId != null && scope.DormitoryIds.Contains(room.BlockFloor.Building.DormitoryId.Value)) ||
            (room.BlockFloor.Building.HousingUnitId != null && scope.HousingUnitIds.Contains(room.BlockFloor.Building.HousingUnitId.Value));
        if (!inScope) return NotFoundError("Oda bulunamadı.");

        if (!await db.Floors.AnyAsync(x => x.Id == request.BlockFloorId, cancellationToken)) return BadRequestError("Seçilen kat bulunamadı.");
        if (request.Capacity < room.CurrentOccupancy) return BadRequestError("Kapasite mevcut doluluktan düşük olamaz.");

        var roomExists = await db.Rooms.AnyAsync(x => x.Id != id && x.BlockFloorId == request.BlockFloorId && x.RoomNumber == request.RoomNumber, cancellationToken);
        if (roomExists) return ConflictError("Aynı kat içinde aynı oda numarası mevcut.");

        room.BlockFloorId = request.BlockFloorId;
        room.RoomNumber = request.RoomNumber;
        room.Capacity = request.Capacity;
        room.Status = request.Status;
        room.Price = request.Price;
        accommodationService.RefreshRoomStatus(room);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToRoomDetailAsync(room.Id, cancellationToken));
    }

    [HttpDelete("rooms/{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var entity = await db.Rooms.FindAsync(id);
        if (entity is null) return NotFoundError("Oda bulunamadı.");
        if (await db.Placements.AnyAsync(x => x.RoomId == id && x.IsActive)) return ConflictError("Bu odada aktif yerleşim bulunduğu için oda silinemez.");
        if (await db.Placements.AnyAsync(x => x.RoomId == id)) return ConflictError("Bu odaya bağlı yerleşim geçmişi bulunduğu için oda silinemez.");
        if (await db.Requests.AnyAsync(x => x.RoomId == id)) return ConflictError("Bu odaya bağlı arıza talepleri bulunduğu için önce ilişkili kayıtlar temizlenmelidir.");
        db.Rooms.Remove(entity);
        await db.SaveChangesAsync();
        return Ok(new { success = true, message = "Oda silindi." });
    }

    private static AdminFacilityListItemDto ToFacilityDto(Dormitory entity, int buildingCount)
        => new(entity.Id, entity.Name, entity.Type, entity.CampusLocation, entity.TotalCapacity, entity.IsActive, buildingCount);

    private static AdminFacilityListItemDto ToFacilityDto(HousingUnit entity, int buildingCount)
        => new(entity.Id, entity.Name, entity.Type, entity.CampusLocation, entity.TotalCapacity, entity.IsActive, buildingCount);

    private async Task<AdminRoomListItemDto> ToRoomDetailAsync(int roomId, CancellationToken cancellationToken)
        => await db.Rooms.AsNoTracking()
            .Where(x => x.Id == roomId)
            .Select(x => new AdminRoomListItemDto(
                x.Id,
                x.BlockFloorId,
                x.BlockFloor.Building.DormitoryId ?? x.BlockFloor.Building.HousingUnitId!.Value,
                x.BlockFloor.Building.Dormitory != null ? AccommodationType.Yurt : AccommodationType.Lojman,
                x.BlockFloor.Building.Dormitory != null ? x.BlockFloor.Building.Dormitory.Name : x.BlockFloor.Building.HousingUnit!.Name,
                x.BlockFloor.Building.BlockName,
                x.BlockFloor.FloorNumber,
                x.RoomNumber,
                x.Capacity,
                x.CurrentOccupancy,
                x.Status,
                x.Price))
            .FirstAsync(cancellationToken);

    private static bool TryParseFacilityType(string value, out AccommodationType type)
        => Enum.TryParse(value, ignoreCase: true, out type) && IsSupportedFacilityType(type);

    private static bool IsSupportedFacilityType(AccommodationType type)
        => type is AccommodationType.Yurt or AccommodationType.Lojman;

    private async Task<IActionResult> DeleteFacilityHierarchyAsync(AccommodationType facilityType, int id, CancellationToken cancellationToken)
    {
        var blockers = await GetFacilityDeleteBlockersAsync(facilityType, id, cancellationToken);
        if (blockers.Count > 0)
        {
            var names = string.Join(", ", blockers);
            return ConflictError($"Bu tesise bağlı oda içeren bloklar bulundu: {names}. Önce bu bloklardaki odaları ve ilişkili kayıtları temizleyin; boş bloklar tesis silme sırasında otomatik kaldırılır.");
        }

        await RemoveEmptyFacilityBuildingsAsync(facilityType, id, cancellationToken);

        if (facilityType == AccommodationType.Yurt)
        {
            var entity = await db.Dormitories.FindAsync([id], cancellationToken);
            if (entity is null) return NotFoundError("Yurt bulunamadı.");
            db.Dormitories.Remove(entity);
        }
        else
        {
            var entity = await db.HousingUnits.FindAsync([id], cancellationToken);
            if (entity is null) return NotFoundError("Lojman bulunamadı.");
            db.HousingUnits.Remove(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Tesis ve bağlı boş bloklar silindi." });
    }

    private async Task<List<string>> GetFacilityDeleteBlockersAsync(AccommodationType facilityType, int id, CancellationToken cancellationToken)
    {
        return await db.Buildings.AsNoTracking()
            .Where(x => facilityType == AccommodationType.Yurt ? x.DormitoryId == id : x.HousingUnitId == id)
            .Where(x => x.Floors.SelectMany(floor => floor.Rooms).Any())
            .OrderBy(x => x.BlockName)
            .Select(x => x.BlockName)
            .ToListAsync(cancellationToken);
    }

    private async Task RemoveEmptyFacilityBuildingsAsync(AccommodationType facilityType, int id, CancellationToken cancellationToken)
    {
        var buildings = await db.Buildings
            .Include(x => x.Floors)
            .Where(x => facilityType == AccommodationType.Yurt ? x.DormitoryId == id : x.HousingUnitId == id)
            .Where(x => !x.Floors.SelectMany(floor => floor.Rooms).Any())
            .ToListAsync(cancellationToken);

        db.Floors.RemoveRange(buildings.SelectMany(x => x.Floors));
        db.Buildings.RemoveRange(buildings);
    }

    private ObjectResult BadRequestError(string message) => StatusCode(StatusCodes.Status400BadRequest, new { success = false, message });

    private ObjectResult NotFoundError(string message) => StatusCode(StatusCodes.Status404NotFound, new { success = false, message });

    private ObjectResult ConflictError(string message) => StatusCode(StatusCodes.Status409Conflict, new { success = false, message });
}
