using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IAccommodationService
{
    Task<Placement> PlaceUserAsync(Guid userId, AccommodationType type, int? roomId, CancellationToken cancellationToken);
    Task CheckoutAsync(int placementId, CancellationToken cancellationToken);
    void RefreshRoomStatus(Room room);
}

public class AccommodationService(AppDbContext db) : IAccommodationService
{
    public async Task<Placement> PlaceUserAsync(Guid userId, AccommodationType type, int? roomId, CancellationToken cancellationToken)
    {
        var hasActivePlacement = await db.Placements.AnyAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
        if (hasActivePlacement)
        {
            throw new InvalidOperationException("Kullanicinin aktif bir yerlestirmesi zaten var.");
        }

        var room = roomId.HasValue
            ? await db.Rooms.Include(x => x.BlockFloor).ThenInclude(x => x.Building).FirstOrDefaultAsync(x => x.Id == roomId.Value, cancellationToken)
            : await FindAvailableRoomAsync(type, cancellationToken);

        if (room is null)
        {
            throw new InvalidOperationException("Uygun oda bulunamadi.");
        }

        if (room.Status == RoomStatus.Maintenance || room.CurrentOccupancy >= room.Capacity)
        {
            throw new InvalidOperationException("Secilen oda yerlestirmeye uygun degil.");
        }

        var placement = new Placement
        {
            UserId = userId,
            RoomId = room.Id,
            CheckInDate = DateTime.UtcNow,
            IsActive = true
        };

        room.CurrentOccupancy++;
        RefreshRoomStatus(room);

        db.Placements.Add(placement);
        await db.SaveChangesAsync(cancellationToken);
        return placement;
    }

    public async Task CheckoutAsync(int placementId, CancellationToken cancellationToken)
    {
        var placement = await db.Placements.Include(x => x.Room)
            .FirstOrDefaultAsync(x => x.Id == placementId, cancellationToken);

        if (placement is null)
        {
            throw new KeyNotFoundException("Yerlestirme bulunamadi.");
        }

        if (!placement.IsActive)
        {
            return;
        }

        placement.IsActive = false;
        placement.CheckOutDate = DateTime.UtcNow;
        placement.Room.CurrentOccupancy = Math.Max(0, placement.Room.CurrentOccupancy - 1);
        RefreshRoomStatus(placement.Room);
        await db.SaveChangesAsync(cancellationToken);
    }

    public void RefreshRoomStatus(Room room)
    {
        if (room.Status == RoomStatus.Maintenance)
        {
            return;
        }

        room.Status = room.CurrentOccupancy <= 0
            ? RoomStatus.Empty
            : room.CurrentOccupancy >= room.Capacity
                ? RoomStatus.Full
                : RoomStatus.PartiallyFull;
    }

    private Task<Room?> FindAvailableRoomAsync(AccommodationType type, CancellationToken cancellationToken)
    {
        return db.Rooms
            .Include(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .Where(x => x.Status != RoomStatus.Maintenance && x.CurrentOccupancy < x.Capacity)
            .Where(x => type == AccommodationType.Yurt
                ? x.BlockFloor.Building.DormitoryId != null
                : x.BlockFloor.Building.HousingUnitId != null)
            .OrderByDescending(x => x.CurrentOccupancy)
            .ThenBy(x => x.RoomNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
