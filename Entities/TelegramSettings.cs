namespace ShopManagementSystem.Entities;

public class TelegramSettings
{
    public int Id { get; set; }
    public int ShopId { get; set; }

    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    
    public decimal LowStockThreshold { get; set; } = 5;

    
    public int DailyReportHour { get; set; } = 21;
    public bool DailyReportEnabled { get; set; } = true;

   
    public DateTime? LastDailyReportSentAt { get; set; }
    public DateTime? LastLowStockAlertAt { get; set; }
}