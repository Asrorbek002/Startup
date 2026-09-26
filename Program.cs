using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Baza ulanishi: agar Render'da DATABASE_URL bo'lsa -> PostgreSQL ishlatiladi
// Agar DATABASE_URL bo'lmasa (masalan o'zingizning kompyuteringizda) -> SQLite ishlatiladi
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Render odatda "postgres://user:pass@host:port/dbname" formatida beradi.
        // Ba'zan port yozilmagan bo'ladi — shu holda standart 5432 ishlatiladi.
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2); // parolda ':' bo'lsa ham to'g'ri ajratish uchun
        var port = uri.Port > 0 ? uri.Port : 5432;
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');

        var npgsqlConnString =
            $"Host={uri.Host};Port={port};Database={database};" +
            $"Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";

        options.UseNpgsql(npgsqlConnString);
    }
    else
    {
        // Lokal kompyuterda ishlashda hozirgidek SQLite qoladi
        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "shopmanagement.db");
        options.UseSqlite($"Data Source={dbPath}");
    }

    // Migratsiya SQLite bilan yaratilgani, ammo serverda Postgres ishlatilgani uchun
    // EF Core "model mos kelmayapti" degan yolg'on ogohlantirish beradi — shuni o'chiramiz.
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Telegram bot xizmatlari ---
builder.Services.AddHttpClient<ITelegramService, TelegramService>();
builder.Services.AddHostedService<TelegramBackgroundService>();

var app = builder.Build();

// Server ishga tushganda mavjud migratsiyalarni bazaga qo'llaydi.
// EnsureDeleted() YO'Q — shuning uchun mavjud ma'lumotlar hech qachon o'chirilmaydi.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
}

// --- 1. SHU YERGA USEDEFAULTFILES() QO'SHING ---
app.UseDefaultFiles(); // index.html ni avtomatik ochish uchun
app.UseStaticFiles();   // wwwroot papkasidagi statik fayllarni ishlatish uchun

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();