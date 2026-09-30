namespace ShopManagementSystem.Entities;

// Do'kon balansidagi har bir o'zgarish: to'ldirish (topup) yoki oylik tarif yechish (charge)
public class BalanceTransaction
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public string Type { get; set; } = string.Empty;   // "topup" yoki "charge"
    public decimal Amount { get; set; }                // topup: musbat, charge: manfiy
    public decimal BalanceAfter { get; set; }          // amaldan keyingi balans
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}