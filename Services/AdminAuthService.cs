using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ShopManagementSystem.Services;

public enum AdminLoginResult { Ok, Invalid, Locked, NotConfigured }

// Super admin kirishini serverda tekshiradi. Login/parol kodda EMAS, sozlamada (Render Environment) turadi:
//   Admin__Username (ixtiyoriy, standart "admin")
//   Admin__Password (majburiy)
//   Admin__TokenSecret (ixtiyoriy; bo'lmasa paroldan hosil qilinadi)
// Muvaffaqiyatli kirishda 12 soatlik imzolangan token beriladi. Admin endpoint'lari shu tokenni tekshiradi.
public class AdminAuthService
{
    private const int TokenHours = 12;
    private const int MaxFails = 5;
    private const int LockMinutes = 15;

    private readonly string _username;
    private readonly string _password;
    private readonly byte[] _key;
    private readonly ConcurrentDictionary<string, (int Fails, DateTime LockedUntil)> _attempts = new();

    public AdminAuthService(IConfiguration config, ILogger<AdminAuthService> logger)
    {
        _username = config["Admin:Username"] ?? "admin";
        if (string.IsNullOrWhiteSpace(_username)) _username = "admin";
        _password = config["Admin:Password"] ?? string.Empty;

        var secret = config["Admin:TokenSecret"];
        if (string.IsNullOrWhiteSpace(secret)) secret = "admin-token|" + _password;
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        if (string.IsNullOrWhiteSpace(_password))
            logger.LogWarning("Admin__Password sozlanmagan! Super admin kirishi o'chirilgan.");
    }

    public AdminLoginResult TryLogin(string login, string password, string ip, out string? token)
    {
        token = null;
        if (string.IsNullOrWhiteSpace(_password)) return AdminLoginResult.NotConfigured;

        var now = DateTime.UtcNow;
        if (_attempts.TryGetValue(ip, out var st) && st.LockedUntil > now)
            return AdminLoginResult.Locked;

        var ok = FixedEquals(login ?? "", _username) & FixedEquals(password ?? "", _password);
        if (!ok)
        {
            _attempts.AddOrUpdate(ip,
                _ => (1, DateTime.MinValue),
                (_, old) =>
                {
                    var fails = old.LockedUntil > now ? old.Fails : old.Fails + 1;
                    return fails >= MaxFails ? (0, now.AddMinutes(LockMinutes)) : (fails, DateTime.MinValue);
                });

            if (_attempts.Count > 1000)
                foreach (var kv in _attempts.Where(k => k.Value.LockedUntil < now && k.Value.Fails == 0).ToList())
                    _attempts.TryRemove(kv.Key, out _);

            return AdminLoginResult.Invalid;
        }

        _attempts.TryRemove(ip, out _);
        var exp = DateTimeOffset.UtcNow.AddHours(TokenHours).ToUnixTimeSeconds().ToString();
        token = exp + "." + Sign(exp);
        return AdminLoginResult.Ok;
    }

    public bool IsValidToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_password)) return false;

        var parts = token.Split('.');
        if (parts.Length != 2) return false;
        if (!long.TryParse(parts[0], out var exp)) return false;
        if (exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;

        return FixedEquals(parts[1], Sign(parts[0]));
    }

    private string Sign(string exp)
        => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes("admin|" + exp)));

    // Vaqt bo'yicha farq bilinmasligi uchun doimiy vaqtda solishtiradi
    private static bool FixedEquals(string a, string b)
    {
        var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }
}