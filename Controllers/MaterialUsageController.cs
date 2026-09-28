using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;
using ShopManagementSystem.Services;

namespace ShopManagementSystem.Controllers;

// Sex xodimlari (Usta / Pechat) uchun material sarfi.
// Ombor qoldig'i sifatida mavjud Element.Length ishlatiladi (savdo ham shundan ayiradi),
// shuning uchun hamma narsa bitta bazada, bitta qoldiqda ko'rinadi.
[Route("api/shop-cabinet")]
[ApiController]
public class MaterialUsageController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITelegramService _telegram;

    // Bir vaqtda ikki so'rov qoldiqni buzmasligi uchun (bitta server nusxasi ichida)
    private static readonly SemaphoreSlim StockLock = new(1, 1);

    // Xodim o'z yozuvini shuncha daqiqa ichida o'zi bekor qila oladi
    private const int EmployeeCancelMinutes = 15;

    public MaterialUsageController(AppDbContext context, ITelegramService telegram)
    {
        _context = context;
        _telegram = telegram;
    }

    // Xodim turini yagona ko'rinishga keltiradi: Sotuvchi / Usta / Pechat.
    // Eski xodimlarda Role bo'sh — ular avtomatik "Sotuvchi" hisoblanadi.
    public static string NormalizeRole(string? role)
    {
        var r = (role ?? string.Empty).Trim().ToLowerInvariant();
        if (r.Contains("usta")) return "Usta";
        if (r.Contains("pechat") || r.Contains("print")) return "Pechat";
        return "Sotuvchi";
    }

    // "Metr (m)" true; "Santimetr (sm)" va "Kvadrat metr (m²)" false
    private static bool IsMeter(string? unit)
        => (unit ?? string.Empty).Trim().StartsWith("metr", StringComparison.OrdinalIgnoreCase);

    // Employee.AllowedElementIds ("3,5,8") ni to'plamga aylantiradi. null = cheklov yo'q (hamma element)
    private static HashSet<int>? ParseAllowedIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToHashSet();
        return ids.Count == 0 ? null : ids;
    }

    // Xodim shu materialdan foydalana oladimi? (Pechat: faqat metr; + boshliq belgilagan elementlar)
    private static bool IsElementAllowed(Employee employee, string role, int elementId, string? unit)
    {
        if (role == "Pechat" && !IsMeter(unit)) return false;
        var allowed = ParseAllowedIds(employee.AllowedElementIds);
        return allowed == null || allowed.Contains(elementId);
    }

    private async Task<IActionResult?> CheckShopAsync(int shopId)
    {
        var shop = await _context.Shops.FindAsync(shopId);
        if (shop == null)
            return NotFound(new { success = false, message = "Savdo nuqtasi topilmadi!" });

        if (shop.Status == "Faol emas" || shop.Status == "Bloklangan")
            return BadRequest(new { success = false, message = "Sizning savdo nuqtangiz vaqtincha o'chirilgan (faol emas)!" });

        return null;
    }

    private static object ToDto(MaterialUsage u) => new
    {
        id = u.Id,
        employeeId = u.EmployeeId,
        employeeName = u.EmployeeName,
        employeeRole = u.EmployeeRole,
        elementId = u.ElementId,
        elementName = u.ElementName,
        unit = u.Unit,
        quantity = u.Quantity,
        outputCount = u.OutputCount,
        note = u.Note,
        createdAt = DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc),
        isCancelled = u.IsCancelled,
        cancelledAt = u.CancelledAt.HasValue ? DateTime.SpecifyKind(u.CancelledAt.Value, DateTimeKind.Utc) : (DateTime?)null,
        cancelReason = u.CancelReason,
        cancelledBy = u.CancelledBy
    };

    // Xodim kabineti ochilganda: xodim qaysi turda (Sotuvchi / Usta / Pechat)
    [HttpGet("{shopId}/employees/{employeeId}/profile")]
    public async Task<IActionResult> GetEmployeeProfile(int shopId, int employeeId)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.ShopId == shopId);
        if (employee == null)
            return NotFound(new { success = false, message = "Xodim topilmadi!" });

        return Ok(new
        {
            success = true,
            employeeId = employee.Id,
            fullName = employee.FullName,
            role = NormalizeRole(employee.Role),
            isActive = employee.IsActive
        });
    }

    // Ombordagi barcha material/tovarlar: joriy qoldiq va sex xodimlari jami ishlatgan miqdor
    [HttpGet("{shopId}/materials/stock")]
    // employeeId berilsa (xodim kabineti) — faqat shu xodimga ruxsat etilgan materiallar qaytadi.
    // berilmasa (boshliq kabineti) — hammasi.
    public async Task<IActionResult> GetMaterialStock(int shopId, [FromQuery] int? employeeId)
    {
        var err = await CheckShopAsync(shopId);
        if (err != null) return err;

        var elements = await _context.Elements
            .Where(e => e.ShopId == shopId)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Unit, e.Length })
            .ToListAsync();

        if (employeeId.HasValue)
        {
            var emp = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId.Value && e.ShopId == shopId);
            if (emp == null)
                return NotFound(new { success = false, message = "Xodim topilmadi!" });

            var empRole = NormalizeRole(emp.Role);
            elements = elements
                .Where(e => IsElementAllowed(emp, empRole, e.Id, e.Unit))
                .ToList();
        }

        // decimal yig'indisini SQLite ham, Postgres ham bir xil hisoblashi uchun xotirada hisoblaymiz
        var usedRows = await _context.MaterialUsages
            .Where(u => u.ShopId == shopId && !u.IsCancelled)
            .Select(u => new { u.ElementId, u.Quantity })
            .ToListAsync();

        var usedByElement = usedRows
            .GroupBy(u => u.ElementId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var result = elements.Select(e => new
        {
            elementId = e.Id,
            name = e.Name,
            unit = e.Unit,
            stock = e.Length,
            usedTotal = usedByElement.TryGetValue(e.Id, out var used) ? used : 0m
        });

        return Ok(result);
    }

    // Sarf tarixi. employeeId berilsa — faqat shu xodimniki (xodim kabineti uchun),
    // berilmasa — hammasi (boshliq kabineti uchun).
    [HttpGet("{shopId}/material-usages")]
    public async Task<IActionResult> GetUsages(
        int shopId,
        [FromQuery] int? employeeId,
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate)
    {
        var err = await CheckShopAsync(shopId);
        if (err != null) return err;

        var query = _context.MaterialUsages.Where(u => u.ShopId == shopId);

        if (employeeId.HasValue)
            query = query.Where(u => u.EmployeeId == employeeId.Value);
        if (fromDate.HasValue)
        {
            var start = fromDate.Value;
            query = query.Where(u => u.CreatedAt >= start);
        }
        if (toDate.HasValue)
        {
            var end = toDate.Value.AddDays(1);
            query = query.Where(u => u.CreatedAt < end);
        }

        var rows = await query
            .OrderByDescending(u => u.CreatedAt)
            .Take(500)
            .ToListAsync();

        return Ok(rows.Select(ToDto));
    }

    // Material sarfini yozish. Qoldiqdan ko'p bo'lsa rad etiladi.
    [HttpPost("{shopId}/material-usages")]
    public async Task<IActionResult> CreateUsage(int shopId, [FromBody] MaterialUsageRequest request)
    {
        var err = await CheckShopAsync(shopId);
        if (err != null) return err;

        if (request == null)
            return BadRequest(new { success = false, message = "Ma'lumotlar noto'g'ri!" });

        var quantity = Math.Round(request.Quantity, 3);
        if (quantity <= 0)
            return BadRequest(new { success = false, message = "Miqdor 0 dan katta bo'lishi kerak!" });
        if (quantity > 1_000_000_000m)
            return BadRequest(new { success = false, message = "Miqdor juda katta!" });

        if (request.OutputCount.HasValue && request.OutputCount.Value < 0)
            return BadRequest(new { success = false, message = "Chiqarilgan pechat soni noto'g'ri!" });

        await StockLock.WaitAsync();
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.ShopId == shopId);
            if (employee == null)
                return NotFound(new { success = false, message = "Xodim topilmadi!" });
            if (!employee.IsActive)
                return BadRequest(new { success = false, message = "Bu xodim faol emas!" });

            var role = NormalizeRole(employee.Role);
            if (role == "Sotuvchi")
                return BadRequest(new { success = false, message = "Material sarfi faqat Usta yoki Pechat xodimlari uchun!" });

            var element = await _context.Elements
                .FirstOrDefaultAsync(e => e.Id == request.ElementId && e.ShopId == shopId);
            if (element == null)
                return NotFound(new { success = false, message = "Material topilmadi!" });

            if (!IsElementAllowed(employee, role, element.Id, element.Unit))
                return BadRequest(new { success = false, message = "Bu material sizga ruxsat etilmagan!" });

            if (quantity > element.Length)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Omborda yetarli emas! Mavjud: {element.Length} {element.Unit}"
                });
            }

            var stockBefore = element.Length;
            element.Length -= quantity;

            var usage = new MaterialUsage
            {
                ShopId = shopId,
                EmployeeId = employee.Id,
                EmployeeName = employee.FullName,
                EmployeeRole = role,
                ElementId = element.Id,
                ElementName = element.Name,
                Unit = element.Unit,
                Quantity = quantity,
                OutputCount = role == "Pechat" ? request.OutputCount : null,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.MaterialUsages.Add(usage);

            // Qoldiq kamayishi va yozuv bitta SaveChanges (bitta tranzaksiya) ichida saqlanadi
            await _context.SaveChangesAsync();

            // Qoldiq chegaradan pastga tushgan bo'lsa, Telegramga darrov xabar
            await LowStockAlert.NotifyAsync(_context, _telegram, shopId,
                new StockChange(element.Name, element.Unit, stockBefore, element.Length));

            return Ok(new
            {
                success = true,
                message = "Sarf yozildi!",
                id = usage.Id,
                remaining = element.Length,
                unit = element.Unit
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Sarfni saqlashda xatolik: " + (ex.InnerException?.Message ?? ex.Message)
            });
        }
        finally
        {
            StockLock.Release();
        }
    }

    // Sarfni bekor qilish: yozuv o'chmaydi, "bekor qilingan" deb belgilanadi va qoldiq qaytadi.
    // employeeId berilsa (xodim o'zi) — faqat o'z yozuvi va faqat dastlabki daqiqalarda.
    // employeeId berilmasa (boshliq) — istalgan vaqtda.
    [HttpPost("{shopId}/material-usages/{usageId}/cancel")]
    public async Task<IActionResult> CancelUsage(int shopId, int usageId, [FromBody] CancelUsageRequest request)
    {
        var err = await CheckShopAsync(shopId);
        if (err != null) return err;

        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length < 3)
            return BadRequest(new { success = false, message = "Bekor qilish sababini yozing!" });

        await StockLock.WaitAsync();
        try
        {
            var usage = await _context.MaterialUsages
                .FirstOrDefaultAsync(u => u.Id == usageId && u.ShopId == shopId);
            if (usage == null)
                return NotFound(new { success = false, message = "Yozuv topilmadi!" });
            if (usage.IsCancelled)
                return BadRequest(new { success = false, message = "Bu yozuv allaqachon bekor qilingan!" });

            string cancelledBy;
            if (request!.EmployeeId.HasValue)
            {
                if (request.EmployeeId.Value != usage.EmployeeId)
                    return BadRequest(new { success = false, message = "Faqat o'zingizning yozuvingizni bekor qila olasiz!" });

                var age = DateTime.UtcNow - DateTime.SpecifyKind(usage.CreatedAt, DateTimeKind.Utc);
                if (age.TotalMinutes > EmployeeCancelMinutes)
                    return BadRequest(new
                    {
                        success = false,
                        message = $"{EmployeeCancelMinutes} daqiqadan o'tib ketdi. Bekor qilish uchun boshliqqa murojaat qiling."
                    });

                cancelledBy = usage.EmployeeName;
            }
            else
            {
                cancelledBy = "Boshliq";
            }

            // Materialga qoldiqni qaytaramiz (agar material o'chirib yuborilgan bo'lsa — faqat belgilanadi)
            var element = await _context.Elements
                .FirstOrDefaultAsync(e => e.Id == usage.ElementId && e.ShopId == shopId);
            if (element != null)
                element.Length += usage.Quantity;

            usage.IsCancelled = true;
            usage.CancelledAt = DateTime.UtcNow;
            usage.CancelReason = reason;
            usage.CancelledBy = cancelledBy;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Sarf bekor qilindi, qoldiq qaytarildi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Bekor qilishda xatolik: " + (ex.InnerException?.Message ?? ex.Message)
            });
        }
        finally
        {
            StockLock.Release();
        }
    }
}

public class MaterialUsageRequest
{
    public int EmployeeId { get; set; }
    public int ElementId { get; set; }
    public decimal Quantity { get; set; }
    public int? OutputCount { get; set; }
    public string? Note { get; set; }
}

public class CancelUsageRequest
{
    public string? Reason { get; set; }
    public int? EmployeeId { get; set; }
}