using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace ShopManagementSystem.Services;

// Email yuborish. Ikki usul qo'llab-quvvatlanadi:
//   1) Brevo API  (Email:BrevoApiKey berilgan bo'lsa)  - Render'da SMTP bloklangan bo'lsa shu ishonchli
//   2) Gmail/har qanday SMTP (Email:SmtpUser + Email:SmtpPassword)
// Maxfiy qiymatlar (parol, kalit) kodga yozilmaydi: Render -> Environment bo'limida
// Email__SmtpUser, Email__SmtpPassword, Email__BrevoApiKey nomlari bilan qo'yiladi.
public class EmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(HttpClient http, IConfiguration config, ILogger<EmailSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var brevoKey = _config["Email:BrevoApiKey"];
        if (!string.IsNullOrWhiteSpace(brevoKey))
        {
            await SendViaBrevoAsync(brevoKey, to, subject, htmlBody);
            return;
        }

        await SendViaSmtpAsync(to, subject, htmlBody);
    }

    private async Task SendViaSmtpAsync(string to, string subject, string htmlBody)
    {
        var host = _config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host)) host = "smtp.gmail.com";
        var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
        var user = _config["Email:SmtpUser"];
        var pass = _config["Email:SmtpPassword"];

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException("Email sozlanmagan: Email__SmtpUser va Email__SmtpPassword (yoki Email__BrevoApiKey) kiritilmagan.");

        var fromAddress = _config["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(fromAddress)) fromAddress = user;
        var fromName = _config["Email:FromName"];
        if (string.IsNullOrWhiteSpace(fromName)) fromName = "MyHisob";

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress!, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
            Timeout = 20000
        };

        await client.SendMailAsync(message);
    }

    private async Task SendViaBrevoAsync(string apiKey, string to, string subject, string htmlBody)
    {
        var fromAddress = _config["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("Brevo uchun Email__FromAddress (tasdiqlangan yuboruvchi email) kiritilmagan.");
        var fromName = _config["Email:FromName"];
        if (string.IsNullOrWhiteSpace(fromName)) fromName = "MyHisob";

        var payload = new
        {
            sender = new { name = fromName, email = fromAddress },
            to = new[] { new { email = to } },
            subject,
            htmlContent = htmlBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("api-key", apiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Brevo xatosi {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Brevo orqali email yuborilmadi.");
        }
    }
}