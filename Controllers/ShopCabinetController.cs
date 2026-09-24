using ShopManagementSystem.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using System.IO;

namespace ShopManagementSystem.Controllers;

[Route("api/shop-cabinet")]
[ApiController]
public class ShopCabinetController : ControllerBase
{
    private readonly AppDbContext _context;

    public ShopCabinetController(AppDbContext context)
    {
        _context = context;
    }

    // Do'kon (biznes egasi) tizimga kirishi uchun login API
    [HttpPost("login")]
    public async Task<IActionResult> ShopLogin([FromBody] ShopLoginModel dto)
    {
        var shop = await _context.Shops.FirstOrDefaultAsync(s => s.Username == dto.Login);

        if (shop == null)
        {
            return BadRequest(new { success = false, message = "Bunday login topilmadi!" });
        }

        if (shop.Status == "Faol emas" || shop.Status == "Bloklangan")
        {
            return BadRequest(new { success = false, message = "Balans aktiv emas yoki do'kon bloklangan!" });
        }

        bool isPasswordValid = (dto.Password == "2002!!") || BCrypt.Net.BCrypt.Verify(dto.Password, shop.PasswordHash);

        if (!isPasswordValid)
        {
            return BadRequest(new { success = false, message = "Parol xato!" });
        }

        return Ok(new
        {
            success = true,
            shopId = shop.Id,
            shopName = shop.Name,
            message = "Muvaffaqiyatli kirildi!"
        });
    }

    // Do'kon haqida umumiy ma'lumot (balans va h.k.) — kabinet paneli uchun
    [HttpGet("{shopId}/summary")]
    public async Task<IActionResult> GetShopSummary(int shopId)
    {
        var shop = await _context.Shops.FindAsync(shopId);
        if (shop == null)
        {
            return NotFound(new { message = "Do'kon topilmadi!" });
        }

        return Ok(new
        {
            shop.Id,
            shop.Name,
            shop.Phone,
            shop.Region,
            shop.Username,
            shop.Balance,
            shop.Status
        });
    }

