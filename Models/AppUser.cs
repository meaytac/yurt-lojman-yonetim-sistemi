using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.Models;

public class AppUser : IdentityUser<Guid>
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(11), MinLength(11)]
    public string TcNo { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? StudentStaffNo { get; set; }

    [Required, MaxLength(30)]
    public string Role { get; set; } = AppRoles.Ogrenci;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AccommodationApplication> Applications { get; set; } = [];
    public ICollection<Placement> Placements { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<MaintenanceRequest> Requests { get; set; } = [];
}

public class AppRole : IdentityRole<Guid>
{
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Yetkili = "Yetkili";
    public const string Ogrenci = "Ogrenci";
    public const string Personel = "Personel";
    public const string TeknikPersonel = "TeknikPersonel";
    public const string TemizlikPersoneli = "TemizlikPersoneli";

    public static readonly string[] All = [Admin, Yetkili, Ogrenci, Personel, TeknikPersonel, TemizlikPersoneli];
}
