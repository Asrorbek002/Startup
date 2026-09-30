using Microsoft.AspNetCore.Mvc;
using ShopManagementSystem.Services;

namespace ShopManagementSystem.Controllers;

// Super admin kirishi. Parol serverda tekshiriladi, brauzer kodida saqlanmaydi.
[Route("api/admin-auth")]
[ApiController]
public class AdminAuthController : ControllerBase
{
    private readonly AdminAuthService _auth;

    public AdminAuthController(AdminAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = _auth.TryLogin(dto?.Login ?? "", dto?.Password ?? "", ip, out var token);

        switch (result)
        {
            case AdminLoginResult.Ok:
                return Ok(new { token });
            case AdminLoginResult.Locked:
                return StatusCode(429, new { message = "Juda ko'p noto'g'ri urinish. 15 daqiqadan keyin qayta urinib ko'ring." });
            case AdminLoginResult.NotConfigured:
                return StatusCode(503, new { message = "Admin paroli serverda sozlanmagan (Admin__Password)." });
            default:
                await Task.Delay(600); // brute-force'ni sekinlashtiradi
                return Unauthorized(new { message = "Login yoki parol xato!" });
        }
    }
}

public class AdminLoginDto
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}