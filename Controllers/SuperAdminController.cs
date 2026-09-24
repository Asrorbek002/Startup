using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Controllers;

[Route("api/super-admin")]
[ApiController]
public class SuperAdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuperAdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("shops")]
    public async Task<IActionResult> GetAllShops([FromHeader(Name = "Secret-Key")] string secretKey)
    {
        if (secretKey != "MY_SUPER_SECRET_ADMIN_KEY_2026")
            return Unauthorized(new { message = "Ruxsat etilmagan kalit! Sizda admin huquqi yo'q." });

        var shops = await _context.Shops.ToListAsync();
        return Ok(shops);
    }

    [HttpPost("create-shop")]
    public async Task<IActionResult> CreateShop([FromHeader(Name = "Secret-Key")] string secretKey, [FromBody] ShopCreateDto dto)
    {
        if (secretKey != "MY_SUPER_SECRET_ADMIN_KEY_2026")
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var shop = new Shop
        {
            Name = dto.Name,
            Region = dto.Region,
            Phone = dto.Phone,
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Tariff = dto.Tariff,
            Balance = 0,
            Status = "Faollashtirilgan"
        };

        _context.Shops.Add(shop);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Savdo nuqtasi muvaffaqiyatli yaratildi va tarif belgilendi!", shopId = shop.Id });
    }

    [HttpPut("update-shop/{id}")]
    public async Task<IActionResult> UpdateShop([FromHeader(Name = "Secret-Key")] string secretKey, int id, [FromBody] ShopCreateDto dto)
    {
        if (secretKey != "MY_SUPER_SECRET_ADMIN_KEY_2026")
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var shop = await _context.Shops.FindAsync(id);
        if (shop == null)
            return NotFound(new { message = "Do'kon topilmadi!" });

        shop.Name = dto.Name;
        shop.Region = dto.Region;
        shop.Phone = dto.Phone;
        shop.Username = dto.Username;
        shop.Tariff = dto.Tariff;

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
        if (secretKey != "MY_SUPER_SECRET_ADMIN_KEY_2026")
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
        if (secretKey != "MY_SUPER_SECRET_ADMIN_KEY_2026")
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
        if (secretKey != "MY_SUPER_SECRET_ADMIN_KEY_2026")
            return Unauthorized(new { message = "Ruxsat etilmagan kalit!" });

        var shop = await _context.Shops.FindAsync(id);
        if (shop == null)
            return NotFound(new { message = "Do'kon topilmadi!" });

        if (dto.Amount <= 0)
            return BadRequest(new { message = "Miqdor noto'g'ri!" });

        shop.Balance += dto.Amount;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Balans muvaffaqiyatli to'ldirildi!", newBalance = shop.Balance });
    }
}

public class ShopCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public decimal Tariff { get; set; } = 200000;
    public string Password { get; set; } = string.Empty;
}

public class DepositDto
{
    public decimal Amount { get; set; }
}