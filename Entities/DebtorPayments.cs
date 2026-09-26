using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopManagementSystem.Entities;

// Qarzdorga qilingan har bir alohida to'lovni saqlaydi (tarix uchun):
// kim (xodim yoki boshliq), qachon, qancha to'lagani.
public class DebtorPayment
{
    [Key]
    public int Id { get; set; }

    public int DebtorId { get; set; }

    [ForeignKey("DebtorId")]
    public Debtor? Debtor { get; set; }

    public int ShopId { get; set; }

    // Agar xodim to'lov qabul qilgan bo'lsa — uning IDsi shu yerda bo'ladi.
    // Boshliq tomonidan kiritilgan to'lovlarda bu null bo'ladi.
    public int? EmployeeId { get; set; }

    // Ko'rsatish uchun: "Boshliq" yoki xodimning ismi.
    public string PaidByName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}