using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record MyPlacementResponse(
    int Id,
    DateTime CheckInDate,
    string FacilityName,
    string FacilityType,
    string BlockName,
    int FloorNumber,
    string RoomNumber);

public record AdminPlacementAssignRequest(
    [Required] Guid UserId,
    int? RoomId,
    AccommodationType AccommodationType,
    bool AutoPlace,
    int? DormitoryId,
    int? HousingUnitId);

public record AdminPlacementListItemDto(
    int Id,
    Guid UserId,
    string FullName,
    int RoomId,
    string RoomNumber,
    DateTime CheckInDate,
    DateTime? CheckOutDate,
    bool IsActive);
