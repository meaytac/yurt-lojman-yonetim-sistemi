using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record AdminActiveToggleRequest(bool IsActive);

public record AdminFacilityListItemDto(
    int Id,
    string Name,
    AccommodationType Type,
    string CampusLocation,
    int TotalCapacity,
    bool IsActive,
    int BuildingCount);

public record AdminRoomListItemDto(
    int Id,
    int BlockFloorId,
    int FacilityId,
    AccommodationType FacilityType,
    string FacilityName,
    string BlockName,
    int FloorNumber,
    string RoomNumber,
    int Capacity,
    int CurrentOccupancy,
    RoomStatus Status,
    decimal Price);

public record AdminRoomOccupantDto(
    int PlacementId,
    Guid UserId,
    string FullName,
    string TcNo,
    string? StudentStaffNo,
    string Role,
    DateTime CheckInDate);

public record AdminRoomOccupantsResponse(
    int RoomId,
    string RoomNumber,
    int Capacity,
    int CurrentOccupancy,
    RoomStatus Status,
    IReadOnlyList<AdminRoomOccupantDto> Occupants);
