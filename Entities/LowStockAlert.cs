using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;

namespace ShopManagementSystem.Services;

// Bitta materialning (elementning) qoldig'i o'zgarishi: oldin va keyin
public record StockChange(string Name, string Unit, decimal Before, decimal After);

// Qoldiq kamaygan zahoti (sotuv, qarzga sotuv, sotuvni tahrirlash, material sarfi, elementni tahrirlash)
// chaqiriladi. Qoldiq "Kam qolish chegarasi"dan pastga tushgan bo'lsa, Telegram botga DARROV xabar yuboradi.
// Har bir sotuvda emas, faqat chegaradan "yuqoridan pastga o'tgan" paytda (spam bo'lmasligi uchun);
// qoldiq 0 ga tushsa alohida "TUGADI" xabari boradi.
public static class LowStockAlert
{
    public static async Task NotifyAsync(
        AppDbContext context,
        ITelegramService telegram,
        int shopId,
        params StockChange[] changes)
    {
        // Xabar yuborishdagi xatolik hech qachon savdoni/sarfni buzmasligi kerak
        try
        {
            var settings = await context.TelegramSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShopId == shopId);

            if (settings == null || !settings.IsEnabled
                || string.IsNullOrWhiteSpace(settings.BotToken)
                || string.IsNullOrWhiteSpace(settings.ChatId))
                return;

            var threshold = settings.LowStockThreshold;
            var lines = new List<string>();

            foreach (var c in changes)
            {
                if (c.After >= c.Before) continue; // faqat kamayish

                var soldOut = c.Before > 0 && c.After <= 0;
                var crossedLow = c.Before > threshold && c.After <= threshold;

                if (soldOut)
                    lines.Add($"❌ {c.Name} — TUGADI (qoldiq: 0 {c.Unit})");
                else if (crossedLow)
                    lines.Add($"⚠️ {c.Name} — qoldiq: {Fmt(c.After)} {c.Unit} (chegara: {Fmt(threshold)})");
            }

            if (lines.Count == 0) return;

            var text = "🔔 Kam qolgan mahsulot!\n\n" + string.Join("\n", lines);

            // Yuborish darrov boshlanadi. Telegram sekin javob bersa ham savdo 3 soniyadan ortiq kutmaydi.
            var send = telegram.SendMessageAsync(settings.BotToken, settings.ChatId, text);
            _ = send.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            await Task.WhenAny(send, Task.Delay(3000));
        }
        catch
        {
            // e'tiborsiz: ogohlantirish yuborilmasa ham asosiy amal muvaffaqiyatli tugaydi
        }
    }

    private static string Fmt(decimal v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}