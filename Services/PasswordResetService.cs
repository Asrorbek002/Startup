using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Services;

// Parolni tiklash mantiqi: kod yaratish, tekshirish, limitlar, parolni yangilash.
public class PasswordResetService
{
    private const int CodeLifetimeMinutes = 5;
    private const int TokenLifetimeMinutes = 10;
    private const int MaxAttempts = 5;
    private const int MaxCodesPerHourPerShop = 5;
    private const int MaxCodesPerHourPerIp = 10;
    private const int ResendCooldownSeconds = 60;
    public const int MinPasswordLength = 8;

    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(AppDbContext db, IEmailSender email, ILogger<PasswordResetService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    private async Task<Shop?> FindShopAsync(string login)
    {
        login = (login ?? string.Empty).Trim();
        if (login.Length == 0 || login.Length > 100) return null;

        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Username == login);
        if (shop != null) return shop;

        var lower = login.ToLower();
        return await _db.Shops.FirstOrDefaultAsync(s => s.Username.ToLower() == lower);
    }

    // 1-qadam: kod yaratib, do'konga biriktirilgan emailga yuboradi.
    // Login yo'q bo'lsa yoki email biriktirilmagan bo'lsa ham tashqariga hech narsa bildirmaydi.
    public async Task RequestCodeAsync(string login, string? ip)
    {
        var shop = await FindShopAsync(login);
        if (shop == null || string.IsNullOrWhiteSpace(shop.Email)) return;

        var now = DateTime.UtcNow;

        // Eski kodlarni tozalaymiz (1 kundan oshgan)
        var shopCodes = await _db.PasswordResetCodes.Where(c => c.ShopId == shop.Id).ToListAsync();
        var stale = shopCodes.Where(c => c.CreatedAt < now.AddDays(-1)).ToList();
        if (stale.Count > 0)
        {
            _db.PasswordResetCodes.RemoveRange(stale);
            shopCodes = shopCodes.Except(stale).ToList();
        }

        // Limitlar: 1 daqiqada 1 ta, 1 soatda 5 ta (bir do'kon uchun)
        if (shopCodes.Any(c => c.CreatedAt > now.AddSeconds(-ResendCooldownSeconds))) { await _db.SaveChangesAsync(); return; }
        if (shopCodes.Count(c => c.CreatedAt > now.AddHours(-1)) >= MaxCodesPerHourPerShop) { await _db.SaveChangesAsync(); return; }

        // Limit: bir IP dan 1 soatda 10 ta
        if (!string.IsNullOrEmpty(ip))
        {
            var ipCodes = await _db.PasswordResetCodes.Where(c => c.IpAddress == ip).ToListAsync();
            if (ipCodes.Count(c => c.CreatedAt > now.AddHours(-1)) >= MaxCodesPerHourPerIp) { await _db.SaveChangesAsync(); return; }
        }

        // Eski faol kodlarni bekor qilamiz
        foreach (var old in shopCodes.Where(c => !c.Used)) old.Used = true;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            ShopId = shop.Id,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            ExpiresAt = now.AddMinutes(CodeLifetimeMinutes),
            CreatedAt = now,
            IpAddress = ip
        });
        await _db.SaveChangesAsync();

        var to = shop.Email!;
        var shopId = shop.Id;
        var shopName = System.Net.WebUtility.HtmlEncode(shop.Name);
        var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:20px;border:1px solid #e5e7eb;border-radius:10px"">
  <h2 style=""color:#0d6efd;margin-top:0"">MyHisob</h2>
  <p>Assalomu alaykum! <b>{shopName}</b> hisobi uchun parolni tiklash so'rovi keldi.</p>
  <p>Tasdiqlash kodi:</p>
  <p style=""font-size:32px;letter-spacing:8px;font-weight:bold;margin:10px 0"">{code}</p>
  <p style=""color:#555"">Kod {CodeLifetimeMinutes} daqiqa amal qiladi. Kodni hech kimga bermang.</p>
  <p style=""color:#999;font-size:12px"">Agar bu so'rovni siz yubormagan bo'lsangiz, xatga e'tibor bermang.</p>
</div>";

        // Javob tezligi orqali login mavjudligi bilinib qolmasligi uchun fon rejimida yuboramiz
        _ = Task.Run(async () =>
        {
            try { await _email.SendAsync(to, "MyHisob: parolni tiklash kodi", html); }
            catch (Exception ex) { _logger.LogError(ex, "Parolni tiklash emailini yuborishda xatolik (ShopId={ShopId}).", shopId); }
        });
    }

    // 2-qadam: kodni tekshiradi. To'g'ri bo'lsa qisqa muddatli resetToken qaytaradi.
    public async Task<string?> VerifyCodeAsync(string login, string code)
    {
        var shop = await FindShopAsync(login);
        if (shop == null) return null;

        code = (code ?? string.Empty).Trim();
        if (code.Length != 6 || !code.All(char.IsDigit)) return null;

        var now = DateTime.UtcNow;
        var candidates = await _db.PasswordResetCodes.Where(c => c.ShopId == shop.Id && !c.Used).ToListAsync();
        var entry = candidates
            .Where(c => c.ResetTokenHash == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
        if (entry == null) return null;

        if (!BCrypt.Net.BCrypt.Verify(code, entry.CodeHash))
        {
            entry.Attempts++;
            if (entry.Attempts >= MaxAttempts) entry.Used = true;
            await _db.SaveChangesAsync();
            return null;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        entry.ResetTokenHash = Sha256(token);
        entry.ResetTokenExpiresAt = now.AddMinutes(TokenLifetimeMinutes);
        await _db.SaveChangesAsync();
        return token;
    }

    // 3-qadam: token bilan yangi parolni saqlaydi.
    public async Task<bool> ResetPasswordAsync(string resetToken, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(resetToken)) return false;
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength || newPassword.Length > 100) return false;

        var hash = Sha256(resetToken.Trim());
        var now = DateTime.UtcNow;

        var entry = (await _db.PasswordResetCodes.Where(c => c.ResetTokenHash == hash && !c.Used).ToListAsync())
            .FirstOrDefault(c => c.ResetTokenExpiresAt.HasValue && c.ResetTokenExpiresAt.Value > now);
        if (entry == null) return false;

        var shop = await _db.Shops.FindAsync(entry.ShopId);
        if (shop == null) return false;

        shop.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        // Shu do'konning barcha kodlarini yopamiz
        var all = await _db.PasswordResetCodes.Where(c => c.ShopId == shop.Id && !c.Used).ToListAsync();
        foreach (var c in all) c.Used = true;
        entry.Used = true;

        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(shop.Email))
        {
            var to = shop.Email!;
            var shopName = System.Net.WebUtility.HtmlEncode(shop.Name);
            var html = $@"
<div style=""font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:20px;border:1px solid #e5e7eb;border-radius:10px"">
  <h2 style=""color:#0d6efd;margin-top:0"">MyHisob</h2>
  <p><b>{shopName}</b> hisobingiz paroli o'zgartirildi.</p>
  <p style=""color:#999;font-size:12px"">Agar buni siz qilmagan bo'lsangiz, zudlik bilan administratorga murojaat qiling.</p>
</div>";
            _ = Task.Run(async () =>
            {
                try { await _email.SendAsync(to, "MyHisob: parol o'zgartirildi", html); }
                catch (Exception ex) { _logger.LogError(ex, "Parol o'zgargani haqidagi emailni yuborishda xatolik."); }
            });
        }

        return true;
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}