using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class Floor
{
    public int Id { get; set; }

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    [Range(0, 100)]
    public int FloorNumber { get; set; }

    public ICollection<Room> Rooms { get; set; } = [];
}
