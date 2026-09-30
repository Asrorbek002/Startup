using Microsoft.AspNetCore.Mvc;
using ShopManagementSystem.Services;

namespace ShopManagementSystem.Controllers;

// Parolni tiklash endpoint'lari. Login (kirish) endpoint'lari ShopCabinetController'da qoladi.
[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly PasswordResetService _service;

    public AuthController(PasswordResetService service)
    {
        _service = service;
    }

    private string? GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    // Javob har doim bir xil: login mavjudligi haqida ma'lumot bermaydi.
    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot([FromBody] ForgotDto dto)
    {
        try { await _service.RequestCodeAsync(dto?.Login ?? "", GetClientIp()); }
        catch { /* xatoni ham yashiramiz, javob bir xil bo'lsin */ }

        return Ok(new { message = "Agar login mavjud va unga email biriktirilgan bo'lsa, tasdiqlash kodi yuborildi." });
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDto dto)
    {
        var token = await _service.VerifyCodeAsync(dto?.Login ?? "", dto?.Code ?? "");
        if (token == null)
            return BadRequest(new { message = "Kod noto'g'ri yoki muddati tugagan." });

        return Ok(new { resetToken = token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (string.IsNullOrEmpty(dto?.NewPassword) || dto.NewPassword.Length < PasswordResetService.MinPasswordLength)
            return BadRequest(new { message = $"Parol kamida {PasswordResetService.MinPasswordLength} belgidan iborat bo'lishi kerak." });

        var ok = await _service.ResetPasswordAsync(dto.ResetToken ?? "", dto.NewPassword);
        if (!ok)
            return BadRequest(new { message = "Sessiya muddati tugagan. Qaytadan urinib ko'ring." });

        return Ok(new { message = "Parol muvaffaqiyatli yangilandi." });
    }
}

public class ForgotDto
{
    public string Login { get; set; } = string.Empty;
}

public class VerifyCodeDto
{
    public string Login { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string ResetToken { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}