using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopManagementSystem.Entities;

// Mijozning buyurtmasi. Mijoz pulning bir qismini oldindan (avans) berishi mumkin:
// TotalAmount - buyurtma umumiy summasi, PaidAmount - hozirgacha to'langan (avans + keyingi to'lovlar).
// Qolgan summa = TotalAmount - PaidAmount. Uni bir tugma bilan "Qarzdor"ga o'tkazish mumkin.
public class Order
{
    public const string StatusNew = "Yangi";
    public const string StatusReady = "Tayyor";
    public const string StatusDelivered = "Topshirilgan";
    public const string StatusDebt = "Qarzga o'tkazilgan";

    [Key]
    public int Id { get; set; }

    public int ShopId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    // Nima buyurtma qilingani (masalan: "Peshtaxta 2x3 m, 5 dona")
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    public string Status { get; set; } = StatusNew;

    // Buyurtma topshirilishi kerak bo'lgan sana (ixtiyoriy)
    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Qolgan summa "Qarzdor"ga o'tkazilgan bo'lsa:
    public int? DebtorId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransferredAmount { get; set; }

    public DateTime? TransferredAt { get; set; }
}

// Buyurtma bo'yicha har bir alohida to'lov (birinchisi - avans).
public class OrderPayment
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }
    public int ShopId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public string PaidByName { get; set; } = string.Empty;

    public string? Note { get; set; }
}