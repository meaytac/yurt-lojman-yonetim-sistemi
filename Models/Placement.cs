namespace yurt_lojman_yonetim_sistemi.Models;

public class Placement
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime CheckInDate { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutDate { get; set; }
    public bool IsActive { get; set; } = true;
}
