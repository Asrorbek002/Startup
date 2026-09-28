using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopManagementSystem.Entities;

// Sex xodimlari (Usta / Pechat) ombordagi materialdan ishlatgan har bir miqdorni saqlaydi.
// Yozilganda Element.Length (real qoldiq) shu miqdorga kamayadi.
// Yozuv o'chirilmaydi — bekor qilinadi, shunda qoldiq qaytadi va tarix saqlanib qoladi.
public class MaterialUsage
{
    [Key]
    public int Id { get; set; }

    public int ShopId { get; set; }

    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    // "Usta" yoki "Pechat" (yozilgan paytdagi tur)
    public string EmployeeRole { get; set; } = string.Empty;

    public int ElementId { get; set; }
    public string ElementName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    // Faqat pechat xodimi uchun, ixtiyoriy: nechta pechat chiqarilgani
    public int? OutputCount { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledBy { get; set; }
}