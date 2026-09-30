using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;
using ShopManagementSystem.Services;

namespace ShopManagementSystem.Controllers;

[Route("api/super-admin")]
[ApiController]
public class SuperAdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AdminAuthService _adminAuth;

    public SuperAdminController(AppDbContext context, AdminAuthService adminAuth)
    {
        _context = context;
        _adminAuth = adminAuth;
    }

    // Email: bo'sh bo'lsa null, aks holda kichik harfda va tozalangan holda qaytaradi
    private static string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static bool IsValidOptionalEmail(string? email)
    {
        var e = NormalizeEmail(email);
        if (e == null) return true;
        if (e.Length > 200) return false;
        try { return new System.Net.Mail.MailAddress(e).Address == e; }
        catch { return false; }
    }

    [HttpGet("shops")]
    public async Task<IActionResult> GetAllShops([FromHeader(Name = "Secret-Key")] string secretKey)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit! Sizda admin huquqi yo'q." });

        var shops = await _context.Shops.ToListAsync();
        return Ok(shops);
    }

    [HttpPost("create-shop")]
    public async Task<IActionResult> CreateShop([FromHeader(Name = "Secret-Key")] string secretKey, [FromBody] ShopCreateDto dto)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        if (!IsValidOptionalEmail(dto.Email))
            return BadRequest(new { message = "Email manzil noto'g'ri kiritilgan!" });

        var shop = new Shop
        {
            Name = dto.Name,
            Region = dto.Region,
            Phone = dto.Phone,
            Email = NormalizeEmail(dto.Email),
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Tariff = dto.Tariff,
            Balance = 0,
            CreditLimit = dto.CreditLimit ?? 0,                   // limit berilmasa - 0 (minusga ruxsat yo'q)
            NextBillingDate = DateTime.UtcNow.AddMonths(1),       // birinchi yechish - 1 oydan keyin
            Status = "Faollashtirilgan"
        };

        _context.Shops.Add(shop);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Savdo nuqtasi muvaffaqiyatli yaratildi va tarif belgilendi!", shopId = shop.Id });
    }

    [HttpPut("update-shop/{id}")]
    public async Task<IActionResult> UpdateShop([FromHeader(Name = "Secret-Key")] string secretKey, int id, [FromBody] ShopCreateDto dto)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        if (!IsValidOptionalEmail(dto.Email))
            return BadRequest(new { message = "Email manzil noto'g'ri kiritilgan!" });

        var shop = await _context.Shops.FindAsync(id);
        if (shop == null)
            return NotFound(new { message = "Do'kon topilmadi!" });

        shop.Name = dto.Name;
        shop.Region = dto.Region;
        shop.Phone = dto.Phone;
        shop.Username = dto.Username;
        if (dto.Email != null) shop.Email = NormalizeEmail(dto.Email);            // yuborilmasa (null) eski email saqlanadi, bo'sh matn yuborilsa o'chiriladi
        shop.Tariff = dto.Tariff;
        if (dto.CreditLimit.HasValue) shop.CreditLimit = dto.CreditLimit.Value;   // yuborilmasa eski limit saqlanadi
        BillingLogic.SyncStatus(shop);                                            // limit o'zgargan bo'lsa bloklash/ochish

        if (!string.IsNullOrEmpty(dto.Password))
        {
            shop.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Do'kon muvaffaqiyatli tahrirlandi!" });
    }

    [HttpPatch("toggle-status/{id}")]
    public async Task<IActionResult> ToggleShopStatus([FromHeader(Name = "Secret-Key")] string secretKey, int id)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var shop = await _context.Shops.FindAsync(id);
        if (shop == null)
            return NotFound(new { message = "Do'kon topilmadi!" });

        if (shop.Status == "Faollashtirilgan" || shop.Status == "Faol")
        {
            shop.Status = "Faol emas";
        }
        else
        {
            shop.Status = "Faollashtirilgan";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"Do'kon holati o'zgartirildi: {shop.Status}", newStatus = shop.Status });
    }

    [HttpDelete("delete-shop/{id}")]
    public async Task<IActionResult> DeleteShop([FromHeader(Name = "Secret-Key")] string secretKey, int id)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var shop = await _context.Shops.FindAsync(id);
        if (shop == null)
            return NotFound(new { message = "Do'kon topilmadi!" });

        _context.Shops.Remove(shop);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Do'kon butunlay o'chirib tashlandi!" });
    }

    // Do'kon balansiga pul qo'shish (to'ldirish)
    [HttpPost("shops/{id}/deposit")]
    public async Task<IActionResult> DepositToShop([FromHeader(Name = "Secret-Key")] string secretKey, int id, [FromBody] DepositDto dto)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var shop = await _context.Shops.FindAsync(id);
        if (shop == null)
            return NotFound(new { message = "Do'kon topilmadi!" });

        if (dto.Amount <= 0)
            return BadRequest(new { message = "Miqdor noto'g'ri!" });

        shop.Balance += dto.Amount;

        _context.BalanceTransactions.Add(new BalanceTransaction
        {
            ShopId = shop.Id,
            Type = "topup",
            Amount = dto.Amount,
            BalanceAfter = shop.Balance,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? "Balans to'ldirildi" : dto.Note,
            CreatedAt = DateTime.UtcNow
        });
        BillingLogic.SyncStatus(shop); // balans limit ichiga qaytsa, do'kon avtomatik ochiladi

        await _context.SaveChangesAsync();

        return Ok(new { message = "Balans muvaffaqiyatli to'ldirildi!", newBalance = shop.Balance, newStatus = shop.Status });
    }

    // Do'konning balans tarixi (to'ldirishlar va oylik yechishlar)
    [HttpGet("shops/{id}/transactions")]
    public async Task<IActionResult> GetShopTransactions([FromHeader(Name = "Secret-Key")] string secretKey, int id)
    {
        if (!_adminAuth.IsValidToken(secretKey))
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var list = (await _context.BalanceTransactions
            .Where(t => t.ShopId == id)
            .ToListAsync())
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToList();

        return Ok(list);
    }
}

public class ShopCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }          // ixtiyoriy: parolni tiklash kodi shu emailga boradi
    public string Username { get; set; } = string.Empty;
    public decimal Tariff { get; set; } = 200000;
    public decimal? CreditLimit { get; set; }   // ixtiyoriy: balans qancha minusga tushishi mumkin
    public string Password { get; set; } = string.Empty;
}

public class DepositDto
{
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}