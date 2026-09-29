using System.ComponentModel.DataAnnotations;

namespace ShopManagementSystem.Entities;

// Botga kunlik xabarlar (pechat hisoboti, kirim eslatmasi) bugun yuborilganini eslab qoladi,
// shunda server qayta ishga tushsa ham xabar ikki marta ketmaydi.
public class BotDailyLog
{
    [Key]
    public int Id { get; set; }

    public int ShopId { get; set; }

    // "PrintReport" yoki "StockReminder"
    public string Kind { get; set; } = string.Empty;

    // Toshkent vaqti bo'yicha kun: "2026-09-29"
    public string DayKey { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}