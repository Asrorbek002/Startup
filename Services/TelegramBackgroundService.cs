using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Services;

public class TelegramBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBackgroundService> _logger;

    // O'zbekiston vaqti: UTC+5 (yozgi/qishki vaqtga o'tish yo'q).
    // Render serveri UTC'da ishlaydi, shuning uchun soatlarni shu farq bilan hisoblaymiz.
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(5);

    // Pechat xodimlari kunlik hisoboti yuboriladigan vaqt (Toshkent vaqti)
    private static readonly TimeSpan PrintReportTime = new(18, 30, 0);

    // Kam qolgan elementlar bo'yicha "kirim qiling" eslatmasi yuboriladigan vaqt (Toshkent vaqti)
    private static readonly TimeSpan StockReminderTime = new(9, 0, 0);

    // Nechchi daqiqada bir marta tekshirib turish
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    // Xabar yuborilmasa (masalan token xato), qayta urinishdan oldin kutish
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(10);

    // Telegram xabari 4096 belgidan oshmasligi uchun ro'yxatda ko'rsatiladigan eng ko'p qator
    private const int MaxListItems = 40;

    private const string KindPrintReport = "PrintReport";
    private const string KindStockReminder = "StockReminder";

    // "Bugun bajarildi" degan belgini xotirada ham saqlaymiz, shunda har daqiqada bazaga so'rov ketmaydi
    private static readonly ConcurrentDictionary<string, string> DoneCache = new();
    private static readonly ConcurrentDictionary<string, DateTime> LastAttempt = new();

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
            // Bitta do'konda xatolik bo'lsa, boshqalariga xalaqit bermasin
            try { await CheckStockReminderAsync(context, telegram, settings, stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Kirim eslatmasida xatolik (ShopId={ShopId})", settings.ShopId); }

            try { await CheckPrintReportAsync(context, telegram, settings, stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Pechat hisobotida xatolik (ShopId={ShopId})", settings.ShopId); }

            try { await CheckDailyReportAsync(context, telegram, settings, stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Kunlik hisobotda xatolik (ShopId={ShopId})", settings.ShopId); }
        }
    }

    // ====================== KIRIM ESLATMASI (har kuni) ======================
    // Element qoldig'i belgilangan miqdordan (Telegram sozlamalaridagi "kam qoldiq chegarasi") kam bo'lsa,
    // kirim qilib miqdor oshirilmaguncha har kuni eslatib turadi. Kirim qilinib, qoldiq chegaradan oshsa,
    // eslatma o'zi to'xtaydi. (Qoldiq chegaradan tushgan zahoti keladigan xabar alohida — LowStockAlert.)
    private async Task CheckStockReminderAsync(AppDbContext context, ITelegramService telegram, TelegramSettings settings, CancellationToken ct)
    {
        var localNow = DateTime.UtcNow + LocalOffset;
        if (localNow.TimeOfDay < StockReminderTime) return;

        var dayKey = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await IsDoneAsync(context, settings.ShopId, KindStockReminder, dayKey, ct)) return;

        // Decimal solishtirish/tartiblash SQLite'da muammo bo'lmasligi uchun xotirada filtrlaymiz
        var elements = await context.Elements
            .Where(e => e.ShopId == settings.ShopId)
            .ToListAsync(ct);

        var low = elements
            .Where(e => e.Length <= settings.LowStockThreshold)
            .OrderBy(e => e.Length)
            .ThenBy(e => e.Name)
            .ToList();

        // Kam qolgan element yo'q — bugun uchun tekshiruv tugadi
        if (low.Count == 0)
        {
            await MarkDoneAsync(context, settings.ShopId, KindStockReminder, dayKey, ct);
            return;
        }

        if (RetryTooSoon(settings.ShopId, KindStockReminder)) return;

        var message =
            $"⏰ <b>Kirim eslatmasi</b>\n" +
            $"🗓 {localNow:dd.MM.yyyy}\n\n" +
            $"Quyidagi elementlar belgilangan miqdordan ({Num(settings.LowStockThreshold)}) kam qolgan va hali kirim qilinmagan:\n\n";

        foreach (var e in low.Take(MaxListItems))
            message += $"• {Esc(e.Name)}: <b>{Num(e.Length)}</b> {Esc(e.Unit)}\n";

        if (low.Count > MaxListItems)
            message += $"... va yana {low.Count - MaxListItems} ta\n";

        message += "\nKirim qilib, miqdorni oshirsangiz, eslatma to'xtaydi.";

        var sent = await telegram.SendMessageAsync(settings.BotToken, settings.ChatId, message);
        if (sent)
            await MarkDoneAsync(context, settings.ShopId, KindStockReminder, dayKey, ct);
    }

    // ====================== PECHAT XODIMLARI KUNLIK HISOBOTI ======================
    // Har kuni 18:30 (Toshkent vaqti) da: bugun har bir pechat xodimi nechta pechat chiqargani
    // va qaysi materialdan qancha ishlatgani. Bekor qilingan yozuvlar hisobga olinmaydi.
    private async Task CheckPrintReportAsync(AppDbContext context, ITelegramService telegram, TelegramSettings settings, CancellationToken ct)
    {
        // "Kunlik hisobot" o'chirilgan bo'lsa, bu hisobot ham yuborilmaydi
        if (!settings.DailyReportEnabled) return;

        var localNow = DateTime.UtcNow + LocalOffset;
        if (localNow.TimeOfDay < PrintReportTime) return;

        var dayKey = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await IsDoneAsync(context, settings.ShopId, KindPrintReport, dayKey, ct)) return;
        if (RetryTooSoon(settings.ShopId, KindPrintReport)) return;

        // Toshkent kunining boshi/oxiri -> UTC
        var dayStartUtc = localNow.Date - LocalOffset;
        var dayEndUtc = dayStartUtc.AddDays(1);

        var usages = await context.MaterialUsages
            .Where(u => u.ShopId == settings.ShopId
                        && !u.IsCancelled
                        && u.EmployeeRole == "Pechat"
                        && u.CreatedAt >= dayStartUtc
                        && u.CreatedAt < dayEndUtc)
            .ToListAsync(ct);

        var message = $"🖨 <b>Pechat xodimlari hisoboti</b>\n🗓 {localNow:dd.MM.yyyy}\n\n";

        if (usages.Count == 0)
        {
            message += "Bugun pechat xodimlari tomonidan birorta sarf yozilmagan.";
        }
        else
        {
            var totalPrints = 0;
            var totalMissing = 0;

            foreach (var g in usages
                         .GroupBy(u => new { u.EmployeeId, u.EmployeeName })
                         .OrderBy(g => g.Key.EmployeeName))
            {
                var prints = g.Sum(u => u.OutputCount ?? 0);
                var missing = g.Count(u => u.OutputCount == null);
                totalPrints += prints;
                totalMissing += missing;

                message += $"👤 <b>{Esc(g.Key.EmployeeName)}</b>\n";
                message += $"🖨 Chiqargan pechat: <b>{prints}</b> ta\n";

                foreach (var m in g
                             .GroupBy(u => new { u.ElementName, u.Unit })
                             .OrderBy(m => m.Key.ElementName))
                {
                    message += $"   • {Esc(m.Key.ElementName)}: {Num(m.Sum(u => u.Quantity))} {Esc(m.Key.Unit)}\n";
                }

                if (missing > 0)
                    message += $"   ⚠️ {missing} ta yozuvda pechat soni kiritilmagan\n";

                message += "\n";
            }

            message += $"📊 <b>Jami pechat: {totalPrints} ta</b>";
            if (totalMissing > 0)
                message += $"\n⚠️ Pechat soni kiritilmagan yozuvlar: {totalMissing} ta (jamiga kirmagan)";
        }

        var sent = await telegram.SendMessageAsync(settings.BotToken, settings.ChatId, message);
        if (sent)
            await MarkDoneAsync(context, settings.ShopId, KindPrintReport, dayKey, ct);
    }

    // ====================== KUNLIK SAVDO HISOBOTI (avvalgidek) ======================
    private async Task CheckDailyReportAsync(AppDbContext context, ITelegramService telegram, TelegramSettings settings, CancellationToken ct)
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

    // ====================== YORDAMCHI FUNKSIYALAR ======================
    private static string Esc(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    // 1970.5 -> "1 970.5"
    private static string Num(decimal value)
    {
        var s = Math.Round(value, 3).ToString("#,0.###", CultureInfo.InvariantCulture);
        return s.Replace(",", " ");
    }

    private static string CacheKey(int shopId, string kind) => $"{shopId}:{kind}";

    private static async Task<bool> IsDoneAsync(AppDbContext context, int shopId, string kind, string dayKey, CancellationToken ct)
    {
        if (DoneCache.TryGetValue(CacheKey(shopId, kind), out var cachedDay) && cachedDay == dayKey)
            return true;

        var exists = await context.BotDailyLogs
            .AnyAsync(l => l.ShopId == shopId && l.Kind == kind && l.DayKey == dayKey, ct);

        if (exists) DoneCache[CacheKey(shopId, kind)] = dayKey;
        return exists;
    }

    private static async Task MarkDoneAsync(AppDbContext context, int shopId, string kind, string dayKey, CancellationToken ct)
    {
        context.BotDailyLogs.Add(new BotDailyLog
        {
            ShopId = shopId,
            Kind = kind,
            DayKey = dayKey,
            SentAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct);
        DoneCache[CacheKey(shopId, kind)] = dayKey;
    }

    // Xabar yuborilmay qolsa (token noto'g'ri va h.k.) har daqiqada urinib, logni to'ldirmaslik uchun
    private static bool RetryTooSoon(int shopId, string kind)
    {
        var key = CacheKey(shopId, kind);
        var now = DateTime.UtcNow;
        if (LastAttempt.TryGetValue(key, out var last) && now - last < RetryDelay)
            return true;

        LastAttempt[key] = now;
        return false;
    }
}