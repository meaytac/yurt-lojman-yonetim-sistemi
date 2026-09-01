using System.ComponentModel.DataAnnotations;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record FacilityRequest(
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(180)] string CampusLocation,
    [Range(0, int.MaxValue)] int TotalCapacity,
    bool IsActive,
    bool IsPublished = false,
    bool IsApplicationOpen = true,
    [MaxLength(1000)] string? PublicDescription = null,
    [MaxLength(1000)] string? Amenities = null,
    [MaxLength(500)] string? ImageUrl = null,
    [MaxLength(1000)] string? ApplicationConditions = null);

public record FacilityMutationRequest(
    [Required] AccommodationType Type,
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(180)] string CampusLocation,
    [Range(0, int.MaxValue)] int TotalCapacity,
    bool IsActive,
    bool IsPublished = false,
    bool IsApplicationOpen = true,
    [MaxLength(1000)] string? PublicDescription = null,
    [MaxLength(1000)] string? Amenities = null,
    [MaxLength(500)] string? ImageUrl = null,
    [MaxLength(1000)] string? ApplicationConditions = null);

public record BuildingRequest(int? DormitoryId, int? HousingUnitId, [Required, MaxLength(50)] string BlockName);

public record FloorRequest([Required] int BuildingId, [Range(0, 100)] int FloorNumber);

public record RoomRequest(
    [Required] int BlockFloorId,
    [Required, MaxLength(30)] string RoomNumber,
    [Range(1, 50)] int Capacity,
    RoomStatus Status,
    [Range(0, 999999)] decimal Price);

public record RoomResponse(int Id, int BlockFloorId, string RoomNumber, int Capacity, int CurrentOccupancy, RoomStatus Status, decimal Price);
