namespace ShopManagementSystem.Entities;

// Parolni tiklash uchun yuborilgan bir martalik kodlar.
// Kodning o'zi saqlanmaydi, faqat hash ko'rinishi (CodeHash) saqlanadi.
public class PasswordResetCode
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }               // kod amal qilish muddati (5 daqiqa)
    public int Attempts { get; set; } = 0;                // noto'g'ri urinishlar soni
    public bool Used { get; set; } = false;
    public string? ResetTokenHash { get; set; }           // kod tasdiqlangach beriladigan token (hash)
    public DateTime? ResetTokenExpiresAt { get; set; }    // token muddati (10 daqiqa)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}