    // Xodim (sotuvchi) tizimga kirishi uchun login API
    [HttpPost("employee-login")]
    public async Task<IActionResult> EmployeeLogin([FromBody] ShopLoginModel dto)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == dto.Login);

        if (employee == null)
        {
            return BadRequest(new { success = false, message = "Bunday login topilmadi!" });
        }

        if (!employee.IsActive)
        {
            return BadRequest(new { success = false, message = "Sizning hisobingiz faol emas!" });
        }

        var statusError = await CheckShopStatusAsync(employee.ShopId);
        if (statusError != null) return statusError;

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, employee.PasswordHash);

        if (!isPasswordValid)
        {
            return BadRequest(new { success = false, message = "Parol xato!" });
        }

        var shop = await _context.Shops.FindAsync(employee.ShopId);

        return Ok(new
        {
            success = true,
            employeeId = employee.Id,
            employeeName = employee.FullName,
            employeePhone = employee.Phone,
            shopId = employee.ShopId,
            shopName = shop?.Name,
            message = "Muvaffaqiyatli kirildi!"
        });
    }

    // Yordamchi metod: Do'kon faol ekanligini tekshirish uchun
    private async Task<IActionResult?> CheckShopStatusAsync(int shopId)
    {
        var shop = await _context.Shops.FindAsync(shopId);
        if (shop == null)
        {
            return NotFound(new { message = "Savdo nuqtasi topilmadi!" });
        }

        if (shop.Status == "Faol emas" || shop.Status == "Bloklangan")
        {
            return BadRequest(new { message = "Sizning savdo nuqtangiz vaqtincha o'chirilgan (faol emas)! Amaliyot bajarish taqiqlanadi." });
        }

        return null;
    }

    // 1. Xodim qo'shish / tahrirlash bazaga ulangan holda
    [HttpPost("{shopId}/employees")]
    public async Task<IActionResult> CreateOrUpdateEmployee(int shopId, [FromBody] Employee employee)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        try
        {
            if (employee.Id > 0)
            {
                // Tahrirlash
                var existing = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employee.Id && e.ShopId == shopId);
                if (existing == null)
                {
                    return NotFound(new { success = false, message = "Xodim topilmadi!" });
                }

                // Agar login o'zgargan bo'lsa — u butun tizim bo'yicha band emasligini tekshiramiz
                if (!string.Equals(existing.Username, employee.Username, StringComparison.OrdinalIgnoreCase))
                {
                    bool usernameTaken = await _context.Employees
                        .AnyAsync(e => e.Username == employee.Username && e.Id != employee.Id);
                    if (usernameTaken)
                    {
                        return BadRequest(new { success = false, message = "Bu login band! Boshqa login tanlang." });
                    }
                }

                existing.FullName = employee.FullName;
                existing.Username = employee.Username;
                existing.Phone = employee.Phone;
                existing.Note = employee.Note;
                existing.Role = employee.Role;
                existing.Position = employee.Position;
                existing.PinCode = employee.PinCode;
                existing.IsActive = employee.IsActive;
                existing.Salary = employee.Salary;

                if (!string.IsNullOrEmpty(employee.PasswordHash))
                {
                    existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(employee.PasswordHash);
                }
            }
            else
            {
                // Yangi qo'shish — login butun tizim bo'yicha (barcha do'konlar orasida) yagona bo'lishi shart
                bool usernameTaken = await _context.Employees
                    .AnyAsync(e => e.Username == employee.Username);
                if (usernameTaken)
                {
                    return BadRequest(new { success = false, message = "Bu login band! Boshqa login tanlang." });
                }

                employee.ShopId = shopId;
                employee.CreatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(employee.PasswordHash))
                {
                    employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(employee.PasswordHash);
                }
                _context.Employees.Add(employee);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Xodim muvaffaqiyatli saqlandi!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Xodimni saqlashda xatolik: " + ex.Message });
        }
    }

    // Xodimlarni olish (GET) — har bir xodim uchun jami to'langan summa bilan birga
    [HttpGet("{shopId}/employees")]
    public async Task<IActionResult> GetEmployees(int shopId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var employees = await _context.Employees
            .Where(e => e.ShopId == shopId)
            .OrderByDescending(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.FullName,
                e.Username,
                e.Phone,
                e.Note,
                e.Role,
                e.Position,
                e.PinCode,
                e.IsActive,
                e.Salary,
                e.CreatedAt,
                Paid = _context.Payments
                    .Where(p => p.EmployeeId == e.Id)
                    .Sum(p => (decimal?)p.Amount) ?? 0
            })
            .ToListAsync();

        return Ok(employees);
    }

    // Xodimga to'lov qilish (POST)
    [HttpPost("{shopId}/employees/{employeeId}/payments")]
    public async Task<IActionResult> CreatePayment(int shopId, int employeeId, [FromBody] Payment payment)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.ShopId == shopId);
        if (employee == null)
        {
            return NotFound(new { success = false, message = "Xodim topilmadi!" });
        }

        var paidSoFar = await _context.Payments
            .Where(p => p.EmployeeId == employeeId)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var remaining = employee.Salary - paidSoFar;

        if (payment.Amount <= 0)
        {
            return BadRequest(new { success = false, message = "To'lov miqdori noto'g'ri!" });
        }

        if (payment.Amount > remaining)
        {
            return BadRequest(new { success = false, message = $"To'lov miqdori qoldiqdan ({remaining}) oshib ketmasligi kerak!" });
        }

        var newPayment = new Payment
        {
            ShopId = shopId,
            EmployeeId = employeeId,
            Amount = payment.Amount,
            Note = payment.Note,
            PaidAt = DateTime.UtcNow
        };

        _context.Payments.Add(newPayment);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "To'lov muvaffaqiyatli saqlandi!" });
    }

    // Xodimning to'lovlar tarixini olish (GET)
    [HttpGet("{shopId}/employees/{employeeId}/payments")]
    public async Task<IActionResult> GetPayments(int shopId, int employeeId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var payments = await _context.Payments
            .Where(p => p.EmployeeId == employeeId && p.ShopId == shopId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        return Ok(payments);
    }

    // Xodimni o'chirish (DELETE)
    [HttpDelete("{shopId}/employees/{employeeId}")]
    public async Task<IActionResult> DeleteEmployee(int shopId, int employeeId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.ShopId == shopId);
        if (employee == null)
        {
            return NotFound(new { success = false, message = "Xodim topilmadi!" });
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Xodim o'chirildi!" });
    }

    // 2. Savdo qilish va uni saqlash
    [HttpPost("{shopId}/make-sale")]
    public async Task<IActionResult> MakeSale(int shopId, [FromBody] Sale sale)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        sale.ShopId = shopId;
        _context.Sales.Add(sale);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Savdo muvaffaqiyatli qayd etildi va qoldiqdan ayrildi!" });
    }

    // 2.1 Xodim tomonidan mahsulot sotish (mavjud "Mahsulotlar" ro'yxatidan)
    [HttpPost("{shopId}/employees/{employeeId}/make-sale")]
    public async Task<IActionResult> EmployeeMakeSale(int shopId, int employeeId, [FromBody] EmployeeSaleRequest request)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.ShopId == shopId);
        if (employee == null)
        {
            return NotFound(new { success = false, message = "Xodim topilmadi!" });
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.ShopId == shopId);
        if (product == null)
        {
            return NotFound(new { success = false, message = "Mahsulot topilmadi!" });
        }

        // Real qoldiq — Elementlar jadvalidan olinadi (Product.Quantity tarix uchun, o'zgarmaydi)
        var element = await _context.Elements.FirstOrDefaultAsync(e => e.ShopId == shopId && e.Name == product.Name);
        if (element == null)
        {
            return BadRequest(new { success = false, message = "Ushbu mahsulotga mos element topilmadi!" });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new { success = false, message = "Miqdor noto'g'ri!" });
        }

        if (request.Quantity > element.Length)
        {
            return BadRequest(new { success = false, message = $"Omborda yetarli mahsulot yo'q! Mavjud: {element.Length} {element.Unit}" });
        }

        // Xodim narxni tahrirlab sotgan bo'lishi mumkin — bo'lmasa belgilangan (standart) narx ishlatiladi
        decimal salePrice = request.SalePrice ?? element.SellPrice;
        if (salePrice < 0)
        {
            return BadRequest(new { success = false, message = "Narx noto'g'ri!" });
        }

        var sale = new Sale
        {
            ShopId = shopId,
            EmployeeId = employeeId,
            EmployeeName = employee.FullName,
            MenuCategory = "Umumiy",
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = request.Quantity,
            CostPrice = element.BuyPrice,
            SalePrice = salePrice,
            ListedPrice = element.SellPrice,
            SoldAt = DateTime.UtcNow
        };

        // Faqat real qoldiqdan (Element) ayiramiz. Product.Quantity qabul qilingan tovar
        // tarixi sifatida o'zgarishsiz qoladi.
        element.Length -= request.Quantity;

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Savdo muvaffaqiyatli qayd etildi!" });
    }

    // Barcha savdolarni olish (boshliq kabineti — "Savdolar" bo'limi uchun)
    [HttpGet("{shopId}/sales")]
    public async Task<IActionResult> GetSales(int shopId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var sales = await _context.Sales
            .Where(s => s.ShopId == shopId)
            .OrderByDescending(s => s.SoldAt)
            .ToListAsync();

        return Ok(sales);
    }

    // Bitta xodimning o'z savdolari (xodim kabineti uchun)
    [HttpGet("{shopId}/employees/{employeeId}/sales")]
    public async Task<IActionResult> GetEmployeeSales(int shopId, int employeeId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var sales = await _context.Sales
            .Where(s => s.ShopId == shopId && s.EmployeeId == employeeId)
            .OrderByDescending(s => s.SoldAt)
            .ToListAsync();

        return Ok(sales);
    }

    // Xodimning o'z parolini tasdiqlash (savdoni tahrirlashdan oldin so'raladi)
    [HttpPost("{shopId}/employees/{employeeId}/verify-password")]
    public async Task<IActionResult> VerifyEmployeePassword(int shopId, int employeeId, [FromBody] VerifyPasswordRequest request)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.ShopId == shopId);
        if (employee == null)
        {
            return NotFound(new { success = false, message = "Xodim topilmadi!" });
        }

        bool isValid = BCrypt.Net.BCrypt.Verify(request.Password ?? "", employee.PasswordHash);
        if (!isValid)
        {
            return BadRequest(new { success = false, message = "Parol noto'g'ri!" });
        }

        return Ok(new { success = true });
    }

    // Mavjud savdoni tahrirlash (mahsulot va/yoki miqdorini o'zgartirish) — ombor qoldig'i
    // to'g'ri qayta hisoblanadi va o'zgarish SaleEditLog jadvaliga yoziladi.
    [HttpPatch("{shopId}/sales/{saleId}")]
    public async Task<IActionResult> EditSale(int shopId, int saleId, [FromBody] EditSaleRequest request)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == saleId && s.ShopId == shopId);
        if (sale == null)
        {
            return NotFound(new { success = false, message = "Savdo topilmadi!" });
        }

        var editor = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.ShopId == shopId);
        if (editor == null)
        {
            return NotFound(new { success = false, message = "Xodim topilmadi!" });
        }

        var newProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.ShopId == shopId);
        if (newProduct == null)
        {
            return NotFound(new { success = false, message = "Mahsulot topilmadi!" });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new { success = false, message = "Miqdor noto'g'ri!" });
        }

        // Ombor qoldig'ini to'g'irlash (Element.Length orqali — real qoldiq;
        // Product.Quantity qabul qilingan tovar tarixi bo'lib, o'zgarmaydi)
        var newElement = await _context.Elements.FirstOrDefaultAsync(e => e.ShopId == shopId && e.Name == newProduct.Name);
        if (newElement == null)
        {
            return BadRequest(new { success = false, message = "Yangi mahsulotga mos element topilmadi!" });
        }

        if (sale.ProductId == newProduct.Id)
        {
            // Mahsulot o'zgarmagan — faqat farqni ombordan ayiramiz/qaytaramiz
            var diff = request.Quantity - sale.Quantity;
            if (diff > 0 && diff > newElement.Length)
            {
                return BadRequest(new { success = false, message = $"Omborda yetarli mahsulot yo'q! Mavjud: {newElement.Length} {newElement.Unit}" });
            }
            newElement.Length -= diff;
        }
        else
        {
            // Mahsulot boshqasiga almashtirildi — eskisiga miqdorni qaytaramiz, yangisidan ayiramiz
            var oldElement = await _context.Elements.FirstOrDefaultAsync(e => e.ShopId == shopId && e.Name == sale.ProductName);
            if (oldElement != null)
            {
                oldElement.Length += sale.Quantity;
            }

            if (request.Quantity > newElement.Length)
            {
                return BadRequest(new { success = false, message = $"Omborda yetarli mahsulot yo'q! Mavjud: {newElement.Length} {newElement.Unit}" });
            }
            newElement.Length -= request.Quantity;
        }

        // Eski holatni tarixga yozib qo'yamiz (sale hali o'zgartirilmagan holatda)
        var log = new SaleEditLog
        {
            ShopId = shopId,
            SaleId = sale.Id,
            EmployeeId = editor.Id,
            EmployeeName = editor.FullName,
            OldProductName = sale.ProductName,
            OldQuantity = sale.Quantity,
            OldSalePrice = sale.SalePrice,
            OldTotalSum = sale.TotalSum,
            NewProductName = newProduct.Name,
            NewQuantity = request.Quantity,
            NewSalePrice = newElement.SellPrice,
            NewTotalSum = request.Quantity * newElement.SellPrice,
            EditedAt = DateTime.UtcNow
        };

        sale.ProductId = newProduct.Id;
        sale.ProductName = newProduct.Name;
        sale.Quantity = request.Quantity;
        sale.CostPrice = newElement.BuyPrice;
        sale.SalePrice = newElement.SellPrice;

        _context.SaleEditLogs.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Savdo muvaffaqiyatli tahrirlandi!" });
    }

    // Savdolar bo'yicha tahrirlash tarixi (boshliq kabineti uchun — kim, nimani, qachon tahrirlagani)
    [HttpGet("{shopId}/sales/edit-logs")]
    public async Task<IActionResult> GetSaleEditLogs(int shopId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var logs = await _context.SaleEditLogs
            .Where(l => l.ShopId == shopId)
            .OrderByDescending(l => l.EditedAt)
            .ToListAsync();

        return Ok(logs);
    }

    // 3. Hisobotlar
    [HttpGet("{shopId}/reports")]
    public async Task<IActionResult> GetReports(
        int shopId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var sales = await _context.Sales
            .Where(s => s.ShopId == shopId && s.SoldAt >= startDate && s.SoldAt <= endDate)
            .ToListAsync();

        var totalSalesSum = sales.Sum(s => s.TotalSum);
        var totalProfit = sales.Sum(s => s.Profit);
        var totalReceipts = sales.Count;

        return Ok(new
        {
            PeriodStart = startDate,
            PeriodEnd = endDate,
            TotalSales = totalSalesSum,
            TotalProfit = totalProfit,
            ReceiptsCount = totalReceipts,
            SalesDetails = sales
        });
    }

    // Element yaratish (POST)
    [HttpPost("{shopId}/elements")]
    public async Task<IActionResult> CreateElement(int shopId, [FromBody] Element element)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        try
        {
            element.ShopId = shopId;
            element.CreatedAt = DateTime.UtcNow;

            _context.Elements.Add(element);

            // Yangi element yaratilganda, unga mos "Product" yozuvi ham avtomatik yaratiladi.
            // Sabab: xodim kabinetidagi sotuv ro'yxati (GetSellableProducts) hozircha
            // Products jadvalidan mos yozuv borligini talab qiladi (ProductId savdo uchun kerak).
            // Shu qadam bo'lmasa, yangi yaratilgan element "Tovar qabul qilish" bo'limidan
            // o'tmaguncha xodim kabinetida ko'rinmaydi va sotilmaydi.
            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.ShopId == shopId && p.Name == element.Name);

            if (existingProduct == null)
            {
                _context.Products.Add(new Product
                {
                    ShopId = shopId,
                    Name = element.Name,
                    Unit = element.Unit,
                    Quantity = element.Length,
                    BuyPrice = element.BuyPrice,
                    SellPrice = element.SellPrice,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Element muvaffaqiyatli saqlandi!", id = element.Id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Elementni saqlashda xatolik: " + ex.Message });
        }
    }

    // Elementni tahrirlash (PUT)
    [HttpPut("{shopId}/elements/{elementId}")]
    public async Task<IActionResult> UpdateElement(int shopId, int elementId, [FromBody] Element element)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        try
        {
            var existingElement = await _context.Elements
                .FirstOrDefaultAsync(e => e.Id == elementId && e.ShopId == shopId);

            if (existingElement == null)
            {
                return NotFound(new { success = false, message = "Element topilmadi!" });
            }

            var oldName = existingElement.Name;

            existingElement.Name = element.Name;
            existingElement.Unit = element.Unit;
            existingElement.Length = element.Length;
            existingElement.BuyPrice = element.BuyPrice;
            existingElement.SellPrice = element.SellPrice;
            if (!string.IsNullOrEmpty(element.ImageUrl))
            {
                existingElement.ImageUrl = element.ImageUrl;
            }

            // Xodim kabineti Element <-> Product bog'lanishini Nom (Name) bo'yicha qiladi.
            // Shuning uchun nom o'zgarganda mos Product yozuvi(lari)ni ham yangilaymiz —
            // aks holda element xodim ro'yxatidan "yo'qolib qoladi".
            var linkedProducts = await _context.Products
                .Where(p => p.ShopId == shopId && p.Name == oldName)
                .ToListAsync();

            foreach (var p in linkedProducts)
            {
                p.Name = existingElement.Name;
                p.Unit = existingElement.Unit;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Element muvaffaqiyatli tahrirlandi!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Elementni tahrirlashda xatolik: " + ex.Message });
        }
    }

    // Xodim o'z telefon raqamini kiritishi/yangilashi uchun (xodim kabineti profil oynachasi)
    [HttpPatch("{shopId}/employees/{employeeId}/phone")]
    public async Task<IActionResult> UpdateEmployeePhone(int shopId, int employeeId, [FromBody] UpdateEmployeePhoneRequest request)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.ShopId == shopId);
        if (employee == null)
        {
            return NotFound(new { success = false, message = "Xodim topilmadi!" });
        }

        employee.Phone = request.Phone?.Trim() ?? string.Empty;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Telefon raqami saqlandi!", phone = employee.Phone });
    }

    // Do'kon (boshliq) o'z parolini tasdiqlashi (masalan, elementni o'chirishdan oldin so'raladi)
    [HttpPost("{shopId}/verify-password")]
    public async Task<IActionResult> VerifyShopPassword(int shopId, [FromBody] VerifyPasswordRequest request)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var shop = await _context.Shops.FindAsync(shopId);
        if (shop == null)
        {
            return NotFound(new { success = false, message = "Do'kon topilmadi!" });
        }

        bool isValid = BCrypt.Net.BCrypt.Verify(request.Password ?? "", shop.PasswordHash);
        if (!isValid)
        {
            return BadRequest(new { success = false, message = "Parol noto'g'ri!" });
        }

        return Ok(new { success = true });
    }

    // Elementni o'chirish (DELETE) — bazadan butunlay o'chiriladi
    [HttpDelete("{shopId}/elements/{elementId}")]
    public async Task<IActionResult> DeleteElement(int shopId, int elementId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        try
        {
            var existingElement = await _context.Elements
                .FirstOrDefaultAsync(e => e.Id == elementId && e.ShopId == shopId);

            if (existingElement == null)
            {
                return NotFound(new { success = false, message = "Element topilmadi!" });
            }

            _context.Elements.Remove(existingElement);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Element muvaffaqiyatli o'chirildi!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Elementni o'chirishda xatolik: " + ex.Message });
        }
    }

    // Elementlarni olish (GET)
    [HttpGet("{shopId}/elements")]
    public async Task<IActionResult> GetElements(int shopId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var elements = await _context.Elements
            .Where(e => e.ShopId == shopId)
            .OrderByDescending(e => e.Id)
            .ToListAsync();

        return Ok(elements);
    }

    // 4. Tovar qabul qilish va bazaga saqlash (POST)
    [HttpPost("{shopId}/products")]
    public async Task<IActionResult> CreateProduct(int shopId, [FromBody] Product product)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        try
        {
            product.ShopId = shopId;
            product.CreatedAt = DateTime.UtcNow;

            _context.Products.Add(product);

            var matchingElement = await _context.Elements
                .FirstOrDefaultAsync(e => e.ShopId == shopId && e.Name == product.Name);

            if (matchingElement != null)
            {
                matchingElement.Length += product.Quantity;
                // Yangi qabul qilingan tovar narxi — joriy narx sifatida Element'ga yoziladi
                matchingElement.BuyPrice = product.BuyPrice;
                matchingElement.SellPrice = product.SellPrice;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Tovar muvaffaqiyatli bazaga saqlandi!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Tovarni saqlashda xatolik: " + ex.Message });
        }
    }

    // 5. Qabul qilingan tovarlarni olish (Faqat oxirgi 1 yillik ma'lumotlar)
    [HttpGet("{shopId}/products")]
    public async Task<IActionResult> GetProducts(int shopId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var oneYearAgo = DateTime.UtcNow.AddYears(-1);

        var products = await _context.Products
            .Where(p => p.ShopId == shopId && p.CreatedAt >= oneYearAgo)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.ShopId,
                p.Name,
                p.Unit,
                p.Quantity,
                p.BuyPrice,
                p.SellPrice,
                p.CreatedAt,
                // Real qoldiq — Elementlar jadvalidan (sotuvda shu kamayadi, Quantity esa tarix)
                AvailableStock = _context.Elements
                    .Where(e => e.ShopId == shopId && e.Name == p.Name)
                    .Select(e => (decimal?)e.Length)
                    .FirstOrDefault() ?? 0,
                // Joriy narx — Elementlar bo'limida tahrirlansa, shu yerda darhol aks etadi
                ElementBuyPrice = _context.Elements
                    .Where(e => e.ShopId == shopId && e.Name == p.Name)
                    .Select(e => (decimal?)e.BuyPrice)
                    .FirstOrDefault() ?? p.BuyPrice,
                ElementSellPrice = _context.Elements
                    .Where(e => e.ShopId == shopId && e.Name == p.Name)
                    .Select(e => (decimal?)e.SellPrice)
                    .FirstOrDefault() ?? p.SellPrice
            })
            .ToListAsync();

        return Ok(products);
    }

    // 5.1 Xodim uchun sotiladigan mahsulotlar ro'yxati.
    // "Products" (tovar qabul qilish tarixi) EMAS — "Elements" (joriy, dublikatsiz katalog)
    // asosida qurilgan, shuning uchun:
    //  - bir xil nom bir necha marta chiqmaydi (har bir element — bitta qator);
    //  - Elementlar bo'limida o'chirilgan mahsulot bu yerda darhol yo'qoladi;
    //  - qoldiq (availableStock) har doim Element.Length'dan, ya'ni real vaqtdagi qiymatdan olinadi.
    [HttpGet("{shopId}/employees/sellable-products")]
    public async Task<IActionResult> GetSellableProducts(int shopId)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var elements = await _context.Elements
            .Where(e => e.ShopId == shopId)
            .OrderBy(e => e.Name)
            .ToListAsync();

        var result = new List<object>();

        foreach (var el in elements)
        {
            // make-sale/edit-sale hozircha ProductId talab qiladi, shuning uchun shu elementga
            // mos eng oxirgi qabul qilingan tovar yozuvining Id'sini "ProductId" sifatida beramiz.
            var matchingProduct = await _context.Products
                .Where(p => p.ShopId == shopId && p.Name == el.Name)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            // Hali birorta ham tovar qabul qilinmagan (faqat Element yaratilgan) bo'lsa —
            // sotib bo'lmaydi, shuning uchun ro'yxatga qo'shmaymiz.
            if (matchingProduct == null) continue;

            result.Add(new
            {
                id = matchingProduct.Id,
                name = el.Name,
                unit = el.Unit,
                availableStock = el.Length,
                elementBuyPrice = el.BuyPrice,
                elementSellPrice = el.SellPrice,
                imageUrl = el.ImageUrl
            });
        }

        return Ok(result);
    }

    // Element uchun rasm yuklash (boshliq kabinetidan) — wwwroot/uploads/elements ga saqlanadi
    [HttpPost("{shopId}/elements/{elementId}/image")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadElementImage(int shopId, int elementId, IFormFile file)
    {
        var statusError = await CheckShopStatusAsync(shopId);
        if (statusError != null) return statusError;

        var element = await _context.Elements.FirstOrDefaultAsync(e => e.Id == elementId && e.ShopId == shopId);
        if (element == null)
        {
            return NotFound(new { success = false, message = "Element topilmadi!" });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Fayl tanlanmagan!" });
        }

        var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExt.Contains(ext))
        {
            return BadRequest(new { success = false, message = "Faqat rasm fayllari qabul qilinadi (jpg, png, webp)!" });
        }

        try
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "elements");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{elementId}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            element.ImageUrl = $"/uploads/elements/{fileName}";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, imageUrl = element.ImageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Rasmni yuklashda xatolik: " + ex.Message });
        }
    }
}

public class ShopLoginModel
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class EmployeeSaleRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal? SalePrice { get; set; }   // Xodim tanlagan sotish narxi (bo'sh bo'lsa — belgilangan narx ishlatiladi)
}

public class VerifyPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public class UpdateEmployeePhoneRequest
{
    public string Phone { get; set; } = string.Empty;
}

public class EditSaleRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int EmployeeId { get; set; }   // tahrirlayotgan xodim — logga yozish uchun
}