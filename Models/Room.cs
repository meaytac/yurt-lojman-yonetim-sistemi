using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace yurt_lojman_yonetim_sistemi.Models;

public class Room
{
    public int Id { get; set; }

    public int BlockFloorId { get; set; }
    public Floor BlockFloor { get; set; } = null!;

    [Required, MaxLength(30)]
    public string RoomNumber { get; set; } = string.Empty;

    [Range(1, 50)]
    public int Capacity { get; set; }

    [Range(0, 50)]
    public int CurrentOccupancy { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Empty;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999999)]
    public decimal Price { get; set; }

    public ICollection<Placement> Placements { get; set; } = [];
    public ICollection<MaintenanceRequest> Requests { get; set; } = [];
}
