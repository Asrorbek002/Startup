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

    // Qarzdor to'lovi (qoldiqdan oshmaydi) — har bir to'lov alohida tarix sifatida ham saqlanadi
    [HttpPost("{shopId}/debtors/{debtorId}/payments")]
    public async Task<IActionResult> PayDebtor(int shopId, int debtorId, [FromBody] DebtorPaymentDto dto)
    {
        if (dto.Amount <= 0)
            return BadRequest(new { success = false, message = "Miqdor noto'g'ri!" });

        var debtor = await _context.Debtors.FirstOrDefaultAsync(d => d.Id == debtorId && d.ShopId == shopId);
        if (debtor == null)
            return NotFound(new { success = false, message = "Qarzdor topilmadi!" });

        var remaining = debtor.Amount - debtor.PaidAmount;
        var actualAmount = Math.Min(dto.Amount, remaining);
        debtor.PaidAmount += actualAmount;

        var payerName = !string.IsNullOrWhiteSpace(dto.PaidByName)
            ? dto.PaidByName
            : (dto.EmployeeId.HasValue ? "Xodim" : "Boshliq");

        _context.DebtorPayments.Add(new DebtorPayment
        {
            DebtorId = debtor.Id,
            ShopId = shopId,
            EmployeeId = dto.EmployeeId,
            PaidByName = payerName,
            Amount = actualAmount,
            PaidAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new { success = true, paidAmount = debtor.PaidAmount });
    }

    // Bitta qarzdorning barcha to'lovlari tarixi (kim, qachon, qancha)
    [HttpGet("{shopId}/debtors/{debtorId}/payments")]
    public async Task<IActionResult> GetDebtorPayments(int shopId, int debtorId)
    {
        var payments = await _context.DebtorPayments
            .Where(p => p.DebtorId == debtorId && p.ShopId == shopId)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new
            {
                p.Id,
                p.Amount,
                p.PaidAt,
                p.EmployeeId,
                p.PaidByName
            })
            .ToListAsync();

        return Ok(payments);
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
    public int? EmployeeId { get; set; }
    public string? PaidByName { get; set; }
}