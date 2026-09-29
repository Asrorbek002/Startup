using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopManagementSystem.Entities;

// Ta'minotchi (materialni kimdan olganingiz).
// TotalAmount - ta'minotchidan olingan barcha tovarlarning umumiy summasi,
// PaidAmount - unga to'langan summa. Qarz = TotalAmount - PaidAmount (siz unga qarzsiz).
public class Supplier
{
    [Key]
    public int Id { get; set; }

    public int ShopId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// "Tovar qabul qilish" da ta'minotchidan olingan har bir partiya.
public class SupplierPurchase
{
    [Key]
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public int ShopId { get; set; }

    // Products jadvalidagi qabul yozuvi (agar bog'langan bo'lsa)
    public int? ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    // Partiyaning umumiy summasi (Quantity * BuyPrice)
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    // Tovar qabul qilingan paytda darrov to'langan summa
    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Ta'minotchiga keyinchalik qilingan to'lovlar (qarzni uzish tarixi).
public class SupplierPayment
{
    [Key]
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public int ShopId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }
}