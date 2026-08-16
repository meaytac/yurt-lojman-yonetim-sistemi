using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class Building
{
    public int Id { get; set; }

    public int? DormitoryId { get; set; }
    public Dormitory? Dormitory { get; set; }

    public int? HousingUnitId { get; set; }
    public HousingUnit? HousingUnit { get; set; }

    [Required, MaxLength(50)]
    public string BlockName { get; set; } = string.Empty;

    public ICollection<Floor> Floors { get; set; } = [];
}
