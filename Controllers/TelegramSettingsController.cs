using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;
using ShopManagementSystem.Services;

namespace ShopManagementSystem.Controllers;

[Route("api/shop-cabinet")]
[ApiController]
public class TelegramSettingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITelegramService _telegram;

    public TelegramSettingsController(AppDbContext context, ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    // Joriy sozlamalarni olish
    [HttpGet("{shopId}/telegram-settings")]
    public async Task<IActionResult> GetSettings(int shopId)
    {
        var settings = await _context.TelegramSettings.FirstOrDefaultAsync(s => s.ShopId == shopId);

        if (settings == null)
        {
            return Ok(new
            {
                shopId,
                botToken = "",
                chatId = "",
                isEnabled = false,
                lowStockThreshold = 5,
                dailyReportEnabled = true,
                dailyReportHour = 21
            });
        }

        return Ok(new
        {
            settings.ShopId,
            settings.BotToken,
            settings.ChatId,
            settings.IsEnabled,
            settings.LowStockThreshold,
            settings.DailyReportEnabled,
            settings.DailyReportHour
        });
    }

    // Sozlamalarni saqlash (birinchi marta yaratish yoki yangilash)
    [HttpPost("{shopId}/telegram-settings")]
    public async Task<IActionResult> SaveSettings(int shopId, [FromBody] TelegramSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.BotToken) || string.IsNullOrWhiteSpace(dto.ChatId))
            return BadRequest(new { success = false, message = "Bot token va Chat ID kiritilishi shart!" });

        var settings = await _context.TelegramSettings.FirstOrDefaultAsync(s => s.ShopId == shopId);

        if (settings == null)
        {
            settings = new TelegramSettings { ShopId = shopId };
            _context.TelegramSettings.Add(settings);
        }

        settings.BotToken = dto.BotToken.Trim();
        settings.ChatId = dto.ChatId.Trim();
        settings.IsEnabled = dto.IsEnabled;
        settings.LowStockThreshold = dto.LowStockThreshold;
        settings.DailyReportEnabled = dto.DailyReportEnabled;
        settings.DailyReportHour = dto.DailyReportHour;

        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // "Test" tugmasi — sozlamalar to'g'ri ishlashini tekshirish uchun sinov xabari yuboradi
    [HttpPost("{shopId}/telegram-settings/test")]
    public async Task<IActionResult> TestMessage(int shopId)
    {
        var settings = await _context.TelegramSettings.FirstOrDefaultAsync(s => s.ShopId == shopId);
        if (settings == null || string.IsNullOrWhiteSpace(settings.BotToken) || string.IsNullOrWhiteSpace(settings.ChatId))
            return BadRequest(new { success = false, message = "Avval sozlamalarni saqlang!" });

        var sent = await _telegram.SendMessageAsync(settings.BotToken, settings.ChatId, "✅ Bot muvaffaqiyatli ulandi! Endi hisobotlar va ogohlantirishlar shu yerga keladi.");

        if (!sent)
            return BadRequest(new { success = false, message = "Xabar yuborilmadi. Token yoki Chat ID noto'g'ri bo'lishi mumkin." });

        return Ok(new { success = true });
    }
}

public class TelegramSettingsDto
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public decimal LowStockThreshold { get; set; } = 5;
    public bool DailyReportEnabled { get; set; } = true;
    public int DailyReportHour { get; set; } = 21;
}