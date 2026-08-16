using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace yurt_lojman_yonetim_sistemi.Models;

public class Payment
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 999999)]
    public decimal Amount { get; set; }

    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Unpaid;

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;
}
