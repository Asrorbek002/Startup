using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Controllers;

[Route("api/shop-cabinet")]
[ApiController]
public class DebtorsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DebtorsController(AppDbContext context)
    {
        _context = context;
    }

    // Qarzdorlar ro'yxati (eng yangisi birinchi)
    [HttpGet("{shopId}/debtors")]
    public async Task<IActionResult> GetDebtors(int shopId)
    {
        var debtors = await _context.Debtors
            .Where(d => d.ShopId == shopId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Phone,
                d.Amount,
                d.PaidAmount,
                d.CreatedAt
            })
            .ToListAsync();

        return Ok(debtors);
    }

    // Yangi qarzdor qo'shish
    [HttpPost("{shopId}/debtors")]
    public async Task<IActionResult> CreateDebtor(int shopId, [FromBody] DebtorCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Phone) || dto.Amount <= 0)
            return BadRequest(new { success = false, message = "Ma'lumotlar noto'g'ri!" });

        var debtor = new Debtor
        {
            ShopId = shopId,
            Name = dto.Name.Trim(),
            Phone = dto.Phone.Trim(),
            Amount = dto.Amount,
            PaidAmount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Debtors.Add(debtor);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, id = debtor.Id });
    }

    // Qarzdor to'lovi (qoldiqdan oshmaydi)
    [HttpPost("{shopId}/debtors/{debtorId}/payments")]
    public async Task<IActionResult> PayDebtor(int shopId, int debtorId, [FromBody] DebtorPaymentDto dto)
    {
        if (dto.Amount <= 0)
            return BadRequest(new { success = false, message = "Miqdor noto'g'ri!" });

        var debtor = await _context.Debtors.FirstOrDefaultAsync(d => d.Id == debtorId && d.ShopId == shopId);
        if (debtor == null)
            return NotFound(new { success = false, message = "Qarzdor topilmadi!" });

        var remaining = debtor.Amount - debtor.PaidAmount;
        debtor.PaidAmount += Math.Min(dto.Amount, remaining);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, paidAmount = debtor.PaidAmount });
    }
}

public class DebtorCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class DebtorPaymentDto
{
    public decimal Amount { get; set; }
}