using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Controllers;

// Buyurtmalar (avans bilan) va qolgan summani "Qarzdor"ga o'tkazish.
[Route("api/shop-cabinet")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrdersController(AppDbContext context)
    {
        _context = context;
    }

    private static object ToDto(Order o)
    {
        var remaining = o.Status == Order.StatusDebt ? 0 : Math.Max(0, o.TotalAmount - o.PaidAmount);
        return new
        {
            o.Id,
            o.CustomerName,
            o.CustomerPhone,
            o.Description,
            o.TotalAmount,
            o.PaidAmount,
            Remaining = remaining,
            o.Status,
            o.DueDate,
            o.CreatedAt,
            o.DebtorId,
            o.TransferredAmount,
            o.TransferredAt
        };
    }

    // Buyurtmalar ro'yxati (eng yangisi birinchi)
    [HttpGet("{shopId}/orders")]
    public async Task<IActionResult> GetOrders(int shopId)
    {
        var orders = await _context.Orders
            .Where(o => o.ShopId == shopId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(ToDto));
    }

    // Yangi buyurtma (ixtiyoriy avans bilan)
    [HttpPost("{shopId}/orders")]
    public async Task<IActionResult> CreateOrder(int shopId, [FromBody] OrderCreateDto dto)
    {
        var name = (dto.CustomerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Mijoz ismini kiriting!" });
        if (dto.TotalAmount <= 0)
            return BadRequest(new { success = false, message = "Buyurtma summasi noto'g'ri!" });
        if (dto.AdvanceAmount < 0)
            return BadRequest(new { success = false, message = "Avans summasi noto'g'ri!" });
        if (dto.AdvanceAmount > dto.TotalAmount)
            return BadRequest(new { success = false, message = "Avans buyurtma summasidan oshib ketdi!" });

        var order = new Order
        {
            ShopId = shopId,
            CustomerName = name,
            CustomerPhone = (dto.CustomerPhone ?? string.Empty).Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            TotalAmount = dto.TotalAmount,
            PaidAmount = dto.AdvanceAmount,
            Status = Order.StatusNew,
            DueDate = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        if (dto.AdvanceAmount > 0)
        {
            _context.OrderPayments.Add(new OrderPayment
            {
                OrderId = order.Id,
                ShopId = shopId,
                Amount = dto.AdvanceAmount,
                PaidAt = DateTime.UtcNow,
                PaidByName = "Boshliq",
                Note = "Avans (oldindan to'lov)"
            });
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, id = order.Id });
    }

    // Buyurtma ma'lumotlarini tahrirlash (summa to'langan summadan kam bo'lmasligi kerak)
    [HttpPut("{shopId}/orders/{orderId}")]
    public async Task<IActionResult> UpdateOrder(int shopId, int orderId, [FromBody] OrderUpdateDto dto)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shopId);
        if (order == null)
            return NotFound(new { success = false, message = "Buyurtma topilmadi!" });
        if (order.Status == Order.StatusDebt)
            return BadRequest(new { success = false, message = "Qarzdorga o'tkazilgan buyurtmani tahrirlab bo'lmaydi!" });

        var name = (dto.CustomerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Mijoz ismini kiriting!" });
        if (dto.TotalAmount <= 0 || dto.TotalAmount < order.PaidAmount)
            return BadRequest(new { success = false, message = "Buyurtma summasi noto'g'ri (to'langan summadan kam bo'lmasin)!" });

        order.CustomerName = name;
        order.CustomerPhone = (dto.CustomerPhone ?? string.Empty).Trim();
        order.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        order.TotalAmount = dto.TotalAmount;
        order.DueDate = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc) : null;

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // Buyurtmaga qo'shimcha to'lov (avansdan keyingi to'lovlar)
    [HttpPost("{shopId}/orders/{orderId}/payments")]
    public async Task<IActionResult> PayOrder(int shopId, int orderId, [FromBody] OrderPaymentDto dto)
    {
        if (dto.Amount <= 0)
            return BadRequest(new { success = false, message = "Miqdor noto'g'ri!" });

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shopId);
        if (order == null)
            return NotFound(new { success = false, message = "Buyurtma topilmadi!" });
        if (order.Status == Order.StatusDebt)
            return BadRequest(new { success = false, message = "Qolgan summa qarzdorga o'tkazilgan. To'lovni Qarzdor bo'limida qabul qiling." });

        var remaining = order.TotalAmount - order.PaidAmount;
        if (remaining <= 0)
            return BadRequest(new { success = false, message = "Buyurtma allaqachon to'liq to'langan!" });
        if (dto.Amount > remaining)
            return BadRequest(new { success = false, message = $"To'lov qoldiqdan oshib ketdi! Qoldiq: {remaining:N0} so'm" });

        order.PaidAmount += dto.Amount;

        _context.OrderPayments.Add(new OrderPayment
        {
            OrderId = order.Id,
            ShopId = shopId,
            Amount = dto.Amount,
            PaidAt = DateTime.UtcNow,
            PaidByName = string.IsNullOrWhiteSpace(dto.PaidByName) ? "Boshliq" : dto.PaidByName.Trim(),
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
        });

        await _context.SaveChangesAsync();
        return Ok(new { success = true, paidAmount = order.PaidAmount, remaining = order.TotalAmount - order.PaidAmount });
    }

    // Buyurtma to'lovlari tarixi
    [HttpGet("{shopId}/orders/{orderId}/payments")]
    public async Task<IActionResult> GetOrderPayments(int shopId, int orderId)
    {
        var payments = await _context.OrderPayments
            .Where(p => p.OrderId == orderId && p.ShopId == shopId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        return Ok(payments.Select(p => new { p.Id, p.Amount, p.PaidAt, p.PaidByName, p.Note }));
    }

    // Holatni o'zgartirish: Yangi / Tayyor / Topshirilgan
    [HttpPost("{shopId}/orders/{orderId}/status")]
    public async Task<IActionResult> SetStatus(int shopId, int orderId, [FromBody] OrderStatusDto dto)
    {
        var allowed = new[] { Order.StatusNew, Order.StatusReady, Order.StatusDelivered };
        if (!allowed.Contains(dto.Status))
            return BadRequest(new { success = false, message = "Holat noto'g'ri!" });

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shopId);
        if (order == null)
            return NotFound(new { success = false, message = "Buyurtma topilmadi!" });
        if (order.Status == Order.StatusDebt)
            return BadRequest(new { success = false, message = "Qarzdorga o'tkazilgan buyurtma holatini o'zgartirib bo'lmaydi!" });

        if (dto.Status == Order.StatusDelivered && order.TotalAmount - order.PaidAmount > 0)
            return BadRequest(new
            {
                success = false,
                message = "Buyurtma to'liq to'lanmagan! Avval qolgan summani to'lang yoki \"Qarzdorga o'tkazish\" tugmasini bosing."
            });

        order.Status = dto.Status;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // Qolgan summani bir tugma bilan "Qarzdor" bo'limiga o'tkazish.
    // Shu ismdagi qarzdor bo'lsa uning qarziga qo'shiladi, bo'lmasa yangi qarzdor yaratiladi.
    [HttpPost("{shopId}/orders/{orderId}/transfer-to-debtor")]
    public async Task<IActionResult> TransferToDebtor(int shopId, int orderId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shopId);
        if (order == null)
            return NotFound(new { success = false, message = "Buyurtma topilmadi!" });
        if (order.Status == Order.StatusDebt)
            return BadRequest(new { success = false, message = "Bu buyurtma allaqachon qarzdorga o'tkazilgan!" });

        var remaining = order.TotalAmount - order.PaidAmount;
        if (remaining <= 0)
            return BadRequest(new { success = false, message = "Qolgan summa yo'q, buyurtma to'liq to'langan!" });

        var name = order.CustomerName.Trim();
        var debtor = await _context.Debtors
            .FirstOrDefaultAsync(d => d.ShopId == shopId && d.Name.ToLower() == name.ToLower());

        if (debtor == null)
        {
            debtor = new Debtor
            {
                ShopId = shopId,
                Name = name,
                Phone = order.CustomerPhone ?? string.Empty,
                Amount = 0,
                PaidAmount = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.Debtors.Add(debtor);
        }
        else if (string.IsNullOrWhiteSpace(debtor.Phone) && !string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            debtor.Phone = order.CustomerPhone;
        }

        debtor.Amount += remaining;

        order.Status = Order.StatusDebt;
        order.TransferredAmount = remaining;
        order.TransferredAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Yangi qarzdorning Id'si saqlagandan keyin ma'lum bo'ladi
        order.DebtorId = debtor.Id;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            debtorId = debtor.Id,
            debtorName = debtor.Name,
            transferred = remaining,
            debtorRemaining = debtor.Amount - debtor.PaidAmount
        });
    }

    // Buyurtmani o'chirish (to'lovlar tarixi bilan). Qarzdorga o'tkazilgan bo'lsa — mumkin emas.
    [HttpDelete("{shopId}/orders/{orderId}")]
    public async Task<IActionResult> DeleteOrder(int shopId, int orderId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shopId);
        if (order == null)
            return NotFound(new { success = false, message = "Buyurtma topilmadi!" });
        if (order.Status == Order.StatusDebt)
            return BadRequest(new { success = false, message = "Qarzdorga o'tkazilgan buyurtmani o'chirib bo'lmaydi! Avval Qarzdor bo'limida tekshiring." });

        var payments = _context.OrderPayments.Where(p => p.OrderId == orderId && p.ShopId == shopId);
        _context.OrderPayments.RemoveRange(payments);
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}

public class OrderCreateDto
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AdvanceAmount { get; set; }
    public DateTime? DueDate { get; set; }
}

public class OrderUpdateDto
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? DueDate { get; set; }
}

public class OrderPaymentDto
{
    public decimal Amount { get; set; }
    public string? PaidByName { get; set; }
    public string? Note { get; set; }
}

public class OrderStatusDto
{
    public string Status { get; set; } = string.Empty;
}