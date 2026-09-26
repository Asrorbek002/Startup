using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;

namespace ShopManagementSystem.Services;

public class TelegramBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBackgroundService> _logger;

    // Kam qoldiq haqidagi xabar bir xil narsalar uchun necha soatda bir marta qayta yuborilishi
    private static readonly TimeSpan LowStockAlertInterval = TimeSpan.FromHours(6);

    // Nechchi daqiqada bir marta tekshirib turish
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public TelegramBackgroundService(IServiceProvider serviceProvider, ILogger<TelegramBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Server ishga tushgandan keyin bazaning tayyor bo'lishini biroz kutamiz
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram background tekshiruvida xatolik");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunCheckAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

        var allSettings = await context.TelegramSettings
            .Where(s => s.IsEnabled)
            .ToListAsync(stoppingToken);

        foreach (var settings in allSettings)
        {
            await CheckLowStockAsync(context, telegram, settings, stoppingToken);
            await CheckDailyReportAsync(context, telegram, settings, stoppingToken);
        }
    }

    private async Task CheckLowStockAsync(AppDbContext context, ITelegramService telegram, ShopManagementSystem.Entities.TelegramSettings settings, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        if (settings.LastLowStockAlertAt.HasValue && now - settings.LastLowStockAlertAt.Value < LowStockAlertInterval)
            return;

        var lowProducts = await context.Products
            .Where(p => p.ShopId == settings.ShopId && p.Quantity <= settings.LowStockThreshold)
            .OrderBy(p => p.Quantity)
            .ToListAsync(ct);

        var lowElements = await context.Elements
            .Where(e => e.ShopId == settings.ShopId && e.Length <= settings.LowStockThreshold)
            .OrderBy(e => e.Length)
            .ToListAsync(ct);

        if (lowProducts.Count == 0 && lowElements.Count == 0)
            return;

        var message = "⚠️ <b>Kam qolgan mahsulot va elementlar</b>\n\n";

        if (lowProducts.Count > 0)
        {
            message += "📦 <b>Tovarlar:</b>\n";
            foreach (var p in lowProducts)
                message += $"• {p.Name}: {p.Quantity} {p.Unit}\n";
            message += "\n";
        }

        if (lowElements.Count > 0)
        {
            message += "🧱 <b>Elementlar:</b>\n";
            foreach (var e in lowElements)
                message += $"• {e.Name}: {e.Length} {e.Unit}\n";
        }

        var sent = await telegram.SendMessageAsync(settings.BotToken, settings.ChatId, message);
        if (sent)
        {
            settings.LastLowStockAlertAt = now;
            await context.SaveChangesAsync(ct);
        }
    }

    private async Task CheckDailyReportAsync(AppDbContext context, ITelegramService telegram, ShopManagementSystem.Entities.TelegramSettings settings, CancellationToken ct)
    {
        if (!settings.DailyReportEnabled)
            return;

        var now = DateTime.UtcNow;

        var alreadySentToday = settings.LastDailyReportSentAt.HasValue &&
                                settings.LastDailyReportSentAt.Value.Date == now.Date;

        if (alreadySentToday || now.Hour < settings.DailyReportHour)
            return;

        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);

        var todaysSales = await context.Sales
            .Where(s => s.ShopId == settings.ShopId && s.SoldAt >= todayStart && s.SoldAt < todayEnd)
            .ToListAsync(ct);

        var totalSum = todaysSales.Sum(s => s.SalePrice * s.Quantity);
        var totalCount = todaysSales.Count;
        var totalQuantity = todaysSales.Sum(s => s.Quantity);

        var message =
            $"📊 <b>Kunlik hisobot</b>\n" +
            $"🗓 {now:dd.MM.yyyy}\n\n" +
            $"🧾 Savdolar soni: {totalCount} ta\n" +
            $"📦 Sotilgan miqdor: {totalQuantity} dona\n" +
            $"💰 Umumiy summa: {totalSum:N0} so'm";

        var sent = await telegram.SendMessageAsync(settings.BotToken, settings.ChatId, message);
        if (sent)
        {
            settings.LastDailyReportSentAt = now;
            await context.SaveChangesAsync(ct);
        }
    }
}