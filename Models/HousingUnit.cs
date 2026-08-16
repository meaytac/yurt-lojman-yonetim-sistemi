using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class HousingUnit
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public AccommodationType Type { get; set; } = AccommodationType.Lojman;

    [Required, MaxLength(180)]
    public string CampusLocation { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int TotalCapacity { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Building> Buildings { get; set; } = [];
}
