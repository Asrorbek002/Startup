using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Controllers;

// Ta'minotchilar va ularga bo'lgan qarz (siz ularga qarzsiz).
// Ta'minotchi "Tovar qabul qilish" paytida yoziladi (ShopCabinetController.CreateProduct),
// bu yerda ro'yxat, to'lov, tahrirlash va tarix bilan ishlanadi.
[Route("api/shop-cabinet")]
[ApiController]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuppliersController(AppDbContext context)
    {
        _context = context;
    }

    // Ta'minotchilar ro'yxati
    [HttpGet("{shopId}/suppliers")]
    public async Task<IActionResult> GetSuppliers(int shopId)
    {
        var suppliers = await _context.Suppliers
            .Where(s => s.ShopId == shopId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(suppliers.Select(s => new
        {
            s.Id,
            s.Name,
            s.Phone,
            s.TotalAmount,
            s.PaidAmount,
            Remaining = Math.Max(0, s.TotalAmount - s.PaidAmount),
            s.CreatedAt
        }));
    }

    // Yangi ta'minotchi qo'shish (ixtiyoriy boshlang'ich qarz bilan)
    [HttpPost("{shopId}/suppliers")]
    public async Task<IActionResult> CreateSupplier(int shopId, [FromBody] SupplierCreateDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Ta'minotchi nomini kiriting!" });
        if (dto.InitialDebt < 0)
            return BadRequest(new { success = false, message = "Qarz summasi noto'g'ri!" });

        var exists = await _context.Suppliers
            .AnyAsync(s => s.ShopId == shopId && s.Name.ToLower() == name.ToLower());
        if (exists)
            return BadRequest(new { success = false, message = "Bunday ta'minotchi allaqachon mavjud!" });

        var supplier = new Supplier
        {
            ShopId = shopId,
            Name = name,
            Phone = (dto.Phone ?? string.Empty).Trim(),
            TotalAmount = dto.InitialDebt,
            PaidAmount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        if (dto.InitialDebt > 0)
        {
            _context.SupplierPurchases.Add(new SupplierPurchase
            {
                SupplierId = supplier.Id,
                ShopId = shopId,
                ProductName = "Boshlang'ich qarz",
                Unit = "",
                Quantity = 0,
                TotalAmount = dto.InitialDebt,
                PaidAmount = 0,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, id = supplier.Id });
    }

    // Ism / telefonni tahrirlash
    [HttpPut("{shopId}/suppliers/{supplierId}")]
    public async Task<IActionResult> UpdateSupplier(int shopId, int supplierId, [FromBody] SupplierCreateDto dto)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.ShopId == shopId);
        if (supplier == null)
            return NotFound(new { success = false, message = "Ta'minotchi topilmadi!" });

        var name = (dto.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Ta'minotchi nomini kiriting!" });

        supplier.Name = name;
        supplier.Phone = (dto.Phone ?? string.Empty).Trim();
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // Ta'minotchiga to'lov qilish (qarzni uzish)
    [HttpPost("{shopId}/suppliers/{supplierId}/payments")]
    public async Task<IActionResult> PaySupplier(int shopId, int supplierId, [FromBody] SupplierPaymentDto dto)
    {
        if (dto.Amount <= 0)
            return BadRequest(new { success = false, message = "Miqdor noto'g'ri!" });

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.ShopId == shopId);
        if (supplier == null)
            return NotFound(new { success = false, message = "Ta'minotchi topilmadi!" });

        var remaining = supplier.TotalAmount - supplier.PaidAmount;
        if (remaining <= 0)
            return BadRequest(new { success = false, message = "Bu ta'minotchiga qarz yo'q!" });
        if (dto.Amount > remaining)
            return BadRequest(new { success = false, message = $"To'lov qarzdan oshib ketdi! Qarz: {remaining:N0} so'm" });

        supplier.PaidAmount += dto.Amount;

        _context.SupplierPayments.Add(new SupplierPayment
        {
            SupplierId = supplier.Id,
            ShopId = shopId,
            Amount = dto.Amount,
            PaidAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
        });

        await _context.SaveChangesAsync();
        return Ok(new { success = true, remaining = supplier.TotalAmount - supplier.PaidAmount });
    }

    // Ta'minotchi tarixi: qabul qilingan tovarlar va qilingan to'lovlar (bitta ro'yxatda, yangisi birinchi)
    [HttpGet("{shopId}/suppliers/{supplierId}/history")]
    public async Task<IActionResult> GetHistory(int shopId, int supplierId)
    {
        var purchases = await _context.SupplierPurchases
            .Where(p => p.SupplierId == supplierId && p.ShopId == shopId)
            .ToListAsync();

        var payments = await _context.SupplierPayments
            .Where(p => p.SupplierId == supplierId && p.ShopId == shopId)
            .ToListAsync();

        var items = purchases.Select(p => new
        {
            type = "purchase",
            date = p.CreatedAt,
            title = p.ProductName,
            unit = p.Unit,
            quantity = p.Quantity,
            total = p.TotalAmount,
            paid = p.PaidAmount,
            note = (string?)null
        }).Concat(payments.Select(p => new
        {
            type = "payment",
            date = p.PaidAt,
            title = "To'lov",
            unit = "",
            quantity = 0m,
            total = 0m,
            paid = p.Amount,
            note = p.Note
        })).OrderByDescending(x => x.date).ToList();

        return Ok(items);
    }

    // Ta'minotchini o'chirish (tarixi bilan)
    [HttpDelete("{shopId}/suppliers/{supplierId}")]
    public async Task<IActionResult> DeleteSupplier(int shopId, int supplierId)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.ShopId == shopId);
        if (supplier == null)
            return NotFound(new { success = false, message = "Ta'minotchi topilmadi!" });

        _context.SupplierPurchases.RemoveRange(_context.SupplierPurchases.Where(p => p.SupplierId == supplierId && p.ShopId == shopId));
        _context.SupplierPayments.RemoveRange(_context.SupplierPayments.Where(p => p.SupplierId == supplierId && p.ShopId == shopId));
        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}

public class SupplierCreateDto
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public decimal InitialDebt { get; set; }
}

public class SupplierPaymentDto
{
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}