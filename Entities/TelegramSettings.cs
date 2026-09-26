namespace ShopManagementSystem.Entities;

public class TelegramSettings
{
    public int Id { get; set; }
    public int ShopId { get; set; }

    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // Qoldiq shu sondan kam yoki teng bo'lsa, "kam qoldi" xabari yuboriladi
    public decimal LowStockThreshold { get; set; } = 5;

    // Kunlik hisobot qaysi soatda (0-23, server vaqti bo'yicha) yuborilishi
    public int DailyReportHour { get; set; } = 21;
    public bool DailyReportEnabled { get; set; } = true;

    // Spam bo'lmasligi uchun oxirgi yuborilgan vaqtlar
    public DateTime? LastDailyReportSentAt { get; set; }
    public DateTime? LastLowStockAlertAt { get; set; }
}